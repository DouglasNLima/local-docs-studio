using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.SourceAuthoring;

/// <summary>Shared dispatch for architecture views; authoritative source remains untouched.</summary>
public static class TopologySource
{
    private static readonly Regex Header = SourceText.Pattern(@"^(?:architecture(?:-beta)?(?:\s|$)|C4(?:Context|Container|Component|Dynamic|Deployment)\b|block(?:-beta)?\b)", RegexOptions.IgnoreCase);
    public static bool IsTopology(string source) => Header.IsMatch(ChartSource.Header(source));
}
