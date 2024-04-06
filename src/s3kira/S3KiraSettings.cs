namespace s3kira;

public sealed class S3KiraSettings
{
    public required string Endpoint { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public string Region { get; init; } = "us-east-1";
    public bool UseHttps { get; init; }
}