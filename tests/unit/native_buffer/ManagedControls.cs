using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

internal static class ManagedControls
{
    private static readonly List<string> Passed = new();
    private static void Check(bool value) { if (!value) throw new InvalidOperationException("Control failed."); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected failure was not retained: " + typeof(T).Name);
    }
    private static void Test(string name, Action body)
    {
        body(); Check(Native.FixtureCount(4) == 0); Passed.Add(name);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly ulong Token;
        private bool disposed;
        internal Fixture(string units)
        {
            Token = Native.FixtureCreate(units.Select(c => (ushort)c).ToArray(), (uint)units.Length);
            Check(Token != 0);
        }
        internal HighContrastGet Get(uint mode = 0, uint size = 16)
        {
            Check(Native.FixtureSelect(Token, mode));
            var state = new HighContrastNative { size = size };
            var success = Native.FixtureGet(0x42, size, ref state, 0);
            var error = success ? 0 : Marshal.GetLastPInvokeError();
            return new(success, state, error);
        }
        internal char Unit(IntPtr pointer, int index)
        {
            if (!Native.FixtureRead(Token, pointer, (uint)index, out var result))
                throw new IOException("Controlled native extent rejected the read.");
            return (char)result;
        }
        internal HighContrastCopy Read(uint mode = 0) => HighContrastReader.Read(SchemeOwnership.BorrowedSystem, () => Get(mode), Unit);
        public void Dispose()
        {
            if (disposed) return;
            Check(Native.FixtureOwnerRelease(Token)); disposed = true;
        }
    }

