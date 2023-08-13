namespace s3kira;

public sealed class UploadFileCommand
{
    public required string Bucket { get; init; }
    public required string FileName { get; init; }
    public required long Size { get; init; }
    public required string ContentType { get; init; }
    public required Stream Stream { get; init; }
    
}