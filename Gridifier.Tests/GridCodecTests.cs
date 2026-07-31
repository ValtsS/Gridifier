using Gridifier.Shared.Validation;

namespace Gridifier.Tests;

public class GridCodecTests
{
    public static TheoryData<string, ushort> ValidCodes => new()
    {
        { "JO20", 17620 },
        { "KP20", 19520 },
        { "AA00", 0 },
        { "RR99", 32399 },
        { "JO99", 17699 },
        { "AR99", 1799 },
        { "BA00", 1800 },
    };

    [Theory]
    [MemberData(nameof(ValidCodes))]
    public void Encode_then_Decode_roundtrips(string grid, ushort _)
    {
        var code = GridCodec.Encode(grid[..4]);
        Assert.Equal(grid[..4], GridCodec.Decode(code));
    }

    [Theory]
    [InlineData("AA00", 0)]
    [InlineData("AR99", 1799)]
    [InlineData("BA00", 1800)]
    [InlineData("RR99", 32399)]
    public void Encode_maps_to_expected_code(string grid, ushort expected)
    {
        Assert.Equal(expected, GridCodec.Encode(grid));
    }

    [Fact]
    public void Encode_is_bijective_over_all_32400_grids()
    {
        var seen = new HashSet<ushort>();
        for (char a = 'A'; a <= 'R'; a++)
        for (char b = 'A'; b <= 'R'; b++)
        for (char c = '0'; c <= '9'; c++)
        for (char d = '0'; d <= '9'; d++)
        {
            var grid = $"{a}{b}{c}{d}";
            var code = GridCodec.Encode(grid);
            Assert.True(seen.Add(code), $"Collision for {grid}");
            Assert.Equal(grid, GridCodec.Decode(code));
        }
        Assert.Equal(32_400, seen.Count);
    }

    [Fact]
    public void Decode_of_max_code_is_RR99()
    {
        Assert.Equal("RR99", GridCodec.Decode(32_399));
    }

    [Fact]
    public void Encode_handles_lowercase_input()
    {
        Assert.Equal(GridCodec.Encode("JO20"), GridCodec.Encode("jo20"));
        Assert.Equal(GridCodec.Encode("KP99"), GridCodec.Encode("kp99"));
    }

    [Fact]
    public void Encode_handles_mixed_case_input()
    {
        Assert.Equal(GridCodec.Encode("JO20"), GridCodec.Encode("jO20"));
        Assert.Equal(GridCodec.Encode("JO20"), GridCodec.Encode("Jo20"));
    }

    [Theory]
    [InlineData("jo20")]
    [InlineData("jO20")]
    [InlineData("KP99")]
    [InlineData("rr99")]
    public void Encode_then_Decode_roundtrips_case_insensitively(string grid)
    {
        var code = GridCodec.Encode(grid);
        Assert.Equal(grid.ToUpperInvariant(), GridCodec.Decode(code));
    }
}
