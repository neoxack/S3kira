namespace s3kira.Tests;

public class BucketTests: IClassFixture<S3KiraFactory>
{
    private readonly S3Kira _s3Kira;
    public BucketTests(S3KiraFactory s3KiraFactory)
    {
        _s3Kira = s3KiraFactory.GetS3Kira();
    }

    private static readonly Random Random = new Random();
    
    private static string GetRandomBucketName()
    {
        return $"bucket-{Random.Next(10, 300_000)}";
    }

    [Fact]
    public async Task CreateNotExistsBucket_ShouldBeSuccess()
    {
        var bucket = GetRandomBucketName();
        await _s3Kira.CreateBucketAsync(bucket, CancellationToken.None);
    }
    
    [Fact]
    public async Task CreateExistsBucket_ShouldBeSuccess()
    {
        var bucket = GetRandomBucketName();
        await _s3Kira.CreateBucketAsync(bucket, CancellationToken.None);
        await _s3Kira.CreateBucketAsync(bucket, CancellationToken.None);
    }
    
    [Fact]
    public async Task BucketExists_ForExistsBucket_ShouldBeTrue()
    {
        var bucket = GetRandomBucketName();
        await _s3Kira.CreateBucketAsync(bucket, CancellationToken.None);
        
        var exists = await _s3Kira.IsBucketExistsAsync(bucket, CancellationToken.None);
        
        Assert.True(exists);
    }
    
    [Fact]
    public async Task BucketExists_ForNotExistsBucket_ShouldBeFalse()
    {
        var bucket = GetRandomBucketName();
        var exists = await _s3Kira.IsBucketExistsAsync(bucket, CancellationToken.None);
        
        Assert.False(exists);
    }
    
    [Fact]
    public async Task Delete_ExistsBucket_ShouldBeSuccess()
    {
        var bucket = GetRandomBucketName();
        await _s3Kira.CreateBucketAsync(bucket, CancellationToken.None);
        await _s3Kira.DeleteBucketAsync(bucket, CancellationToken.None);
        
        var exists = await _s3Kira.IsBucketExistsAsync(bucket, CancellationToken.None);
        
        Assert.False(exists);
    }
    
    [Fact]
    public async Task Delete_NotExistsBucket_ShouldBeSuccess()
    {
        var bucket = GetRandomBucketName();
        await _s3Kira.DeleteBucketAsync(bucket, CancellationToken.None);
    }
    
    [Fact]
    public async Task ForceDelete_BucketWithFiles_ShouldBeSuccess()
    {
        var bucket = GetRandomBucketName();
        await _s3Kira.CreateBucketAsync(bucket, CancellationToken.None);
        
        var path = Path.Combine("files", "AST_LM_80_music_loop_embers_A.wav");
        var fi = new FileInfo(path);
        
        await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            await _s3Kira.UploadFileAsync(new UploadFileCommand
            {
                Bucket = bucket,
                FileName = fi.Name,
                Size = fi.Length,
                ContentType = "audio/wav",
                Stream = fs
            }, CancellationToken.None);
        }
        
        await _s3Kira.DeleteBucketAsync(bucket, true, CancellationToken.None);
        var bucketExists = await _s3Kira.IsBucketExistsAsync(bucket, CancellationToken.None);
        Assert.False(bucketExists);
    }
}