using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

[StructLayout(LayoutKind.Sequential)]
internal struct HighContrastNative
{
    internal uint size, flags;
    internal IntPtr scheme;
}

internal enum SchemeOwnership { Unknown, BorrowedSystem }
internal readonly record struct HighContrastGet(bool Success, HighContrastNative State, int Error);
internal sealed record HighContrastCopy(uint Flags, bool PointerWasNull, string? Scheme)
{
    internal string Kind => PointerWasNull ? "NULL" : Scheme!.Length == 0 ? "EMPTY" : "NAMED";
}

internal static class HighContrastReader
{
    // Microsoft explicitly corrects the Learn page's deallocation instruction.
    internal const string Contract = "dotnet/winforms@295f067453524c1eef18fcbe85e14431bd8aef17:HIGHCONTRASTW-borrowed";
    internal const int MaximumCodeUnits = 32768; // Includes the required terminator.

    internal static void ValidateAbi(int pointerBytes, int structureBytes, int sizeOffset,
        int flagsOffset, int pointerOffset, string architecture)
    {
        if (pointerBytes != 8 || structureBytes != 16 || sizeOffset != 0 || flagsOffset != 4 ||
            pointerOffset != 8 || architecture != "X64")
            throw new InvalidOperationException("Unsupported HIGHCONTRASTW ABI or architecture.");
    }

    internal static HighContrastCopy Read(SchemeOwnership ownership, Func<HighContrastGet> get,
        Func<IntPtr, int, char>? readCodeUnit = null)
    {
        if (ownership != SchemeOwnership.BorrowedSystem)
            throw new InvalidOperationException("Unknown HIGHCONTRAST GET ownership; native access rejected.");
        ValidateAbi(IntPtr.Size, Marshal.SizeOf<HighContrastNative>(),
            Marshal.OffsetOf<HighContrastNative>(nameof(HighContrastNative.size)).ToInt32(),
            Marshal.OffsetOf<HighContrastNative>(nameof(HighContrastNative.flags)).ToInt32(),
            Marshal.OffsetOf<HighContrastNative>(nameof(HighContrastNative.scheme)).ToInt32(),
            RuntimeInformation.ProcessArchitecture.ToString());
        var result = get();
        if (!result.Success) throw new Win32Exception(result.Error, "SPI_GETHIGHCONTRAST failed.");
        if (result.State.size != 16) throw new InvalidOperationException("Returned HIGHCONTRASTW size differs.");
        if (result.State.scheme == IntPtr.Zero) return new(result.State.flags, true, null);

        // No logging/callback except the bounded reader occurs while copying the borrowed value.
        // The cap is not a claim that an arbitrary address is readable: the real path relies
        // on the API's valid, null-terminated UTF-16 pointer contract. No address escapes.
        readCodeUnit ??= static (pointer, index) => (char)Marshal.ReadInt16(pointer, checked(index * 2));
        var text = new StringBuilder();
        var awaitingLowSurrogate = false;
        for (var index = 0; index < MaximumCodeUnits; index++)
        {
            var character = readCodeUnit(result.State.scheme, index);
            if (character == '\0')
            {
                if (awaitingLowSurrogate) throw new IOException("Incomplete UTF-16 surrogate pair.");
                return new(result.State.flags, false, text.ToString());
            }
            if (awaitingLowSurrogate && !char.IsLowSurrogate(character) ||
                !awaitingLowSurrogate && char.IsLowSurrogate(character))
                throw new IOException("Malformed UTF-16 scheme.");
            awaitingLowSurrogate = char.IsHighSurrogate(character);
            text.Append(character);
        }
        throw new IOException("HIGHCONTRAST scheme is not terminated within the fixed bound.");
        // Borrowed OS memory is never released, including error paths. Managed copies
        // own their contents. Only a fixture's allocator may release fixture storage.
    }
}
