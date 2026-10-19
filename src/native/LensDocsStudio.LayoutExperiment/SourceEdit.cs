namespace LensDocsStudio.LayoutExperiment;

internal static class SourceEdit
{
    // Preserve source boundaries around one contiguous replacement, including leading/distant edits.
    public static int Transform(string before, string after, int offset)
    {
        var prefix = 0; while (prefix < Math.Min(before.Length, after.Length) && before[prefix] == after[prefix]) prefix++;
        var suffix = 0; while (suffix < before.Length - prefix && suffix < after.Length - prefix && before[^(suffix + 1)] == after[^(suffix + 1)]) suffix++;
        if (offset < prefix) return offset;
        if (offset >= before.Length - suffix) return Math.Clamp(offset + after.Length - before.Length, 0, after.Length);
        return Math.Clamp(prefix + Math.Min(offset - prefix, after.Length - suffix - prefix), 0, after.Length);
    }
}
