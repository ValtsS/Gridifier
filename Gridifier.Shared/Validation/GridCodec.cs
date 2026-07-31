namespace Gridifier.Shared.Validation;

public static class GridCodec
{
    public static ushort Encode(string grid)
    {
        // 4-char Maidenhead: [A-R][A-R][0-9][0-9], case-insensitive
        var a = char.ToUpperInvariant(grid[0]) - 'A';
        var b = char.ToUpperInvariant(grid[1]) - 'A';
        var c = char.ToUpperInvariant(grid[2]) - '0';
        var d = char.ToUpperInvariant(grid[3]) - '0';
        return (ushort)((a * 18 + b) * 100 + c * 10 + d);
    }

    public static string Decode(ushort code)
    {
        Span<char> chars = stackalloc char[4];
        chars[3] = (char)('0' + code % 10);
        code /= 10;
        chars[2] = (char)('0' + code % 10);
        code /= 10;
        chars[1] = (char)('A' + code % 18);
        code /= 18;
        chars[0] = (char)('A' + code);
        return new string(chars);
    }
}
