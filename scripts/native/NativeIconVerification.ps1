# Inspect PE icon resources as data. No application or installer code is executed.
if (-not ('LensNativeIconResources' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class LensNativeIconResources {
    private delegate bool ResourceName(IntPtr module, IntPtr type, IntPtr name, IntPtr parameter);
    [DllImport("kernel32", CharSet=CharSet.Unicode, SetLastError=true)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32", SetLastError=true)] private static extern bool FreeLibrary(IntPtr module);
    [DllImport("kernel32", CharSet=CharSet.Unicode, SetLastError=true)] private static extern bool EnumResourceNames(IntPtr module, IntPtr type, ResourceName callback, IntPtr parameter);
    [DllImport("kernel32", CharSet=CharSet.Unicode, SetLastError=true)] private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32", SetLastError=true)] private static extern uint SizeofResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32", SetLastError=true)] private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32")] private static extern IntPtr LockResource(IntPtr resource);
    private static byte[] Read(IntPtr module, IntPtr name, int type) {
        var resource = FindResource(module, name, new IntPtr(type));
        if (resource == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        var size = SizeofResource(module, resource);
        if (size == 0 || size > 4 * 1024 * 1024) throw new InvalidOperationException("Icon resource exceeds its inspection budget.");
        var bytes = new byte[size]; Marshal.Copy(LockResource(LoadResource(module, resource)), bytes, 0, bytes.Length); return bytes;
    }
    public static byte[][][] Groups(string path) {
        var module = LoadLibraryEx(path, IntPtr.Zero, 0x60); // image resource + exclusive data file
        if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        var groups = new List<byte[][]>(); Exception failure = null;
        ResourceName callback = (library, type, name, parameter) => {
            try {
                var group = Read(library, name, 14);
                if (group.Length < 6 || BitConverter.ToUInt16(group, 2) != 1) throw new InvalidOperationException("Invalid icon group.");
                var count = BitConverter.ToUInt16(group, 4);
                if (count == 0 || count > 32 || group.Length != 6 + count * 14) throw new InvalidOperationException("Invalid icon group size.");
                var images = new List<byte[]>();
                for (var index = 0; index < count; index++) {
                    var offset = 6 + index * 14;
                    var bytes = Read(library, new IntPtr(BitConverter.ToUInt16(group, offset + 12)), 3);
                    if (bytes.Length != BitConverter.ToUInt32(group, offset + 8)) throw new InvalidOperationException("Icon image length differs from its group.");
                    images.Add(bytes);
                }
                groups.Add(images.ToArray()); return true;
            } catch (Exception error) { failure = error; return false; }
        };
        try {
            if (!EnumResourceNames(module, new IntPtr(14), callback, IntPtr.Zero) && failure == null) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (failure != null) throw failure;
            return groups.ToArray();
        } finally { FreeLibrary(module); GC.KeepAlive(callback); }
    }
}
'@
}

function Test-NativeIconResources {
    param([Parameter(Mandatory)][string]$BinaryPath, [Parameter(Mandatory)][string]$SourceIcon)
    Add-Type -AssemblyName PresentationCore, WindowsBase
    $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $SourceIcon).Path)
    if ($bytes.Length -lt 6 -or [BitConverter]::ToUInt16($bytes, 0) -ne 0 -or [BitConverter]::ToUInt16($bytes, 2) -ne 1) { throw 'Invalid authored Windows icon.' }
    $count = [BitConverter]::ToUInt16($bytes, 4)
    $images = @()
    for ($index = 0; $index -lt $count; $index++) {
        $entry = 6 + $index * 16
        $size = if ($bytes[$entry] -eq 0) { 256 } else { [int]$bytes[$entry] }
        $height = if ($bytes[$entry + 1] -eq 0) { 256 } else { [int]$bytes[$entry + 1] }
        $length = [BitConverter]::ToUInt32($bytes, $entry + 8); $offset = [BitConverter]::ToUInt32($bytes, $entry + 12)
        if ($size -ne $height -or $offset + $length -gt $bytes.Length -or $length -gt 4MB) { throw 'Invalid authored icon frame.' }
        $frame = [byte[]]::new($length); [Array]::Copy($bytes, $offset, $frame, 0, $length)
        $stream = [IO.MemoryStream]::new($frame)
        try {
            $decoded = [System.Windows.Media.Imaging.PngBitmapDecoder]::new($stream, [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat, [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad).Frames[0]
            if ($decoded.PixelWidth -ne $size -or $decoded.PixelHeight -ne $size) { throw 'The authored icon frame has incorrect pixel dimensions.' }
            $bitmap = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new($decoded, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
            $pixels = [byte[]]::new($size * $size * 4); $bitmap.CopyPixels($pixels, $size * 4, 0)
            foreach ($corner in @(0, ($size - 1), ($size * ($size - 1)), ($size * $size - 1))) {
                if ($pixels[$corner * 4 + 3] -ne 0) { throw 'The authored icon has an opaque corner instead of correct transparency.' }
            }
            if ($pixels[([int]($size / 2) * $size + [int]($size / 2)) * 4 + 3] -ne 255) { throw 'The authored icon has no opaque central artwork.' }
        } finally { $stream.Dispose() }
        $images += [ordered]@{ size=$size; transparentCorners=$true; opaqueArtwork=$true; sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($frame)).ToLowerInvariant() }
    }
    if (@($images.size | Sort-Object) -join ',' -ne '16,24,32,48,64,128,256') { throw 'The authored icon is missing a required resolution.' }
    $groups = [LensNativeIconResources]::Groups((Resolve-Path -LiteralPath $BinaryPath).Path)
    $matched = $false
    foreach ($group in $groups) {
        $hashes = @($group | ForEach-Object { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($_)).ToLowerInvariant() })
        if (@($images | Where-Object { $_.sha256 -notin $hashes }).Count -eq 0) { $matched = $true; break }
    }
    if (-not $matched) { throw "Packaged PE resources do not contain every authored Lens icon frame: $BinaryPath" }
    [ordered]@{ status='PASS'; binary=[IO.Path]::GetFileName($BinaryPath); sourceSha256=(Get-FileHash -LiteralPath $SourceIcon -Algorithm SHA256).Hash.ToLowerInvariant(); frames=$images; executionPerformed=$false }
}
