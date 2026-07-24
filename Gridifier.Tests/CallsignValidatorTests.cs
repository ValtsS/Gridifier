using Gridifier.Shared.Validation;

namespace Gridifier.Tests;

public class CallsignValidatorTests
{
    [Theory]
    [InlineData("TEST1", true)]
    [InlineData("K1ABC", true)]
    [InlineData("DL/VE3ABC", true)]
    [InlineData("  test1  ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("ABCDEFGHIJKLMNOPQ", false)]
    [InlineData("test@home", false)]
    [InlineData("hello world", false)]
    [InlineData("a.bc", false)]
    public void IsValid_various_inputs(string? input, bool expected)
    {
        var result = CallsignValidator.IsValid(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  test1  ", "TEST1")]
    [InlineData("k1abc", "K1ABC")]
    [InlineData(" dl/ve3abc ", "DL/VE3ABC")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_various_inputs(string? input, string expected)
    {
        var result = CallsignValidator.Normalize(input);
        Assert.Equal(expected, result);
    }
}