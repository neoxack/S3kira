using BenchmarkDotNet.Attributes;
using Minio;

namespace s3kira.Benchmark;

[MemoryDiagnoser]
public class FullBenchmark
{
    private S3Kira _s3Kira = null!;
    private MinioClient _minioClient = null!;
    private CancellationToken _cancellation;
    private readonly MemoryStream _memoryStream = new MemoryStream();
    private readonly MemoryStream _outputData = new MemoryStream(60 * 1024 * 1024);
    private long _size;
    
    private const string ContentType = "audio/wav";
    
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

        const string fileName = "AST_LM_80_music_loop_embers_A.wav";
        var path = Path.Combine("files", fileName);
        var fi = new FileInfo(path);
        _size = fi.Length;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            fs.CopyTo(_memoryStream);
        }
        _memoryStream.Seek(0, SeekOrigin.Begin);
        _outputData.Seek(0, SeekOrigin.Begin);
    }
    
    [Benchmark]
    public async Task Minio()
    {
        var bucketName = "benchmark";
        
        // make bucket
        await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), _cancellation);

        // bucket exists
        var bucketExists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), _cancellation);
        if (!bucketExists)
        {
            throw new Exception("Bucket not exists");
        }

        _memoryStream.Seek(0, SeekOrigin.Begin);
        var fileName = "file_" + Guid.NewGuid()+"_привет.wav";

        // upload file
        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(fileName)
            .WithContentType(ContentType)
            .WithStreamData(_memoryStream)
            .WithObjectSize(_size), _cancellation);
        
        // file exists
        var stat = await _minioClient.StatObjectAsync(new StatObjectArgs()
            .WithBucket(bucketName)
            .WithObject(fileName), _cancellation);
        if (stat == null)
        {
            throw new Exception("File not exists");
        }
        
        _outputData.Seek(0, SeekOrigin.Begin);
        
        // download file
        var result = await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(fileName)
            .WithCallbackStream((file, ct) => file.CopyToAsync(_outputData, ct)), _cancellation);
        
        if (result == null || _outputData.Length != _size)
        {
            throw new Exception("Download error");
        }

        // delete file
        await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(fileName),
            _cancellation);
        
        // delete bucket
        await _minioClient.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(bucketName),
            _cancellation);
    }
    
    [Benchmark(Baseline = true)]
    public async Task S3Kira()
    {
        var bucketName = "benchmark";
        
        // make bucket
        await _s3Kira.CreateBucketAsync(bucketName, _cancellation);

        // bucket exists
        var bucketExists = await _s3Kira.IsBucketExistsAsync(bucketName, _cancellation);
        if (!bucketExists)
        {
            throw new Exception("Bucket not exists");
        }

        _memoryStream.Seek(0, SeekOrigin.Begin);
        var fileName = "file_" + Guid.NewGuid()+"_привет.wav";

        // upload file
        await _s3Kira.UploadFileAsync(new UploadFileCommand
        {
            Bucket = bucketName,
            FileName = fileName,
            ContentType = ContentType,
            Size = _size,
            Stream = _memoryStream
        }, _cancellation);
        
        // file exists
        var fileExists = await _s3Kira.IsFileExistsAsync(new FileExistsQuery
        {
            Bucket = bucketName,
            FileName = fileName
        }, _cancellation);
        
        if (!fileExists)
        {
            throw new Exception("File not exists");
        }
        
        _outputData.Seek(0, SeekOrigin.Begin);
        
        // download file
        var stream = await _s3Kira.GetFileStreamAsync(new FileStreamQuery()
        {
            Bucket = bucketName,
            FileName = fileName
        }, _cancellation);
        
        await using (stream)
        {
            await stream.CopyToAsync(_outputData, _cancellation);
        }
        
        if (stream == null || _outputData.Length != _size)
        {
            throw new Exception("Download error");
        }

        // delete file
        await _s3Kira.DeleteFileAsync(new DeleteFileCommand()
        {
            Bucket = bucketName,
            FileName = fileName
        },
        _cancellation);
        
        // delete bucket
        await _s3Kira.DeleteBucketAsync(bucketName,_cancellation);
    }
}