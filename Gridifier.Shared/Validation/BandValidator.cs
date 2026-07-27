using System.Text.RegularExpressions;

namespace Gridifier.Shared.Validation;

public static partial class BandValidator
{
    [GeneratedRegex(@"^\d+[cm]$", RegexOptions.IgnoreCase)]
    private static partial Regex BandPattern();

    public static bool IsValid(string band)
    {
        return !string.IsNullOrWhiteSpace(band) && BandPattern().IsMatch(band);
    }

    public static string Normalize(string band)
    {
        return band.Trim().ToLowerInvariant();
    }
}
