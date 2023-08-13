namespace s3kira;

public sealed class DeleteFileCommand
{
    public required string Bucket { get; init; }
    public required string FileName { get; init; }
}