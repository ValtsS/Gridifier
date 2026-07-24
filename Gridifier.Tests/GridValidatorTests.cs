using Gridifier.Shared.Validation;

namespace Gridifier.Tests;

public class GridValidatorTests
{
    [Theory]
    [InlineData("JO20AA", true)]
    [InlineData("FN42hn", true)]
    [InlineData("jo20aa", true)]
    [InlineData("  JO20AA  ", true)]
    [InlineData("JO20", true)]
    [InlineData("jo20", true)]
    [InlineData("AA00AA", true)]
    [InlineData("RR99XX", true)]
    [InlineData("JO20AA11", true)]
    [InlineData("FN42hn88", true)]
    [InlineData("JO20AA99", true)]
    [InlineData("JO20AA99AA", true)]
    [InlineData("JO20AA99AB", true)]
    [InlineData("JO20AA99ZZ", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("JO", false)]
    [InlineData("JO2", false)]
    [InlineData("JO20A", false)]
    [InlineData("JO20AAA", false)]
    [InlineData("JO20A1", false)]
    [InlineData("SJ20AA", false)]
    [InlineData("JO20AA1", false)]
    [InlineData("JO20AA111", false)]
    [InlineData("JO20AA ", true)]
    public void IsValid_various_inputs(string? input, bool expected)
    {
        var result = GridValidator.IsValid(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  jo20aa  ", "JO20AA")]
    [InlineData("fn42hn", "FN42HN")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_various_inputs(string? input, string expected)
    {
        var result = GridValidator.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("JO20AA", "JO20")]
    [InlineData("JO20AA99", "JO20")]
    [InlineData("JO20", "JO20")]
    [InlineData("FN42hn88", "FN42")]
    public void Shorten_returns_first_4_chars(string input, string expected)
    {
        var result = GridValidator.Shorten(input);
        Assert.Equal(expected, result);
    }
}