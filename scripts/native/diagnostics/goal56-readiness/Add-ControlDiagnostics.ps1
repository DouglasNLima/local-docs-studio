param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$DiagnosticProtocol)
$ErrorActionPreference = 'Stop'
$Source = (Resolve-Path -LiteralPath $Source).Path
function Edit([string]$Path, [string]$Before, [string]$After, [int]$Count = 1) {
    $target = Join-Path $Source $Path
    $content = [IO.File]::ReadAllText($target).Replace("`r`n", "`n")
    if (([regex]::Matches($content, [regex]::Escape($Before))).Count -ne $Count) { throw "Unexpected control patch anchor: $Path" }
    [IO.File]::WriteAllText($target, $content.Replace($Before, $After), [Text.UTF8Encoding]::new($false))
}
$native = 'src/native/LensDocsStudio.Native'
$core = 'src/native/LensDocsStudio.Core'
$validation = 'src/native/LensDocsStudio.Native.Validation'
$retained = Join-Path $DiagnosticProtocol scripts/native/diagnostics/goal56-save-uia
Copy-Item -LiteralPath (Join-Path $retained SaveTimeline.cs) -Destination (Join-Path $Source "$core/SaveTimeline.cs")
Copy-Item -LiteralPath (Join-Path $retained NativeSaveProbe.cs) -Destination (Join-Path $Source "$native/NativeSaveProbe.cs")
Copy-Item -LiteralPath (Join-Path $PSScriptRoot ReadinessControls.cs) -Destination (Join-Path $Source "$validation/ReadinessControls.cs")
$timeline = 'LensDocsStudio.Core.Diagnostics.SaveTimeline'
# Only the disposable diagnostic barrier outlives readiness. The acceptance bound stays 20 s.
Edit "$core/SaveTimeline.cs" 'release.WaitOne(TimeSpan.FromSeconds(10))' 'release.WaitOne(TimeSpan.FromSeconds(30))'
Edit "$validation/Program.cs" "private static int Main(string[] args)`n    {" "private static int Main(string[] args)`n    {`n        if (args.FirstOrDefault() == `"--goal56-save-readiness`") return Task.Run(() => ReadinessControls.Run(args[1], args[2], args[3])).GetAwaiter().GetResult();"
Edit "$native/ViewModels/WorkspaceViewModel.cs" "public async Task<bool> SaveTabAsync(DocumentViewModel tab, bool saveAs)`n    {" "public async Task<bool> SaveTabAsync(DocumentViewModel tab, bool saveAs)`n    {`n        using var diagnosticSave = $timeline.Begin(`"save`");"
Edit "$native/ViewModels/WorkspaceViewModel.cs" 'var saved = await documents.SaveAsync(path, transfer.Text, expected, OperationToken);' "$timeline.Event(`"disk.save.started`");`n            var saved = await documents.SaveAsync(path, transfer.Text, expected, OperationToken);`n            $timeline.Event(`"disk.save.completed`");"
Edit "$native/ViewModels/WorkspaceViewModel.cs" 'finally { tab.IsSaving = false; }' "finally { tab.IsSaving = false; $timeline.Event(`"save.finally.completed`"); }" 2
Edit "$core/State/SessionStore.cs" "        ValidateName(name);`n        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);`n        try`n        {" "        ValidateName(name);`n        using var diagnosticWrite = $timeline.Begin(`"state.write:`" + name);`n        await $timeline.PersistenceBarrierAsync(name);`n        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);`n        try`n        {"
Edit "$native/MainWindow.xaml.cs" 'InitializeComponent(); DataContext = model;' 'InitializeComponent(); DataContext = model; InitialiseSaveProbe();'
Edit "$native/Controls/MarkdownEditor.cs" 'protected override void SetFocusCore() => owner.TextArea.Focus();' @"
private int observedSetValueCalls;
        private object OwnerState() => new { EditorIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(owner),
            DocumentIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(owner.Document), ReadOnly = owner.IsReadOnly,
            SetValueCalls = observedSetValueCalls, Barrier = $timeline.BarrierState, Main = $timeline.NativeState?.Invoke() };
        protected override string GetItemStatusCore() => System.Text.Json.JsonSerializer.Serialize(OwnerState());
        protected override void SetFocusCore() => owner.TextArea.Focus();
        public override object GetPattern(PatternInterface pattern)
        {
            var original = base.GetPattern(pattern);
            return pattern == PatternInterface.Value && original is System.Windows.Automation.Provider.IValueProvider value ? new ObservedValue(value, this) : original;
        }
        private sealed class ObservedValue(System.Windows.Automation.Provider.IValueProvider inner, MarkdownEditorAutomationPeer peer) : System.Windows.Automation.Provider.IValueProvider
        {
            public string Value => inner.Value;
            public bool IsReadOnly => inner.IsReadOnly;
            public void SetValue(string value)
            {
                peer.observedSetValueCalls++; $timeline.Event("provider.set-value.entered", peer.OwnerState());
                inner.SetValue(value); $timeline.Event("provider.set-value.completed", peer.OwnerState());
            }
        }
"@
