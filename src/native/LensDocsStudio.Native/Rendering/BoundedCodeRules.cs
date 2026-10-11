using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Highlighting;

namespace LensDocsStudio.Native.Rendering;

public static class BoundedCodeRules
{
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(25);

    public static HighlightingRuleSet Create(HighlightingRuleSet original)
    {
        var copied = new Dictionary<HighlightingRuleSet, HighlightingRuleSet>(ReferenceEqualityComparer.Instance);
        return Copy(original);

        HighlightingRuleSet Copy(HighlightingRuleSet source)
        {
            if (copied.TryGetValue(source, out var existing)) return existing;
            var target = new HighlightingRuleSet { Name = source.Name }; copied.Add(source, target);
            foreach (var rule in source.Rules) target.Rules.Add(new() { Regex = Expression(rule.Regex), Color = rule.Color });
            foreach (var span in source.Spans)
                target.Spans.Add(new()
                {
                    StartExpression = Expression(span.StartExpression), EndExpression = Expression(span.EndExpression),
                    RuleSet = span.RuleSet is null ? null : Copy(span.RuleSet), StartColor = span.StartColor, SpanColor = span.SpanColor, EndColor = span.EndColor,
                    SpanColorIncludesStart = span.SpanColorIncludesStart, SpanColorIncludesEnd = span.SpanColorIncludesEnd
                });
            return target;
        }
        static Regex? Expression(Regex? expression) => expression is null ? null : new(expression.ToString(), expression.Options, MatchTimeout);
    }
}
