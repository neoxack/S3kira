namespace s3kira;

public sealed class FileStreamQuery
{
    public required string Bucket { get; init; }
    public required string FileName { get; init; }
}