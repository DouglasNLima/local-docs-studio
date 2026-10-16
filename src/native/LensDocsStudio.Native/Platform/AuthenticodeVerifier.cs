using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using LensDocsStudio.Core.Updates;

namespace LensDocsStudio.Native.Platform;

public sealed class AuthenticodeVerifier : IInstallerTrustVerifier
{
    public Task VerifyAsync(string path, IReadOnlyList<PublisherIdentity> publishers, CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (publishers.Count == 0) throw new IOException("No trusted update publisher is configured.");
        var info = new WinTrustFileInfo { Size = (uint)Marshal.SizeOf<WinTrustFileInfo>(), FilePath = Path.GetFullPath(path) };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        Marshal.StructureToPtr(info, pointer, false);
        var data = new WinTrustData { Size = (uint)Marshal.SizeOf<WinTrustData>(), UiChoice = 2, RevocationChecks = 1,
            UnionChoice = 1, FileInfo = pointer, StateAction = 1, ProviderFlags = 0x80 | 0x2000 };
        var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        try
        {
            var result = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            if (result != 0) throw new IOException($"Windows rejected the installer signature (0x{result:X8}). The installer will not run.");
            var provider = WTHelperProvDataFromStateData(data.StateData);
            var signer = provider == IntPtr.Zero ? IntPtr.Zero : WTHelperGetProvSignerFromChain(provider, 0, false, 0);
            var certPointer = signer == IntPtr.Zero ? IntPtr.Zero : WTHelperGetProvCertFromChain(signer, 0);
            if (certPointer == IntPtr.Zero) throw new IOException("Windows did not return the verified installer signer.");
            var providerCert = Marshal.PtrToStructure<ProviderCertificate>(certPointer);
            if (providerCert.Certificate == IntPtr.Zero) throw new IOException("The verified signer has no certificate.");
            var context = Marshal.PtrToStructure<CertificateContext>(providerCert.Certificate);
            if (context.Encoded == IntPtr.Zero || context.Length is 0 or > 1048576) throw new IOException("The verified signer certificate is invalid.");
            var encoded = new byte[context.Length]; Marshal.Copy(context.Encoded, encoded, 0, encoded.Length);
            using var certificate = X509CertificateLoader.LoadCertificate(encoded);
            var fingerprint = Convert.ToHexString(SHA256.HashData(certificate.RawData));
            if (!publishers.Any(p => p.Subject == certificate.Subject && p.CertificateSha256.Equals(fingerprint, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("The installer publisher does not match the pinned native update identity.");
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            data.StateAction = 2;
            _ = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            Marshal.DestroyStructure<WinTrustFileInfo>(pointer); Marshal.FreeHGlobal(pointer);
        }
    }, cancellationToken);

    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref WinTrustData data);
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern IntPtr WTHelperProvDataFromStateData(IntPtr stateData);
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr providerData, uint index, [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterIndex);
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer, uint index);
    // Only the documented initial fields are read; the remaining provider fields stay owned by WinVerifyTrust.
    [StructLayout(LayoutKind.Sequential)] private struct ProviderCertificate { public uint Size; public IntPtr Certificate; }
    [StructLayout(LayoutKind.Sequential)] private struct CertificateContext { public uint Encoding; public IntPtr Encoded; public uint Length; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint Size;
        public IntPtr PolicyCallbackData, SipClientData;
        public uint UiChoice, RevocationChecks, UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData, UrlReference;
        public uint ProviderFlags, UiContext;
        public IntPtr SignatureSettings;
    }
}
