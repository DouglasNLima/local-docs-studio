using System.Numerics;
using System.Text.RegularExpressions;

namespace LensDocsStudio.Core.Updates;

public sealed partial record SemanticVersion(BigInteger Major, BigInteger Minor, BigInteger Patch, string Prerelease) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = null!;
        if (value is null || value.Length > 128) return false;
        var match = Pattern().Match(value);
        if (!match.Success) return false;
        var preview = match.Groups[4].Value;
        if (preview.Split('.').Any(p => p.Length > 1 && p.All(char.IsAsciiDigit) && p[0] == '0')) return false;
        version = new(BigInteger.Parse(match.Groups[1].Value), BigInteger.Parse(match.Groups[2].Value), BigInteger.Parse(match.Groups[3].Value), preview);
        return true;
    }
    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;
        foreach (var comparison in new[] { Major.CompareTo(other.Major), Minor.CompareTo(other.Minor), Patch.CompareTo(other.Patch) })
            if (comparison != 0) return comparison;
        if (Prerelease.Length == 0 || other.Prerelease.Length == 0) return Prerelease.Length == other.Prerelease.Length ? 0 : Prerelease.Length == 0 ? 1 : -1;
        var left = Prerelease.Split('.'); var right = other.Prerelease.Split('.');
        for (var index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            var leftNumber = BigInteger.Zero; var rightNumber = BigInteger.Zero;
            var leftNumeric = left[index].All(char.IsAsciiDigit) && BigInteger.TryParse(left[index], out leftNumber);
            var rightNumeric = right[index].All(char.IsAsciiDigit) && BigInteger.TryParse(right[index], out rightNumber);
            var comparison = leftNumeric && rightNumeric ? leftNumber.CompareTo(rightNumber) : leftNumeric != rightNumeric ? leftNumeric ? -1 : 1 : StringComparer.Ordinal.Compare(left[index], right[index]);
            if (comparison != 0) return comparison;
        }
        return left.Length.CompareTo(right.Length);
    }
    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$")]
    private static partial Regex Pattern();
}
