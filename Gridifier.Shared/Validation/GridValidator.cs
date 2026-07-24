using System.Text.RegularExpressions;

namespace Gridifier.Shared.Validation;

public static partial class GridValidator
{
    [GeneratedRegex(@"^[A-R]{2}[0-9]{2}([A-X]{2})?$", RegexOptions.Singleline)]
    private static partial Regex MaidenheadPattern();

    public static string Normalize(string? grid)
    {
        if (string.IsNullOrWhiteSpace(grid))
            return string.Empty;

        return grid.Trim().ToUpperInvariant();
    }

    public static bool IsValid(string? grid)
    {
        var normalized = Normalize(grid);
        return normalized.Length is 4 or 6
            && MaidenheadPattern().IsMatch(normalized);
    }
}