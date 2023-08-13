using BenchmarkDotNet.Attributes;
using Minio;

namespace s3kira.Benchmark;

[MemoryDiagnoser]
public class DownloadFileBenchmark
{
    private S3Kira _s3Kira = null!;
    private MinioClient _minioClient = null!;
    private CancellationToken _cancellation;
    private MemoryStream _outputData = null!;
    
    private const string Bucket = "archive";
    private const string FileName = "289606f1-658f-44dc-9c98-f7f10568c6c1";
    
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
        _outputData = new MemoryStream(60 * 1024 * 1024);
    }
    

    [Benchmark]
    public async Task DownloadFile_Minio()
    {
        _outputData.Seek(0, SeekOrigin.Begin);
        
        var args = new GetObjectArgs()
            .WithBucket(Bucket)
            .WithObject(FileName)
            .WithCallbackStream((file, ct) => file.CopyToAsync(_outputData, ct));
        var result = await _minioClient.GetObjectAsync(args, _cancellation);
    }
    
    [Benchmark(Baseline = true)]
    public async Task DownloadFile_S3Kira()
    {
        _outputData.Seek(0, SeekOrigin.Begin);
        
        var stream = await _s3Kira.GetFileStreamAsync(new FileStreamQuery
        {
            Bucket = Bucket,
            FileName = FileName
        }, CancellationToken.None);

        await using (stream)
        {
            await stream.CopyToAsync(_outputData, _cancellation);
        }
    }
}