    private static int Main()
    {
        try
        {
            Test("Native SDK layout agrees with the actual managed type", () => {
                HighContrastReader.ValidateAbi((int)Native.FixtureAbi(0), (int)Native.FixtureAbi(1),
                    (int)Native.FixtureAbi(2), (int)Native.FixtureAbi(3), (int)Native.FixtureAbi(4), "X64");
                Check(Marshal.SizeOf<HighContrastNative>() == Native.FixtureAbi(1));
                Check(Native.FixtureAbi(5) == 2 && Native.FixtureAbi(6) == 4);
            });
            Test("Wrong layout and non-x64 architecture are rejected", () => {
                foreach (var bad in new[] { (4, 12, 0, 4, 8, "X86"), (8, 24, 0, 4, 8, "X64"),
                    (8, 16, 4, 0, 8, "X64"), (8, 16, 0, 4, 4, "X64"), (8, 16, 0, 4, 8, "Arm64") })
                    Throws<InvalidOperationException>(() => HighContrastReader.ValidateAbi(bad.Item1, bad.Item2, bad.Item3, bad.Item4, bad.Item5, bad.Item6));
            });
            Test("Unknown ownership stops before native GET", () => {
                var calls = 0;
                Throws<InvalidOperationException>(() => HighContrastReader.Read(SchemeOwnership.Unknown, () => { calls++; return default; }));
                Check(calls == 0);
            });
            Test("NULL and non-null empty values remain different", () => {
                using var fixture = new Fixture("\0"); var empty = fixture.Read(); var reads = Native.FixtureCount(3); var missing = fixture.Read(1);
                Check(empty.Kind == "EMPTY" && empty.Scheme == "" && !empty.PointerWasNull);
                Check(missing.Kind == "NULL" && missing.Scheme is null && missing.PointerWasNull);
                Check(Native.FixtureCount(3) == reads);
            });
            Test("Named UTF-16 and raw flags are preserved", () => {
                using var fixture = new Fixture("Café 🎨\0"); var copy = fixture.Read();
                Check(copy.Kind == "NAMED" && copy.Scheme == "Café 🎨" && copy.Flags == 0x8000007e);
            });
            Test("Actual Marshal reader copies only known fixture-owned terminated buffers", () => {
                foreach (var text in new[] { "", "Café 🎨" }) {
                    using var fixture = new Fixture(text + "\0"); var releases = Native.FixtureCount(1);
                    var value = HighContrastReader.Read(SchemeOwnership.BorrowedSystem, () => fixture.Get());
                    Check(value.Scheme == text && !value.PointerWasNull && Native.FixtureCount(1) == releases);
                }
            });
            Test("Same borrowed pointer may be reused with different contents", () => {
                using var fixture = new Fixture("AA\0"); var firstPointer = fixture.Get().State.scheme; var first = fixture.Read();
                Check(Native.FixtureReplace(fixture.Token, new ushort[] { 'B', 'B', 0 }, 3));
                var second = fixture.Read(); Check(fixture.Get().State.scheme == firstPointer);
                Check(first.Scheme == "AA" && second.Scheme == "BB");
            });
            Test("Distinct owners do not share or overwrite snapshots", () => {
                using var first = new Fixture("A\0"); using var second = new Fixture("B\0");
                Check(first.Get().State.scheme != second.Get().State.scheme);
                Check(first.Read().Scheme == "A" && second.Read().Scheme == "B");
            });
            Test("Failed native GET never touches its invalid output pointer", () => {
                using var fixture = new Fixture("\0"); var reads = 0;
                try { HighContrastReader.Read(SchemeOwnership.BorrowedSystem, () => fixture.Get(2), (_, _) => { reads++; throw new IOException(); }); }
                catch (Win32Exception error) { Check(error.NativeErrorCode == 5); Check(reads == 0); return; }
                throw new IOException("GET failure became a result.");
            });
            Test("Wrong input or returned native size is rejected", () => {
                using var fixture = new Fixture("\0");
                Throws<Win32Exception>(() => HighContrastReader.Read(SchemeOwnership.BorrowedSystem, () => fixture.Get(0, 12), fixture.Unit));
                Throws<InvalidOperationException>(() => fixture.Read(3));
            });
            Test("Copy failure propagates without releasing borrowed storage", () => {
                using var fixture = new Fixture("A\0"); var releases = Native.FixtureCount(1);
                Throws<IOException>(() => HighContrastReader.Read(SchemeOwnership.BorrowedSystem, () => fixture.Get(), (_, _) => throw new IOException("injected")));
                Check(Native.FixtureCount(1) == releases); Check(fixture.Read().Scheme == "A");
            });
            Test("Truncated native fixture is rejected at its known extent", () => {
                using var fixture = new Fixture("AB"); Throws<IOException>(() => fixture.Read());
            });
            Test("Unterminated string stops at the fixed cap", () => {
                using var fixture = new Fixture("\0"); var reads = 0;
                Throws<IOException>(() => HighContrastReader.Read(SchemeOwnership.BorrowedSystem, () => fixture.Get(), (_, _) => { reads++; return 'A'; }));
                Check(reads == HighContrastReader.MaximumCodeUnits);
            });
            Test("Malformed UTF-16 is rejected without replacement characters", () => {
                foreach (var value in new[] { "\ud800\0", "\udc00\0", "\ud800X\0", "\ud800\ud800\0" }) {
                    using var fixture = new Fixture(value); Throws<IOException>(() => fixture.Read());
                }
            });
            Test("Terminator at the fixed final position is accepted", () => {
                using var fixture = new Fixture(new string('A', HighContrastReader.MaximumCodeUnits - 1) + "\0");
                Check(fixture.Read().Scheme!.Length == HighContrastReader.MaximumCodeUnits - 1);
            });
            Test("Incorrect borrower and duplicate owner release attempts fail", () => {
                using var fixture = new Fixture("\0"); var releases = Native.FixtureCount(1);
                Check(!Native.FixtureBorrowerRelease(fixture.Token)); Check(Marshal.GetLastPInvokeError() == 5);
                Check(!Native.FixtureBorrowerRelease(fixture.Token)); Check(Native.FixtureCount(1) == releases);
                Check(fixture.Read().Kind == "EMPTY"); fixture.Dispose();
                Check(!Native.FixtureOwnerRelease(fixture.Token)); Check(Marshal.GetLastPInvokeError() == 6);
                Check(Native.FixtureCount(1) == releases + 1);
            });
            Test("Repeated ownership cycles release each fixture allocation once", () => {
                var allocations = Native.FixtureCount(0); var releases = Native.FixtureCount(1);
                for (var index = 0; index < 32; index++) {
                    using var fixture = new Fixture("\0"); Check(fixture.Read().Kind == "EMPTY"); Check(fixture.Read().Kind == "EMPTY");
                }
                Check(Native.FixtureCount(0) == allocations + 32 && Native.FixtureCount(1) == releases + 32);
            });
            Test("Fixture owner cleans up after a conversion exception", () => {
                var releases = Native.FixtureCount(1);
                Throws<IOException>(() => { using var fixture = new Fixture("X"); fixture.Read(); });
                Check(Native.FixtureCount(1) == releases + 1);
            });
            Test("Managed copies survive release by the real fixture owner", () => {
                HighContrastCopy value;
                using (var fixture = new Fixture("retained\0")) value = fixture.Read();
                Check(value.Scheme == "retained");
            });
            Test("Post-copy observation failure preserves exactly-once fixture cleanup", () => {
                var releases = Native.FixtureCount(1);
                Throws<IOException>(() => { using var fixture = new Fixture("\0"); fixture.Read(); throw new IOException("journal failed"); });
                Check(Native.FixtureCount(1) == releases + 1);
            });
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToArray();
            Check(!assemblies.Any(a => a is "PresentationFramework" or "PresentationCore" or "WindowsBase" or "System.Windows.Forms"));
            Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS_CONTROLLED_SIMULATION_ONLY", tests = Passed.Count,
                passed = Passed, runtime = Environment.Version.ToString(), architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                coreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(object).Assembly.Location))).ToLowerInvariant(),
                allocations = Native.FixtureCount(0), ownerReleases = Native.FixtureCount(1), activeAllocations = Native.FixtureCount(4),
                borrowerReleaseDenials = Native.FixtureCount(2), actualHighContrastCalls = 0, uiCalls = 0, liveCertification = false,
                ownershipContract = HighContrastReader.Contract, assemblies }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static class Native
    {
        private const string Library = "NativeBufferFixture.dll";
        [DllImport(Library, ExactSpelling = true)] internal static extern uint FixtureAbi(uint field);
        [DllImport(Library, ExactSpelling = true, SetLastError = true)] internal static extern ulong FixtureCreate([In] ushort[] units, uint count);
        [DllImport(Library, ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool FixtureSelect(ulong token, uint mode);
        [DllImport(Library, ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool FixtureReplace(ulong token, [In] ushort[] units, uint count);
        [DllImport(Library, ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool FixtureGet(uint action, uint size, ref HighContrastNative value, uint flags);
        [DllImport(Library, ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool FixtureRead(ulong token, IntPtr pointer, uint index, out ushort unit);
        [DllImport(Library, ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool FixtureBorrowerRelease(ulong token);
        [DllImport(Library, ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool FixtureOwnerRelease(ulong token);
        [DllImport(Library, ExactSpelling = true)] internal static extern uint FixtureCount(uint field);
    }
}
