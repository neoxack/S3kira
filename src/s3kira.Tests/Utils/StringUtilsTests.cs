using s3kira.Utils;

namespace s3kira.Tests.Utils;

public sealed class StringUtilsTests
{
    private static readonly byte[] Utf8Bytes = "s3kira"u8.ToArray();
    private static readonly byte[] Utf8CyrillicBytes = "Это база!"u8.ToArray();
    public static readonly object[][] ToHexStringData =
    {
        new object[] { new byte[]{1,2,3}, "010203" },
        new object[] { Utf8Bytes, "73336b697261" },
        new object[] { Utf8CyrillicBytes, "d0add182d0be20d0b1d0b0d0b7d0b021" },
    };
    
    [Theory]
    [MemberData(nameof(ToHexStringData))]
    public void ToHexString(byte[] value, string result)
    {
        var res = StringUtils.ToHexString(value);
        Assert.Equal(result, res);
    }

    public static readonly object[][] FormatDateData =
    {
        new object[] { new DateTime(2023, 04, 28), Formats.Iso8601Date, "20230428" },
        new object[] { new DateTime(1992, 01, 01), Formats.Iso8601Date, "19920101" },
        new object[] { new DateTime(2035, 12, 31), Formats.Iso8601Date, "20351231" },
        new object[] { new DateTime(2035, 12, 31, 14, 5, 5), Formats.Iso8601DateTime, "20351231T140505Z" }
    };

    [Theory]
    [MemberData(nameof(FormatDateData))]
    public void FormatDate(DateTime value, string format, string result)
    {
        Span<char> buffer = stackalloc char[64];
        var encoded = StringUtils.FormatDate(ref buffer, value, format);
        
        Assert.Equal(result, buffer[..encoded].ToString());
    }
    
    [Theory]
    [InlineData("123", "123")]
    [InlineData("with space", "with%20space")]
    [InlineData("русский текст", "%D1%80%D1%83%D1%81%D1%81%D0%BA%D0%B8%D0%B9%20%D1%82%D0%B5%D0%BA%D1%81%D1%82")]
    public void AppendUrlEncoded(string value, string result)
    {
        var builder = new ValueStringBuilder(stackalloc char[128]);
        StringUtils.AppendUrlEncoded(ref builder, value);
        Assert.Equal(result, builder.Flush());
    }
}