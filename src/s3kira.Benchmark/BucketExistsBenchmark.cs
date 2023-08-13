using BenchmarkDotNet.Attributes;
using Minio;

namespace s3kira.Benchmark;

[MemoryDiagnoser]
public class BucketExistsBenchmark
{
    private S3Kira _s3Kira = null!;
    private MinioClient _minioClient = null!;
    private CancellationToken _cancellation;
    
    [GlobalSetup]
    public void Config()
    {
        _cancellation = new CancellationToken();
        var config = new S3KiraSettings
        {
            Endpoint = "127.0.0.1:9000",
            AccessKey = "astrosoul",
            SecretKey = "astrosoul",
            Region = ""
        };
        _s3Kira = new S3Kira(config);
        _minioClient = new MinioClient()
            .WithEndpoint(config.Endpoint)
            .WithCredentials(config.AccessKey, config.SecretKey)
            .Build();
    }
    

    [Benchmark]
    public async Task<bool> BucketExists_Minio()
    {
        var beArgs = new BucketExistsArgs()
            .WithBucket("s3kira");
        return await _minioClient.BucketExistsAsync(beArgs, _cancellation);
    }
    
    [Benchmark(Baseline = true)]
    public async Task<bool> BucketExists_S3Kira()
    {
        var bucketExists = await _s3Kira.IsBucketExistsAsync("s3kira", _cancellation);
        return bucketExists;
    }
}