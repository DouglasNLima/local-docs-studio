param([Parameter(Mandatory)][string]$Source)
$ErrorActionPreference = 'Stop'
$Source = (Resolve-Path -LiteralPath $Source).Path
function Edit([string]$Path, [string]$Before, [string]$After, [int]$Count = 1) {
    $target = Join-Path $Source $Path
    $content = [IO.File]::ReadAllText($target).Replace("`r`n", "`n")
    $actual = ([regex]::Matches($content, [regex]::Escape($Before))).Count
    if ($actual -ne $Count) { throw "Unexpected diagnostic patch anchor count in ${Path}: expected $Count, observed $actual" }
    [IO.File]::WriteAllText($target, $content.Replace($Before, $After), [Text.UTF8Encoding]::new($false))
}
$native = 'src/native/LensDocsStudio.Native'
$core = 'src/native/LensDocsStudio.Core'
$validation = 'src/native/LensDocsStudio.Native.Validation'
$tests = 'src/native/LensDocsStudio.Core.Tests'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot SaveTimeline.cs) -Destination (Join-Path $Source "$core/SaveTimeline.cs")
foreach ($file in @('NativeSaveProbe.cs', 'DocumentSaveProbe.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $Source "$native/$file") }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot CreationSaveProbe.cs) -Destination (Join-Path $Source "$validation/CreationSaveProbe.cs")
Copy-Item -LiteralPath (Join-Path $PSScriptRoot SyntheticSaveRace.cs) -Destination (Join-Path $Source "$validation/SyntheticSaveRace.cs")
Copy-Item -LiteralPath (Join-Path $PSScriptRoot RenderingProbe.cs) -Destination (Join-Path $Source "$tests/RenderingProbe.cs")
$timeline = 'LensDocsStudio.Core.Diagnostics.SaveTimeline'
Edit "$validation/Program.cs" "private static int Main(string[] args)`n    {" "private static int Main(string[] args)`n    {`n        if (args.FirstOrDefault() == `"--goal56-save-race`") return Task.Run(() => SyntheticSaveRace.Run(args[1], args[2])).GetAwaiter().GetResult();"
Edit "$native/ViewModels/WorkspaceViewModel.cs" "public async Task<bool> SaveTabAsync(DocumentViewModel tab, bool saveAs)`n    {" "public async Task<bool> SaveTabAsync(DocumentViewModel tab, bool saveAs)`n    {`n        using var diagnosticSave = $timeline.Begin(`"save`");"
Edit "$native/ViewModels/WorkspaceViewModel.cs" 'var saved = await documents.SaveAsync(path, transfer.Text, expected, OperationToken);' "$timeline.Event(`"disk.save.started`");`n            var saved = await documents.SaveAsync(path, transfer.Text, expected, OperationToken);`n            $timeline.Event(`"disk.save.completed`");"
Edit "$native/ViewModels/WorkspaceViewModel.cs" 'finally { tab.IsSaving = false; }' "finally { tab.IsSaving = false; $timeline.Event(`"save.finally.completed`"); }" 2
Edit "$native/ViewModels/WorkspaceViewModel.cs" "public async Task PersistSessionAsync()`n    {" "public async Task PersistSessionAsync()`n    {`n        using var diagnosticSession = $timeline.Begin(`"session.persistence`");"
Edit "$native/ViewModels/WorkspaceViewModel.cs" "public async Task UpdatePreferencesAsync(Func<UserPreferences, UserPreferences> update)`n    {" "public async Task UpdatePreferencesAsync(Func<UserPreferences, UserPreferences> update)`n    {`n        using var diagnosticPreferences = $timeline.Begin(`"preferences.persistence`");"
Edit "$core/State/SessionStore.cs" "        ValidateName(name);`n        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);`n        try`n        {" "        ValidateName(name);`n        using var diagnosticWrite = $timeline.Begin(`"state.write:`" + name);`n        await $timeline.PersistenceBarrierAsync(name);`n        $timeline.Event(`"state.gate.waiting`", new { name });`n        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);`n        $timeline.Event(`"state.gate.acquired`", new { name });`n        try`n        {"
Edit "$native/MainWindow.xaml.cs" 'InitializeComponent(); DataContext = model;' 'InitializeComponent(); DataContext = model; InitialiseSaveProbe();'
Edit "$native/ViewModels/DocumentViewModel.cs" "public DocumentViewModel(string text, DocumentSnapshot? snapshot = null)`n    {" "public DocumentViewModel(string text, DocumentSnapshot? snapshot = null)`n    {`n        PropertyChanged += (_, e) => $timeline.Event(`"document.property-notified`", new { e.PropertyName, IsSaving });"
Edit "$native/Controls/MarkdownEditor.cs" 'protected override void SetFocusCore() => owner.TextArea.Focus();' @"
private object OwnerState() => new { AutomationId = System.Windows.Automation.AutomationProperties.GetAutomationId(owner),
            EditorIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(owner), DocumentIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(owner.Document),
            ReadOnly = owner.IsReadOnly, Enabled = owner.IsEnabled, FocusWithin = owner.IsKeyboardFocusWithin, Barrier = $timeline.BarrierState, Main = $timeline.NativeState?.Invoke() };
        protected override void SetFocusCore() { $timeline.Event("provider.focus.requested", OwnerState()); owner.TextArea.Focus(); $timeline.Event("provider.focus.completed", OwnerState()); }
        protected override string GetItemStatusCore() => System.Text.Json.JsonSerializer.Serialize(OwnerState());
        public override object GetPattern(PatternInterface pattern)
        {
            var original = base.GetPattern(pattern);
            return pattern == PatternInterface.Value && original is System.Windows.Automation.Provider.IValueProvider value ? new ObservedValue(value, OwnerState) : original;
        }
        private sealed class ObservedValue(System.Windows.Automation.Provider.IValueProvider inner, Func<object> state) : System.Windows.Automation.Provider.IValueProvider
        {
            public string Value => inner.Value;
            public bool IsReadOnly { get { var value = inner.IsReadOnly; $timeline.Event("provider.read-only.observed", new { Value = value, State = state() }); return value; } }
            public void SetValue(string value)
            {
                $timeline.Event("provider.set-value.entered", state());
                try { inner.SetValue(value); $timeline.Event("provider.set-value.completed", state()); }
                catch (Exception error) { $timeline.Event("provider.set-value.failed", new { Error = $timeline.Error(error), State = state() }); $timeline.Flush(); throw; }
            }
        }
"@
$creation = Join-Path $Source "$validation/MermaidCreationUiChecks.cs"
$content = [IO.File]::ReadAllText($creation)
if (([regex]::Matches($content, 'owner\.Patterns\.Value\.Pattern\.SetValue\(')).Count -ne 7) { throw 'The original seven SetValue sites changed.' }
$content = [regex]::Replace($content, 'owner\.Patterns\.Value\.Pattern\.SetValue\(([^;]+)\);', 'CreationSaveProbe.SetValue(owner, $1, main!, automation, output);')
$content = $content.Replace('Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);', 'CreationSaveProbe.BeforeSave(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);')
[IO.File]::WriteAllText($creation, $content)
Edit "$core/Rendering/DiagramService.cs" 'var deadline = RenderingBudget(source);' "using var diagnosticRender = $timeline.Begin(`"render`");`n        $timeline.Event(`"render.source`", new { Source = source, CPU = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds, Threads = System.Threading.ThreadPool.ThreadCount, Pending = System.Threading.ThreadPool.PendingWorkItemCount, GC0 = GC.CollectionCount(0), GC2 = GC.CollectionCount(2) });`n        var deadline = RenderingBudget(source);"
Edit "$core/Rendering/DiagramService.cs" 'catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)' 'catch (OperationCanceledException diagnosticError) when (!cancellationToken.IsCancellationRequested)'
Edit "$core/Rendering/DiagramService.cs" 'return new(source, null, $"Rendering resource deadline reached' "$timeline.Event(`"render.deadline`", $timeline.Error(diagnosticError));`n            return new(source, null, $`"Rendering resource deadline reached"
Edit "$core/Workspace/DocumentStore.cs" 'target.Flush(true);' "target.Flush(true); $timeline.Event(`"disk.target.flushed`");" 2
foreach ($case in @(@('RequirementSourceAuthoringTests.cs', 'Atomic_symbolic_rename_updates_both_arrow_forms_without_touching_properties_or_comments'), @('ClassVisualTests.cs', 'Rename_updates_all_known_references_and_retains_namespace_notes_generic_and_annotation'))) {
    Edit "$tests/$($case[0])" "public async Task $($case[1])()`n    {" "public async Task $($case[1])()`n    {`n        using var diagnosticCase = $timeline.Begin(`"test:$($case[1])`");"
}
