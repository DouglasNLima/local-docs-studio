using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Core.Exports;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() is "--arrangement-latency" or "--arrangement-coordinates" or "--arrangement-observe")
        {
            try
            {
                if (args[0] == "--arrangement-latency") NativeArrangementLatencyChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
                else if (args[0] == "--arrangement-coordinates") NativeArrangementCoordinateChecks.Run(Path.GetFullPath(args[1]));
                else NativeArrangementLatencyChecks.Observe(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--arrangement-remediation")
        {
            try { NativeArrangementRemediationChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() is "--arrangement" or "--arrangement-windows")
        {
            try { NativeArrangementChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), args.Contains("--high-contrast"), args[0] == "--arrangement-windows"); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--arrangement-input")
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
                || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            { Console.Error.WriteLine("Automated arrangement desktop input requires a disposable GitHub-hosted Windows runner."); return 1; }
            return NativeUiChecks.RunArrangementInput(args[1], args[2], args.Contains("--high-contrast"));
        }
        if (args.FirstOrDefault() == "--complex-er")
        {
            try { NativeComplexErChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() is "--preview-loading" or "--preview-loading-ui")
        {
            try { NativePreviewLoadingChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), args[0] == "--preview-loading-ui", args.Contains("--high-contrast")); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--desktop-ux-interactions")
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
                || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            { Console.Error.WriteLine("Automated desktop UX input and High Contrast acceptance require a disposable GitHub-hosted Windows runner."); return 1; }
            return NativeUiChecks.RunDesktopUxInteractions(args[1], args[2], args[3], args.Contains("--high-contrast"));
        }
        if (args.FirstOrDefault() == "--desktop-ux")
        {
            try { NativeDesktopUxChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--host-owned-window-controls")
        {
            try { NativeHostOwnerPolicy.RunControls(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--find-routing")
        {
            try { NativeFindRoutingChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--host-mermaid-fixtures")
        {
            try { NativeHostMermaidFixtureChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--window-discovery")
        {
            try
            {
                if (args.Length != 2) throw new ArgumentException("A fresh unit-evidence directory is required.");
                var discoveryOutput = Path.GetFullPath(args[1]);
                if (Directory.Exists(discoveryOutput) || File.Exists(discoveryOutput)) throw new IOException("Unit evidence already exists; preserve it.");
                Directory.CreateDirectory(discoveryOutput); NativeWindowDiscoveryChecks.Run(discoveryOutput); return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--development-host-ui")
        {
            try
            {
                if (args.Length is not (5 or 6) || args.Length == 6 && args[5] is not ("--shell-only" or "--diagnostic-FindEnter" or "--diagnostic-FindNext" or "--diagnostic-FindTyped" or "--diagnostic-FindEnterObserved" or "--diagnostic-FindNextObserved" or "--diagnostic-FindTypedObserved" or "--diagnostic-FindEditorEnterObserved" or "--diagnostic-FindPreviewObserved" or "--diagnostic-InvalidMermaid" or "--diagnostic-OwnedWindow")) throw new ArgumentException("Use the guarded Invoke-NativePortableHostSmoke.ps1 RunUi path with independent source/tree/executable pins.");
                return NativeUiChecks.RunDevelopmentHost(args[1], args[2], args[3], args[4], args.Length == 6 && args[5] == "--shell-only", args.Length == 6 && args[5].StartsWith("--diagnostic-", StringComparison.Ordinal) ? args[5][13..] : null);
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--mermaid-creation-ui")
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
                || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            { Console.Error.WriteLine("Mermaid creation UI acceptance requires a disposable GitHub-hosted Windows runner."); return 1; }
            return NativeUiChecks.RunMermaidCreation(args[1], args[2], args.Contains("--high-contrast"), args.Contains("--dark"));
        }
        if (args.FirstOrDefault() == "--mermaid-creation-sessions")
        {
            try { MermaidCreationSessionChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--expansion-vi-editor-fidelity")
        {
            try { foreach (var family in new[] { "packet", "treeview", "treeview-box", "treemap", "venn" }) NativeWorkflowEditorChecks.Run(Path.Combine(Path.GetFullPath(args[1]), family), Path.GetFullPath(args[2]), expansionFamily: family); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--venn-editor-fidelity")
        {
            try { NativeWorkflowEditorChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), expansionFamily: "venn"); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--treemap-editor-fidelity")
        {
            try { NativeWorkflowEditorChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), expansionFamily: "treemap"); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--treeview-editor-fidelity")
        {
            try { NativeWorkflowEditorChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), expansionFamily: "treeview"); NativeWorkflowEditorChecks.Run(Path.Combine(Path.GetFullPath(args[1]), "box-mode"), Path.GetFullPath(args[2]), expansionFamily: "treeview-box"); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--packet-editor-fidelity")
        {
            try { NativeWorkflowEditorChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), expansionFamily: "packet"); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--expansion-vi-compatibility")
        {
            try { NativeExpansionViCompatibilityChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), args.Contains("--baseline")); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--topology-editor-fidelity")
        {
            try { NativeWorkflowEditorChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), true); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--topology-compatibility")
        {
            try { NativeTopologyCompatibilityChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--workflow-editor-fidelity")
        {
            try { NativeWorkflowEditorChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--workflow-compatibility")
        {
            try { NativeWorkflowCompatibilityChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--chart-compatibility")
        {
            try { NativeChartCompatibilityChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--chart-editor-fidelity")
        {
            try { NativeChartEditorChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--quadrant-compatibility")
        {
            try { NativeQuadrantCompatibilityChecks.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--source-editor-fidelity")
        {
            try { NativeSourceEditorChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--continuous-interactions")
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
                || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            { Console.Error.WriteLine("Visible Continuous interaction checks require a disposable GitHub-hosted Windows runner."); return 1; }
            try { NativePreviewSchedulingChecks.Run(Path.GetFullPath(args[1]), true, args.Contains("--high-contrast")); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--watch-preview-stalls") return PreviewStallWatch.RunHelper(args);
        if (args.FirstOrDefault() is "--suite-performance" or "--suite-uncached-performance")
        {
            try { NativeSuitePerformanceChecks.Run(Path.GetFullPath(args[1]), args[0] == "--suite-uncached-performance"); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--suite-cache")
        {
            try { NativeSuiteCacheChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--suite-phases")
        {
            try { NativeSuitePhaseChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() is "--suite-lifecycle" or "--mermaid-fidelity" or "--sequence-fidelity" or "--state-fidelity" or "--er-fidelity" or "--class-fidelity")
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
                || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            { Console.Error.WriteLine("Native viewport certification requires a disposable GitHub-hosted Windows runner."); return 1; }
            try { if (args[0] == "--suite-lifecycle") NativeSuiteLifecycleChecks.Run(Path.GetFullPath(args[1]), args.Contains("--high-contrast")); else if (args[0] == "--class-fidelity") NativeClassChecks.Run(Path.GetFullPath(args[1]), args.Contains("--high-contrast")); else if (args[0] == "--er-fidelity") NativeErChecks.Run(Path.GetFullPath(args[1]), args.Contains("--high-contrast")); else if (args[0] == "--state-fidelity") NativeStateChecks.Run(Path.GetFullPath(args[1]), args.Contains("--high-contrast")); else if (args[0] == "--sequence-fidelity") NativeSequenceChecks.Run(Path.GetFullPath(args[1])); else NativeMermaidFidelityChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--mermaid-performance")
        {
            try { NativeMermaidPerformanceChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--segmented-native-prototype")
        {
            try { NativeContinuousPerformanceChecks.Run(Path.GetFullPath(args[1]), "all", repetitions: 2, segmentedPrototype: true); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--theme-resources")
        {
            try { NativeThemeResourceChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--viewport-anchors")
        {
            try { NativeContinuousPerformanceChecks.Run(Path.GetFullPath(args[1]), "all", checkAnchors: true); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() is "--continuous-performance" or "--continuous-acceptance")
        {
            try
            {
                var repeatAt = Array.IndexOf(args, "--repeat");
                NativeContinuousPerformanceChecks.Run(Path.GetFullPath(args.ElementAtOrDefault(1) ?? "acceptance/performance/continuous"),
                    args.ElementAtOrDefault(2), args[0] == "--continuous-acceptance",
                    repeatAt < 0 ? 1 : int.Parse(args[repeatAt + 1], System.Globalization.CultureInfo.InvariantCulture), args.Contains("--edited"));
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--literal-formatting")
        {
            var formattingOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "acceptance/literal-formatting"); Directory.CreateDirectory(formattingOutput);
            try { NativeLiteralFormattingChecks.Run(formattingOutput); Console.WriteLine("Native literal formatting checks passed."); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--selection-mapping")
        {
            try { NativeSelectionMappingChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--workspace-chrome")
        {
            try { NativeWorkspaceChromeChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--toolbar-layout")
        {
            try { NativeToolbarChecks.Run(Path.GetFullPath(args[1])); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--export-feedback")
        {
            try { var feedbackOutput = Path.GetFullPath(args[1]); Directory.CreateDirectory(feedbackOutput); NativeExportFeedbackChecks.Run(feedbackOutput); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--layout")
        {
            var layoutOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/layout"); Directory.CreateDirectory(layoutOutput);
            try { NativeLayoutChecks.Run(layoutOutput); Console.WriteLine("Native construction checks and continuous-layout observations completed."); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--acceptance-workflow")
        {
            var workflowOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/acceptance-workflow"); Directory.CreateDirectory(workflowOutput);
            try { NativeAcceptanceWorkflowChecks.Run(workflowOutput); Console.WriteLine("Native persisted-workspace and export acceptance checks passed."); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--check-preview-stall-watch")
        {
            var stallOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/stall-watch");
            try { PreviewStallWatchChecks.Run(stallOutput); Console.WriteLine("Native preview stall diagnostics passed."); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() is "--source-editor-ui" or "--suite-baseline-ui" or "--suite-ui" or "--outline-focus-holder" or "--first-launch-ui" or "--ui" or "--interrupted-save" or "--installer-wizard" or "--restore-installed-session" or "--seed-installed-drafts" or "--office" or "--preview-input-safety" or "--ux-screenshots" or "--ux-controls" or "--mermaid-ui" or "--mermaid-interactions" or "--workspace-viewports" or "--sequence-ui" or "--state-ui" or "--state-performance-ui" or "--er-ui" or "--er-performance-ui" or "--class-ui" or "--class-performance-ui")
        {
            // Prevent accidental desktop/clipboard changes on a developer's machine before reading arguments or creating files.
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
                || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
                || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
            {
                Console.Error.WriteLine("Desktop and Office validation require a disposable GitHub-hosted Windows runner.");
                return 1;
            }
        }
        if (args.FirstOrDefault() == "--suite-baseline-ui") return NativeUiChecks.RunMermaidSuite(args[1], args[2], args.Contains("--high-contrast"), true, args.Contains("--dark"));
        if (args.FirstOrDefault() == "--suite-ui") return NativeUiChecks.RunMermaidSuite(args[1], args[2], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--class-performance-ui") return NativeUiChecks.RunClassPerformance(args[1], args[2], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--source-editor-ui" && args[3] is "Packet" or "TreeView" or "Treemap" or "Venn") return NativeUiChecks.RunExpansionViEditor(args[1], args[2], args[3], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--source-editor-ui" && args[3] is "Kanban" or "Requirement" or "GitGraph" or "Architecture" or "C4" or "Block") return NativeUiChecks.RunWorkflowEditor(args[1], args[2], args[3], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--source-editor-ui" && args[3] is "Xy" or "Radar" or "Sankey") return NativeUiChecks.RunChartEditor(args[1], args[2], args[3], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--source-editor-ui") return args[3] is "Timeline" or "Journey" or "Quadrant"
            ? NativeUiChecks.RunExpansionEditor(args[1], args[2], args[3], args.Contains("--high-contrast"))
            : NativeUiChecks.RunSourceEditor(args[1], args[2], args[3], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--class-ui") return NativeUiChecks.RunClassEditor(args[1], args[2], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--sequence-ui") return NativeUiChecks.RunSequenceEditor(args[1], args[2], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--state-ui") return NativeUiChecks.RunStateEditor(args[1], args[2], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--er-performance-ui") return NativeUiChecks.RunErPerformance(args[1], args[2], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--er-ui") return NativeUiChecks.RunErEditor(args[1], args[2], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--state-performance-ui") return NativeUiChecks.RunStatePerformance(args[1], args[2], args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--ux-screenshots")
            return NativeUiChecks.CaptureUxScreenshots(args[1], args[2], args[3], args.ElementAtOrDefault(4) == "--high-contrast");
        if (args.FirstOrDefault() == "--ux-controls")
            return NativeUiChecks.Run(args[1], args[2], desktopUxOnly: true);
        if (args.FirstOrDefault() == "--mermaid-interactions")
            return NativeUiChecks.RunMermaidInteractions(args[1], args[2], args.Contains("--baseline"), args.Contains("--high-contrast"));
        if (args.FirstOrDefault() == "--workspace-viewports")
            return NativeUiChecks.RunWorkspaceViewports(args[1], args[2]);
        if (args.FirstOrDefault() == "--mermaid-ui")
            return NativeUiChecks.RunMermaidVisualEditor(args[1], args[2], args.Contains("--high-contrast"), args.Contains("--polish"), args.Contains("--baseline"));
        if (args.FirstOrDefault() == "--preview-input-safety")
        {
            try { NativePreviewInputSafetyChecks.Run(Path.GetFullPath(args[1]), args[2]); return 0; }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        if (args.FirstOrDefault() == "--outline-focus-holder") return NativeOutlineFocusProbe.Hold();
        if (args.FirstOrDefault() == "--first-launch-ui") return NativeUiChecks.Run(args[1], args[2], requireBundledRuntime: true, firstLaunchOnly: true);
        if (args.FirstOrDefault() == "--ui")
            return NativeUiChecks.Run(args[1], args.ElementAtOrDefault(2) ?? "artifacts/native/ui",
                requireBundledRuntime: args.ElementAtOrDefault(3) == "--require-bundled-runtime");
        if (args.FirstOrDefault() == "--interrupted-save")
            return NativeUiChecks.RunInterruptedSave(args[1], args.ElementAtOrDefault(2) ?? "artifacts/native/interrupted-save");
        if (args.FirstOrDefault() == "--installer-wizard")
            return NativeUiChecks.RunInstallerWizard(args[1], args.ElementAtOrDefault(2) ?? "artifacts/native/installer-wizard", args.Contains("--no-file-associations"));
        if (args.FirstOrDefault() == "--restore-installed-session")
            return NativeUiChecks.RunInstalledSession(args[1], args[2], args[3]);
        if (args.FirstOrDefault() == "--seed-installed-drafts")
            return NativeUiChecks.RunInstalledDrafts(args[1], args[2], args[3]);
        if (args.FirstOrDefault() == "--office")
            return OfficeClipboardChecks.Run(args.ElementAtOrDefault(1) ?? "artifacts/native/office");
        if (args.FirstOrDefault() == "--authenticode")
        {
            try { NativeAuthenticodeChecks.Run(args[1]); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--export-allocation")
        {
            var allocationOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(allocationOutput);
            try { NativeExportAllocationChecks.Run(allocationOutput); Console.WriteLine("Native export allocation checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--external-links")
        {
            var linkOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(linkOutput);
            try { NativeExternalLinkChecks.Run(linkOutput); Console.WriteLine("Native external link checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--pdf-lists")
        {
            var listOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(listOutput);
            try { NativePdfListChecks.Run(listOutput, Path.GetFullPath(args.ElementAtOrDefault(2) ?? ".")); Console.WriteLine("Native PDF list checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--pdf-syntax")
        {
            var syntaxOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(syntaxOutput);
            try { NativePdfSyntaxChecks.Run(syntaxOutput); Console.WriteLine("Native PDF syntax checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--word-lists")
        {
            var listOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(listOutput);
            try { NativeWordListChecks.Run(listOutput, Path.GetFullPath(args.ElementAtOrDefault(2) ?? ".")); Console.WriteLine("Native Word list checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--word-import-fixture")
        {
            var fixtureOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/word-import-fixture");
            try { var fixture = NativeWordImportFixtureChecks.Run(fixtureOutput); NativeWordImportPreviewChecks.Run(fixtureOutput, fixture); Console.WriteLine("Native Word import fixture passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--workspace-context")
        {
            var contextOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/workspace-context");
            try { NativeWorkspaceContextChecks.Run(contextOutput); Console.WriteLine("Native workspace context checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--window-discovery")
        {
            var discoveryOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/window-discovery");
            Directory.CreateDirectory(discoveryOutput);
            try { NativeWindowDiscoveryChecks.Run(discoveryOutput); Console.WriteLine("Native window discovery checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--observation-wait")
        {
            var observationOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/observation-wait");
            try { NativeObservationWaitChecks.Run(observationOutput); Console.WriteLine("Native observation wait checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--table-editor")
        {
            var tableOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/table-editor");
            try { NativeTableEditorChecks.Run(tableOutput); Console.WriteLine("Native table editor checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--word-syntax")
        {
            var syntaxOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(syntaxOutput);
            try { NativeWordSyntaxChecks.Run(syntaxOutput); Console.WriteLine("Native Word syntax checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--pdf-unicode")
        {
            var unicodeOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(unicodeOutput);
            try { NativePdfUnicodeChecks.Run(unicodeOutput); Console.WriteLine("Native PDF Unicode checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--pagination")
        {
            var paginationOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(paginationOutput);
            try { NativePaginationChecks.Run(paginationOutput); Console.WriteLine("Native pagination checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--large-blocks")
        {
            var blockOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/large-blocks"); Directory.CreateDirectory(blockOutput);
            try { NativePaginationChecks.RunLargeBlocks(blockOutput); Console.WriteLine("Native large-block checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--update-interactions")
        {
            var updateOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/update-interactions"); Directory.CreateDirectory(updateOutput);
            try { NativeUpdateInteractionChecks.Run(updateOutput); Console.WriteLine("Native update interaction checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--inline-layout")
        {
            var inlineOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/inline-layout"); Directory.CreateDirectory(inlineOutput);
            try { NativeInlineLayoutChecks.Run(inlineOutput); Console.WriteLine("Native inline layout checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--save-recovery")
        {
            var recoveryOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/save-recovery"); Directory.CreateDirectory(recoveryOutput);
            try { NativeSaveRecoveryChecks.Run(recoveryOutput); Console.WriteLine("Native save recovery checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--html-syntax")
        {
            var syntaxOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(syntaxOutput);
            try { NativeHtmlSyntaxChecks.Run(syntaxOutput); Console.WriteLine("Native HTML syntax checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--pdf-decorations")
        {
            var decorationOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/pdf-decorations"); Directory.CreateDirectory(decorationOutput);
            try { NativePdfDecorationChecks.Run(decorationOutput); Console.WriteLine("Native PDF decoration checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--diagram-paint")
        {
            var paintOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/diagram-paint"); Directory.CreateDirectory(paintOutput);
            try { NativeDiagramPaintChecks.Run(paintOutput); Console.WriteLine("Native diagram paint checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--diagram-failures")
        {
            var failureOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/diagram-failures"); Directory.CreateDirectory(failureOutput);
            try { NativeDiagramFailureChecks.Run(failureOutput); Console.WriteLine("Native diagram recovery checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--details-latency")
        {
            var detailsOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/details-latency"); Directory.CreateDirectory(detailsOutput);
            try { NativeDetailsLatencyChecks.Run(detailsOutput); Console.WriteLine("Native details latency checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--diagram-themes")
        {
            var themeOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/diagram-themes"); Directory.CreateDirectory(themeOutput);
            try { NativeDiagramThemeChecks.Run(themeOutput); Console.WriteLine("Native diagram theme checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--diagram-contrast")
        {
            var contrastOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(contrastOutput);
            try { NativeDiagramContrastChecks.Run(contrastOutput); Console.WriteLine("Native diagram contrast checks passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--docs-site")
        {
            var siteOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation");
            Directory.CreateDirectory(siteOutput);
            try { NativeDocsSiteChecks.Run(siteOutput); Console.WriteLine("Native Docs Site integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--progress")
        {
            var progressOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(progressOutput);
            try { NativeProgressChecks.Run(progressOutput); Console.WriteLine("Native progress integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--anchors")
        {
            var anchorOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(anchorOutput);
            try { NativeAnchorChecks.Run(anchorOutput); Console.WriteLine("Native anchor integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--context")
        {
            var contextOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(contextOutput);
            try { NativeContextChecks.Run(contextOutput); Console.WriteLine("Native context integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--table")
        {
            var tableOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(tableOutput);
            try { NativeTableChecks.Run(tableOutput); Console.WriteLine("Native table integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--figures")
        {
            var figureOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(figureOutput);
            try { NativeFigureChecks.Run(figureOutput); Console.WriteLine("Native figure integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--word-template")
        {
            var templateOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(templateOutput);
            try { NativeWordTemplateChecks.Run(templateOutput); Console.WriteLine("Native Word template integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--template-library")
        {
            var libraryOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(libraryOutput);
            try { NativeTemplateLibraryChecks.Run(libraryOutput); Console.WriteLine("Native template library integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--content-library")
        {
            var contentOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(contentOutput);
            try { NativeLibraryContentChecks.Run(contentOutput); NativeContentLibraryChecks.Run(contentOutput); Console.WriteLine("Native library content integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--catalogue")
        {
            var catalogueOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(catalogueOutput);
            try { NativeContentCatalogueChecks.Run(catalogueOutput); Console.WriteLine("Native content catalogue integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--details")
        {
            var detailsOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(detailsOutput);
            try { NativeDetailsChecks.Run(detailsOutput); Console.WriteLine("Native details integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--maths")
        {
            var mathsOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(mathsOutput);
            try { NativeMathChecks.Run(mathsOutput); Console.WriteLine("Native maths integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--artefacts")
        {
            var artifactOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(artifactOutput);
            try { NativeArtifactChecks.Run(artifactOutput, args.ElementAtOrDefault(2)); Console.WriteLine("Native artefact integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--folder-move")
        {
            var folderOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(folderOutput);
            try { NativeFolderMoveChecks.Run(folderOutput); Console.WriteLine("Native folder move review passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--workspace-map")
        {
            var mapOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(mapOutput);
            try { NativeWorkspaceMapChecks.Run(mapOutput); Console.WriteLine("Native workspace map integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--wikilinks")
        {
            var wikiOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(wikiOutput);
            try { NativeWikiLinkChecks.Run(wikiOutput); Console.WriteLine("Native wikilink integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--help-guide")
        {
            var helpOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(helpOutput);
            try { NativeHelpChecks.Run(helpOutput); Console.WriteLine("Native help integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--settings")
        {
            var settingsOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(settingsOutput);
            try { NativeSettingsChecks.Run(settingsOutput); Console.WriteLine("Native settings integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--layout")
        {
            var layoutOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(layoutOutput);
            try { NativeLayoutChecks.Run(layoutOutput); Console.WriteLine("Native layout integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--preview-navigation")
        {
            var navigationOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(navigationOutput);
            try { NativePreviewNavigationChecks.Run(navigationOutput); Console.WriteLine("Native preview navigation passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--preview-scheduling")
        {
            var schedulingOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(schedulingOutput);
            try { NativePreviewSchedulingChecks.Run(schedulingOutput); Console.WriteLine("Native preview scheduling passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--clipboard-apartments")
        {
            var apartmentOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(apartmentOutput);
            try { NativeClipboardApartmentChecks.Run(apartmentOutput); Console.WriteLine("Native clipboard apartment validation passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--shell-layout")
        {
            var shellOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(shellOutput);
            try { NativeShellLayoutChecks.Run(shellOutput); Console.WriteLine("Native shell layout passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--performance")
        {
            var performanceOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(performanceOutput);
            try { NativePerformanceChecks.Run(performanceOutput); Console.WriteLine("Native performance observations passed correctness checks."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--workspace")
        {
            var workspaceOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(workspaceOutput);
            try { NativeWorkspaceChecks.Run(workspaceOutput); Console.WriteLine("Native workspace monitoring passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--review")
        {
            var reviewOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(reviewOutput);
            try { NativeReviewChecks.Run(reviewOutput); Console.WriteLine("Native review integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--editor-colours")
        {
            var colourOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(colourOutput);
            try { NativeEditorColourChecks.Run(colourOutput); Console.WriteLine("Native editor colour integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--syntax")
        {
            var syntaxOutput = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/native/validation"); Directory.CreateDirectory(syntaxOutput);
            try { NativeSyntaxChecks.Run(syntaxOutput); Console.WriteLine("Native syntax integration passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.FirstOrDefault() == "--reject-unsigned")
        {
            try
            {
                new LensDocsStudio.Native.Platform.AuthenticodeVerifier().VerifyAsync(Path.GetFullPath(args[1]), [new("CN=Development test", new string('A', 64))], default).GetAwaiter().GetResult();
                Console.Error.WriteLine("Unsigned installer was incorrectly accepted."); return 1;
            }
            catch (IOException ex) when (ex.Message.StartsWith("Windows rejected the installer signature", StringComparison.Ordinal))
            { Console.WriteLine("Actual Windows Authenticode verification rejected the unsigned installer: " + ex.Message); return 0; }
        }
        return RunCorpus(args);
    }

    // Older-product comparisons share this compiled dispatcher. Keep new corpus-only APIs
    // outside Main's JIT boundary so unused checks do not require newer product methods.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int RunCorpus(string[] args)
    {
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/native/validation");
        var repository = Path.GetFullPath(args.ElementAtOrDefault(1) ?? ".");
        Directory.CreateDirectory(output);
        var parser = new MarkdownService();
        var diagrams = new DiagramService();
        var native = new NativeSvgService();
        var results = new List<object>();
        var failures = new List<string>();
        try { NativeDiagramContrastChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native diagram label contrast: " + ex.Message); }
        try { NativeDiagramThemeChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native diagram themes: " + ex.Message); }
        try { NativeDiagramPaintChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native diagram painting: " + ex.Message); }
        try { NativeDiagramFailureChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native diagram recovery: " + ex.Message); }
        try { NativeSvgChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native SVG safety: " + ex.Message); }
        try { NativeAssetChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native asset persistence: " + ex.Message); }
        try { NativeAcceptanceWorkflowChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native acceptance workflow: " + ex.Message); }
        try { NativeSaveRecoveryChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native save recovery: " + ex.Message); }
        try { NativeUpdateInteractionChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native update interactions: " + ex.Message); }
        try { NativePreviewChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native preview preparation: " + ex.Message); }
        try { NativeMarkdownChecks.Run(output, repository); }
        catch (Exception ex) { failures.Add("Native Markdown semantics: " + ex.Message); }
        try { NativeEditorChecks.Run(output); NativeInlineLabelChecks.Run(output); NativeWindowDiscoveryChecks.Run(output); NativeObservationWaitChecks.Run(output); NativeTableEditorChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native editor formatting: " + ex.Message); }
        try { PreviewStallWatchChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native preview stall diagnostics: " + ex.Message); }
        try { NativeLayoutChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native document layout: " + ex.Message); }
        try { NativeLiteralFormattingChecks.Run(output); NativeInlineLayoutChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native inline layout: " + ex.Message); }
        try { NativePreviewSchedulingChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native preview scheduling: " + ex.Message); }
        try { NativePaginationChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native preview pagination: " + ex.Message); }
        try { NativePaginationChecks.RunLargeBlocks(output); }
        catch (Exception ex) { failures.Add("Native large preview blocks: " + ex.Message); }
        try { NativeClipboardApartmentChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native clipboard apartments: " + ex.Message); }
        try { NativeShellLayoutChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native shell layout: " + ex.Message); }
        try { NativePerformanceChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native performance observations: " + ex.Message); }
        try { NativeExportAllocationChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native export allocation: " + ex.Message); }
        try { NativeExternalLinkChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native external links: " + ex.Message); }
        try { NativePdfSyntaxChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native PDF syntax: " + ex.Message); }
        try { NativePdfListChecks.Run(output, repository); }
        catch (Exception ex) { failures.Add("Native PDF lists: " + ex.Message); }
        try { NativePdfUnicodeChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native PDF Unicode: " + ex.Message); }
        try { NativePdfDecorationChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native PDF decorations: " + ex.Message); }
        try { NativeWordSyntaxChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native Word syntax: " + ex.Message); }
        try { NativeWordListChecks.Run(output, repository); }
        catch (Exception ex) { failures.Add("Native Word lists: " + ex.Message); }
        try { var fixture = NativeWordImportFixtureChecks.Run(output); NativeWordImportPreviewChecks.Run(output, fixture); }
        catch (Exception ex) { failures.Add("Native Word import fixture: " + ex.Message); }
        try { NativeWorkspaceContextChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native workspace context: " + ex.Message); }
        try { NativeHtmlSyntaxChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native HTML syntax: " + ex.Message); }
        try { NativeDocsSiteChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native Docs Site export: " + ex.Message); }
        try { NativeProgressChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native progress semantics: " + ex.Message); }
        try { NativeAnchorChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native anchor navigation: " + ex.Message); }
        try { NativeContextChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native context actions: " + ex.Message); }
        try { NativeSyntaxChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native syntax colouring: " + ex.Message); }
        try { NativeEditorColourChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native editor colours: " + ex.Message); }
        try { NativeTableChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native table copying: " + ex.Message); }
        try { NativeReviewChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native document review: " + ex.Message); }
        try { NativeWorkspaceChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native workspace monitoring: " + ex.Message); }
        try { NativeFigureChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native image figures: " + ex.Message); }
        try { NativeWordTemplateChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native Word templates: " + ex.Message); }
        try { NativeTemplateLibraryChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native template library: " + ex.Message); }
        try { NativeLibraryContentChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native library content: " + ex.Message); }
        try { NativeContentLibraryChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native content library: " + ex.Message); }
        try { NativeContentCatalogueChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native content catalogue: " + ex.Message); }
        try { NativeDetailsChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native details sections: " + ex.Message); }
        // Keep the existing first-construction timing check ahead of this control-heavy fixture.
        try { NativeDetailsLatencyChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native details expansion: " + ex.Message); }
        try { NativeMathChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native maths: " + ex.Message); }
        try { NativeArtifactChecks.Run(output, repository); }
        catch (Exception ex) { failures.Add("Native artefacts: " + ex.Message); }
        try { NativeWikiLinkChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native wikilinks: " + ex.Message); }
        try { NativeWorkspaceMapChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native workspace map: " + ex.Message); }
        try { NativeFolderMoveChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native folder move: " + ex.Message); }
        try { NativeHelpChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native help: " + ex.Message); }
        try { NativeSettingsChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native settings: " + ex.Message); }
        try { NativeSelectionMappingChecks.Run(output); }
        catch (Exception ex) { failures.Add("Selection mapping: " + ex.Message); }
        try { NativeWorkspaceChromeChecks.Run(output); }
        catch (Exception ex) { failures.Add("Workspace chrome: " + ex.Message); }
        try { NativeToolbarChecks.Run(output); }
        catch (Exception ex) { failures.Add("Toolbar layout: " + ex.Message); }
        try { NativeExportFeedbackChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native export feedback: " + ex.Message); }
        try { NativePreviewNavigationChecks.Run(output); }
        catch (Exception ex) { failures.Add("Native preview navigation: " + ex.Message); }
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(Path.Combine(repository, "tests", "fixtures"), "*", SearchOption.AllDirectories)
            .Where(p => Path.GetExtension(p) is ".md" or ".mmd" or ".mermaid").Order())
        {
            var text = File.ReadAllText(path);
            var standalone = Path.GetExtension(path) is ".mmd" or ".mermaid";
            var document = parser.Parse(text, standalone);
            foreach (var code in document.Syntax.Descendants<FencedCodeBlock>().Where(c => c.Info is "mermaid" or "mmd"))
            {
                foreach (var dark in new[] { false, true })
                {
                    var name = $"{++count:D3}-{Path.GetFileNameWithoutExtension(path)}-{(dark ? "dark" : "light")}";
                    var watch = Stopwatch.StartNew();
                    var diagram = diagrams.Render(code.Lines.ToString(), dark);
                    // The web autocomplete fixture intentionally contains only the incomplete header "flo".
                    var expectedInvalid = Path.GetFileName(path) is "broken-mermaid.md" or "autocomplete.mmd";
                    var valid = diagram.Success;
                    string? error = diagram.Error;
                    int bytes = 0;
                    if (valid)
                    {
                        try
                        {
                            var drawing = LensDocsStudio.Native.Platform.StaWorker.RunAsync(() => native.Render(diagram.Svg!)).GetAwaiter().GetResult();
                            var png = NativeSvgService.ToExportPng(drawing);
                            File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
                            File.WriteAllText(Path.Combine(output, name + ".svg"), native.Prepare(diagram.Svg!));
                            bytes = png.Length;
                            if (bytes < 100 || drawing.Width < 10 || drawing.Height < 10) throw new InvalidDataException("No meaningful native drawing.");
                        }
                        catch (Exception ex) { valid = false; error = ex.Message; }
                    }
                    var passed = expectedInvalid ? !diagram.Success : valid;
                    if (!passed) failures.Add(name + ": " + error);
                    results.Add(new { Fixture = Path.GetRelativePath(repository, path), Line = code.Line + 1, Theme = dark ? "Dark" : "Light", Passed = passed, ExpectedInvalid = expectedInvalid, Error = error, PngBytes = bytes, ElapsedMilliseconds = watch.ElapsedMilliseconds });
                }
            }
        }
        if (count == 0) failures.Add("No Mermaid fixtures discovered.");
        var cacheWatch = Stopwatch.StartNew();
        const string cacheSource = "flowchart LR\nA[Edit source] --> B[Reuse unchanged diagram]";
        var cachedDiagram = diagrams.Render(cacheSource);
        var cachedDrawing = native.Render(cachedDiagram.Svg!);
        var firstRenderMilliseconds = cacheWatch.Elapsed.TotalMilliseconds;
        cacheWatch.Restart();
        var reused = true;
        for (var repeat = 0; repeat < 20; repeat++)
        {
            var nextDiagram = diagrams.Render(cacheSource);
            reused &= ReferenceEquals(cachedDiagram, nextDiagram) && ReferenceEquals(cachedDrawing, native.Render(nextDiagram.Svg!));
        }
        if (!reused) failures.Add("Unchanged diagram rendering did not reuse its native drawing.");
        File.WriteAllText(Path.Combine(output, "preview-cache.json"), JsonSerializer.Serialize(new { FirstRenderMilliseconds = firstRenderMilliseconds, TwentyCachedRendersMilliseconds = cacheWatch.Elapsed.TotalMilliseconds, Reused = reused }));
        File.WriteAllText(Path.Combine(output, "mermaid-corpus.json"), JsonSerializer.Serialize(new { Total = count, Failures = failures, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
        try
        {
            var sourcePath = Path.Combine(repository, "tests", "fixtures", "mixed.md");
            var exportWatch = Stopwatch.StartNew();
            var content = new ExportPreparationService(parser, diagrams, native).Prepare(File.ReadAllText(sourcePath), sourcePath, Path.GetDirectoryName(sourcePath), false);
            var prepareMilliseconds = exportWatch.Elapsed.TotalMilliseconds;
            exportWatch.Restart(); var docx = new DocxExporter().Export(content); var docxMilliseconds = exportWatch.Elapsed.TotalMilliseconds;
            File.WriteAllBytes(Path.Combine(output, "mixed.docx"), docx);
            using (var package = WordprocessingDocument.Open(new MemoryStream(docx), false))
            {
                var errors = new OpenXmlValidator().Validate(package).Select(e => e.Description).ToArray();
                if (errors.Length != 0) throw new InvalidDataException(string.Join("; ", errors));
                if (!package.MainDocumentPart!.ImageParts.Any()) throw new InvalidDataException("The DOCX has no rendered diagram image.");
            }
            exportWatch.Restart(); var pdf = new PdfExporter().Export(content); var pdfMilliseconds = exportWatch.Elapsed.TotalMilliseconds;
            File.WriteAllBytes(Path.Combine(output, "mixed.pdf"), pdf);
            if (!System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Equals("%PDF-")) throw new InvalidDataException("The local PDF header is invalid.");
            using (var inspected = UglyToad.PdfPig.PdfDocument.Open(pdf))
            {
                var pages = inspected.GetPages().ToArray();
                var text = string.Join(" ", pages.SelectMany(p => p.GetWords()).Select(w => w.Text));
                if (!text.Contains("Export Fixture", StringComparison.Ordinal) || !text.Contains("Checklist", StringComparison.Ordinal))
                    throw new InvalidDataException("The PDF lost required document text.");
                if (!pages.Any(p => p.NumberOfImages > 0)) throw new InvalidDataException("The PDF lost the Mermaid image.");
                File.WriteAllText(Path.Combine(output, "pdf-inspection.json"), JsonSerializer.Serialize(new { Pages = pages.Length, Images = pages.Sum(p => p.NumberOfImages), Text = text }, new JsonSerializerOptions { WriteIndented = true }));
            }
            exportWatch.Restart(); var html = new HtmlExporter().Export(content); var htmlMilliseconds = exportWatch.Elapsed.TotalMilliseconds;
            File.WriteAllText(Path.Combine(output, "mixed.html"), html);
            File.WriteAllText(Path.Combine(output, "export-performance.json"), JsonSerializer.Serialize(new {
                Fixture = "tests/fixtures/mixed.md", SourceBytes = new FileInfo(sourcePath).Length, ReadAndPrepareMilliseconds = prepareMilliseconds,
                DocxMilliseconds = docxMilliseconds, PdfMilliseconds = pdfMilliseconds, HtmlMilliseconds = htmlMilliseconds,
                DocxBytes = docx.Length, PdfBytes = pdf.Length, HtmlCharacters = html.Length,
                Limitation = "Warm validation-process observations; export durations exclude file writes and separate schema/content validation."
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Exports generated and DOCX schema validated: {docx.Length} DOCX bytes, {pdf.Length} PDF bytes.");
        }
        catch (Exception ex) { failures.Add("Export validation: " + ex.Message); }
        File.WriteAllText(Path.Combine(output, "validation-summary.json"), JsonSerializer.Serialize(new { DiagramCases = count, Failures = failures }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Native validation: {count} diagram cases and export checks; {failures.Count} failures. Evidence: {output}");
        foreach (var failure in failures) Console.WriteLine(failure);
        return failures.Count == 0 ? 0 : 1;
    }
}
