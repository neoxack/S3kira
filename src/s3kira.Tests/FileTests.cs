namespace s3kira.Tests;

public sealed class FileTests: IClassFixture<S3KiraFactory>
{
    private const string Bucket = "filezz";
    
    private readonly S3Kira _s3Kira;
    public FileTests(S3KiraFactory s3KiraFactory)
    {
        _s3Kira = s3KiraFactory.GetS3Kira();
    }

    private Task CreateBucketIfNotExists()
    {
        return _s3Kira.CreateBucketAsync(Bucket, CancellationToken.None);
    }

    [Fact]
    public async Task Upload_Then_Download_Then_Delete_File()
    {
        await CreateBucketIfNotExists();
        var path = Path.Combine("files", "AST_LM_80_music_loop_embers_A.wav");
        var fi = new FileInfo(path);
        
        // upload
        await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            await _s3Kira.UploadFileAsync(new UploadFileCommand
            {
                Bucket = Bucket,
                FileName = fi.Name,
                Size = fi.Length,
                ContentType = "audio/wav",
                Stream = fs
            }, CancellationToken.None);
        }
        
        // exists
        var fileExists = await _s3Kira.IsFileExistsAsync(new FileExistsQuery()
        {
            Bucket = Bucket,
            FileName = fi.Name
        }, CancellationToken.None);
        
        Assert.True(fileExists);
        
        // download
        var stream = await _s3Kira.GetFileStreamAsync(new FileStreamQuery
        {
            Bucket = Bucket,
            FileName = fi.Name
        }, CancellationToken.None);
        
        using (var memoryStream = new MemoryStream())
        await using(stream)
        {
            await stream.CopyToAsync(memoryStream);
            Assert.True(memoryStream.Length == fi.Length);
        }
        Assert.False(stream.CanRead);
        
        // delete
        await _s3Kira.DeleteFileAsync(new DeleteFileCommand
        {
            Bucket = Bucket,
            FileName = fi.Name
        }, CancellationToken.None);
        
        // not exists
        fileExists = await _s3Kira.IsFileExistsAsync(new FileExistsQuery()
        {
            Bucket = Bucket,
            FileName = fi.Name
        }, CancellationToken.None);
        
        Assert.False(fileExists);
    }
    
    [Fact]
    public async Task Upload_Then_Delete_Big_File()
    {
        await CreateBucketIfNotExists();
        var path = Path.Combine("files", "Feels Free.zip");
        var fi = new FileInfo(path);
        
        await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            await _s3Kira.UploadFileAsync(new UploadFileCommand
            {
                Bucket = Bucket,
                FileName = fi.Name,
                Size = fi.Length,
                ContentType = "application/zip",
                Stream = fs
            }, CancellationToken.None);
        }
        
        await _s3Kira.DeleteFileAsync(new DeleteFileCommand
        {
            Bucket = Bucket,
            FileName = fi.Name
        }, CancellationToken.None);
        
        var fileExists = await _s3Kira.IsFileExistsAsync(new FileExistsQuery()
        {
            Bucket = Bucket,
            FileName = fi.Name
        }, CancellationToken.None);
        
        Assert.False(fileExists);
    }
    
}