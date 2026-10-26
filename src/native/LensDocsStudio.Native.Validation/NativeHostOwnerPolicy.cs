using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

// Validation-only snapshots. No policy branch activates a window or performs input.
internal sealed record HostOwnerSnapshot
{
    public DateTimeOffset AtUtc { get; init; }
    public int Sequence { get; init; }
    public int ProcessId { get; init; }
    public long ProcessStartTicks { get; init; }
    public long MainHwnd { get; init; }
    public long VisualHwnd { get; init; }
    public int MainPid { get; init; }
    public int VisualPid { get; init; }
    public long MainOwner { get; init; }
    public long VisualOwner { get; init; }
    public long Foreground { get; init; }
    public int VisibleWindows { get; init; }
    public bool MainEnabled { get; init; }
    public bool VisualEnabled { get; init; }
    public bool NormalConsole { get; init; }
    public bool ExactEditorAndOwner { get; init; }
    public bool ReadyPrivateDraft { get; init; }
    public int MainIdentity { get; init; }
    public int VisualIdentity { get; init; }
    public int DocumentIdentity { get; init; }
    public int SessionIdentity { get; init; }
    public int OwnerVersion { get; init; }
    public int PrivateVersion { get; init; }
    public int CapturedVersionAge { get; init; }
    public long OwnerGeneration { get; init; }
    public string SourceSha256 { get; init; } = "";
    public string PrivateSha256 { get; init; } = "";
    public string SavedSha256 { get; init; } = "";
}

internal static class NativeHostOwnerPolicy
{
    public static void Boundary(HostOwnerSnapshot value)
    {
        if (value.ProcessId <= 0 || value.ProcessStartTicks <= 0 || value.MainHwnd == 0 || value.VisualHwnd == 0
            || value.MainHwnd == value.VisualHwnd || value.MainPid != value.ProcessId || value.VisualPid != value.ProcessId
            || value.MainOwner != 0 || value.VisualOwner != value.MainHwnd || value.Foreground != value.VisualHwnd
            || value.VisibleWindows != 2 || !value.MainEnabled || !value.VisualEnabled || !value.NormalConsole
            || !value.ExactEditorAndOwner || !value.ReadyPrivateDraft || value.MainIdentity <= 0 || value.VisualIdentity <= 0
            || value.DocumentIdentity <= 0 || value.SessionIdentity <= 0 || value.OwnerVersion <= 0 || value.PrivateVersion <= 0
            || value.SourceSha256.Length != 64 || value.PrivateSha256.Length != 64 || value.SavedSha256.Length != 64)
            throw new IOException("Synthetic owner mutation requires the exact live nonmodal owner/visual/editor/session and normal foreground console boundary.");
    }

    public static void BeforeMutation(HostOwnerSnapshot expected, HostOwnerSnapshot current)
    {
        Boundary(expected); Boundary(current);
        if (expected.CapturedVersionAge != 0 || current.CapturedVersionAge != 0
            || current.Sequence <= expected.Sequence || current.AtUtc < expected.AtUtc
            || current.AtUtc > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow - current.AtUtc > TimeSpan.FromSeconds(2)
            || expected with { AtUtc = current.AtUtc, Sequence = current.Sequence } != current)
            throw new IOException("Owner, private draft, captured native version, process or window identity changed before the single synthetic mutation.");
    }

    public static void AfterMutation(HostOwnerSnapshot before, HostOwnerSnapshot after, string externalHash)
    {
        Boundary(after);
        if (after.CapturedVersionAge == 0 || after.OwnerVersion == before.OwnerVersion || after.SourceSha256 != externalHash
            || before with { AtUtc = after.AtUtc, Sequence = after.Sequence, OwnerVersion = after.OwnerVersion,
                CapturedVersionAge = after.CapturedVersionAge, SourceSha256 = externalHash } != after)
            throw new IOException("The bounded owner mutation changed identities, private draft or saved bytes, or did not change the native owner version.");
    }

