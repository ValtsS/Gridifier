using System.Text.RegularExpressions;

namespace Gridifier.Shared.Validation;

public static partial class CallsignValidator
{
    [GeneratedRegex(@"^[A-Z0-9/]+$", RegexOptions.Singleline)]
    private static partial Regex AllowedPattern();

    public static string Normalize(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign))
            return string.Empty;

        return callsign
            .Trim()
            .ToUpperInvariant();
    }

    public static bool IsValid(string? callsign)
    {
        var normalized = Normalize(callsign);
        return normalized.Length > 0
            && normalized.Length <= 16
            && AllowedPattern().IsMatch(normalized);
    }
}