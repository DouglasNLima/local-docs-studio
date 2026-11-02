namespace LensDocsStudio.Native.Validation;

internal static class SelectionTableFixture
{
    // Synthetic SPC-style change log; no customer document or imported metadata.
    public static string Create(string newline = "\n") => ("# SPC change log\n\n"
        + "| ID | Planning | Scope | Proposer | Status | Validity |\n"
        + "| --- | --- | --- | --- | --- | --- |\n"
        + string.Concat(Enumerable.Range(1, 15).Select(row =>
            $"| Q-{row:00} | planning **planning** and *planning* | within, **within**; planning within the agreed scope with wrapping text | proposer *proposer* | **RED**, RED | validity **validity**; Café 😀 é, validity. |\n")))
        .ReplaceLineEndings(newline);
}