    public static void RunControls(string output)
    {
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Preserve existing owner-control evidence.");
        Directory.CreateDirectory(output);
        var now = DateTimeOffset.UtcNow;
        var baseline = new HostOwnerSnapshot { AtUtc = now, Sequence = 1, ProcessId = 10, ProcessStartTicks = 100,
            MainHwnd = 20, VisualHwnd = 30, MainPid = 10, VisualPid = 10, VisualOwner = 20, Foreground = 30,
            VisibleWindows = 2, MainEnabled = true, VisualEnabled = true, NormalConsole = true, ExactEditorAndOwner = true,
            ReadyPrivateDraft = true, MainIdentity = 1, VisualIdentity = 2, DocumentIdentity = 3, SessionIdentity = 4,
            OwnerVersion = 5, PrivateVersion = 6, SourceSha256 = new('a', 64), PrivateSha256 = new('b', 64), SavedSha256 = new('a', 64) };
        var current = baseline with { Sequence = 2 };
        BeforeMutation(baseline, current);
        var cases = new (string Name, HostOwnerSnapshot Value)[] {
            ("Wrong process", current with { ProcessId = 11 }), ("Reused process identity", current with { ProcessStartTicks = 101 }),
            ("Wrong owner HWND", current with { MainHwnd = 21 }), ("Wrong visual HWND", current with { VisualHwnd = 31 }),
            ("Foreign main process", current with { MainPid = 11 }), ("Foreign visual process", current with { VisualPid = 11 }),
            ("Ambiguous owner", current with { MainOwner = 99 }), ("Wrong ownership chain", current with { VisualOwner = 99 }),
            ("Modal present", current with { VisibleWindows = 3, VisualEnabled = false }),
            ("Owner disabled", current with { MainEnabled = false }), ("Visual disabled", current with { VisualEnabled = false }),
            ("Foreign foreground", current with { Foreground = 99 }), ("Owner foreground is not this boundary", current with { Foreground = 20 }),
            ("Unavailable normal console", current with { NormalConsole = false }), ("Wrong explicit editor", current with { ExactEditorAndOwner = false }),
            ("Unvalidated draft", current with { ReadyPrivateDraft = false }), ("Reused main identity", current with { MainIdentity = 11 }),
            ("Reused visual identity", current with { VisualIdentity = 12 }), ("Changed document", current with { DocumentIdentity = 13 }),
            ("Changed session", current with { SessionIdentity = 14 }), ("Changed owner version with identical bytes", current with { OwnerVersion = 15 }),
            ("Changed private version with identical bytes", current with { PrivateVersion = 16 }),
            ("Changed captured version", current with { CapturedVersionAge = -1 }), ("Changed active owner generation", current with { OwnerGeneration = 1 }),
            ("Changed owner bytes", current with { SourceSha256 = new('c', 64) }), ("Changed private bytes", current with { PrivateSha256 = new('c', 64) }),
            ("Changed saved bytes", current with { SavedSha256 = new('c', 64) }), ("Replayed snapshot", baseline),
            ("Expired observation", current with { AtUtc = now.AddSeconds(-3) }), ("Future observation", current with { AtUtc = now.AddMinutes(1) })
        };
        var checks = new List<string> { "Exact unchanged nonmodal boundary accepted before a mutation" };
        foreach (var test in cases)
        {
            var mutationReached = false;
            try { BeforeMutation(baseline, test.Value); mutationReached = true; }
            catch (IOException) { }
            if (mutationReached) throw new IOException("Negative owner control accepted: " + test.Name);
            checks.Add(test.Name + " rejected before mutation");
        }
        var changed = current with { OwnerVersion = 7, CapturedVersionAge = -1, SourceSha256 = new('c', 64) };
        AfterMutation(baseline, changed, new('c', 64));
        checks.Add("Only the intended owner bytes/native version may change after mutation");
        foreach (var invalid in new[] { changed with { PrivateSha256 = new('d', 64) }, changed with { SavedSha256 = new('d', 64) },
            changed with { DocumentIdentity = 99 }, changed with { OwnerVersion = baseline.OwnerVersion }, changed with { CapturedVersionAge = 0 } })
        {
            var accepted = false;
            try { AfterMutation(baseline, invalid, new('c', 64)); accepted = true; } catch (IOException) { }
            if (accepted) throw new IOException("Post-mutation preservation control accepted a changed private/saved identity or unchanged native version.");
        }
        checks.Add("Five post-mutation controls reject private/saved/identity corruption and unchanged native versions");
        var document = new ICSharpCode.AvalonEdit.Document.TextDocument("Synthetic owner");
        var captured = document.Version;
        if (captured.CompareAge(document.Version) != 0) throw new IOException("Unchanged native version comparison differs.");
        document.Text = "Changed synthetic owner"; document.Text = "Synthetic owner";
        if (captured.CompareAge(document.Version) == 0) throw new IOException("Restored bytes concealed an intervening native version change.");
        checks.Add("Actual AvalonEdit version chain detects changes even after identical bytes are restored");
        File.WriteAllText(Path.Combine(output, "controls.json"), JsonSerializer.Serialize(new { status = "PASS", scope = "DETERMINISTIC_OWNER_POLICY_ONLY", guiAcceptance = "NOT RUN", checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
