namespace s3kira;

internal static class Headers 
{
    public const string Host = "host";
    public const string XAmzContentSha = "x-amz-content-sha256";
    public const string XAmzDate = "x-amz-date";
    public const string Authorization = "Authorization";
    public const string ContentType = "content-type";
    public const string ContentLength = "content-length";
    public const string MinioForceDelete = "x-minio-force-delete";

    private static readonly string[] SignatureHeaders =
    {
        Host,
        XAmzContentSha,
        XAmzDate
    };

    public static readonly string SignatureHeadersStr = string.Join(';', SignatureHeaders);
}