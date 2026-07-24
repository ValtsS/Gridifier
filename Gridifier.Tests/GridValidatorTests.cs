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
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("JO", false)]
    [InlineData("JO2", false)]
    [InlineData("JO20A", false)]
    [InlineData("JO20AAA", false)]
    [InlineData("JO20A1", false)]
    [InlineData("SJ20AA", false)]
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
}