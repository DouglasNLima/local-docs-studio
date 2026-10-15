# Mermaid editor visual polish

Work in progress on `feature/native-mermaid-editor-visual-polish`, targeting
`feature/native-mermaid-visual-editor` at verified `eb81939835a1bb2a92cf76570de77d8d69e571ff`.
PR 26 and its ancestors remain unchanged.

## Baseline

The verified head passes native, desktop UX and browser CI (runs 38784102177,
38784102160 and 38784102174). The actual Mermaid evidence from run 38784102160
has been retrieved for comparison. The existing delivery report remains the
functional contract; this report will record the presentation changes and final evidence.

Inspected before images show generic local Button/TextBox/ComboBox styles, a
crowded wrapping text toolbar, irregular palette buttons, a flat list, permanently
visible square ports, unstructured inspector fields and two competing footer rows.
The main window uses Segoe UI, compact outline CommandIcons, neutral PanelBrush
surfaces, Desktop command/selection templates and Fluent form controls. These
resources are the reference for this work; Core transformations remain outside scope.

All new application/installer execution is restricted to disposable Windows CI.
The ordinary desktop installation, settings and personal workspaces are untouched.
