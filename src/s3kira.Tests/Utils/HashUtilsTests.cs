using s3kira.Utils;

namespace s3kira.Tests.Utils;

public sealed class HashUtilsTests
{
    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("HEAD\n/s3kira\n\nhost:127.0.0.1:9000\nx-amz-content-sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\nx-amz-date:20230509T174643Z\n\nhost;x-amz-content-sha256;x-amz-date\ne3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "4a2d1bc6763fbff7ec45196b6997e4b209620f6f645be18249dc5198ab1b07a8")]
    public void GetSha256_String(string value, string result)
    {
        var res = HashUtils.GetSha256(value);
        Assert.Equal(result, res);
    }


    private static readonly byte[] Utf8Bytes = "Hello world"u8.ToArray();
    public static IEnumerable<object[]> Data =>
        new List<object[]>
        {
            new object[] { new byte[]{1,2,3}, "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81" },
            new object[] { Utf8Bytes, "64ec88ca00b268e5ba1a35678a1b5316d212f4f366b2477232534a8aeca37f3c" },
            new object[] { GetFileData(), "ca70f84ac5435026c7bfe8a99a99540f635b5cf1ae201b2eecb57d9f2b59f222"}
        };

    private static byte[] GetFileData()
    {
        var path = Path.Combine("files", "AST_LM_80_music_loop_embers_A.wav");
        var fi = new FileInfo(path);

        using (var stream = new MemoryStream((int)fi.Length))
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            fs.CopyTo(stream);
            return stream.ToArray();
        }
    }

    [Theory]
    [MemberData(nameof(Data))]
    public void GetSha256_Bytes(byte[] value, string result)
    {
        var res = HashUtils.GetSha256(value);
        Assert.Equal(result, res);
    }
    
}