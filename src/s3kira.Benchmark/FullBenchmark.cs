using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3.Util;
using BenchmarkDotNet.Attributes;
using Minio;

namespace s3kira.Benchmark;

[MemoryDiagnoser]
public class FullBenchmark
{
    private S3Kira _s3Kira = null!;
    private MinioClient _minioClient = null!;
    private AmazonS3Client _amazonS3Client = null!;
    private TransferUtility _transferUtility = null!;
    private CancellationToken _cancellation;
    private readonly MemoryStream _memoryStream = new MemoryStream();
    private readonly MemoryStream _outputData = new MemoryStream(60 * 1024 * 1024);
    private long _size;
    
    private const string ContentType = "audio/wav";
    
    private const string BucketName = "benchmark";
    
    [GlobalSetup]
    public void Config()
    {
        _cancellation = new CancellationToken();
        var config = new S3KiraSettings
        {
            Endpoint = "127.0.0.1:9000",
            AccessKey = "astrosoul",
            SecretKey = "astrosoul"
        };
        _s3Kira = new S3Kira(config);
        
        _minioClient = new MinioClient()
            .WithEndpoint(config.Endpoint)
            .WithCredentials(config.AccessKey, config.SecretKey)
            .WithRegion(config.Region)
            .Build();
        
        _amazonS3Client = new AmazonS3Client(
            new BasicAWSCredentials(config.AccessKey, config.SecretKey),
            new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USEast1,
                ServiceURL = $"http://{config.Endpoint}",
                ForcePathStyle = true // MUST be true to work correctly with MinIO server
            });
        _transferUtility = new TransferUtility(_amazonS3Client);

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
    public async Task MinioDotnet()
    {
        // make bucket
        await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(BucketName), _cancellation);

        // bucket exists
        var bucketExists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(BucketName), _cancellation);
        if (!bucketExists)
        {
            throw new Exception("Bucket not exists");
        }

        _memoryStream.Seek(0, SeekOrigin.Begin);
        var fileName = "file_" + Guid.NewGuid()+"_привет.wav";

        // upload file
        await _minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(BucketName)
            .WithObject(fileName)
            .WithContentType(ContentType)
            .WithStreamData(_memoryStream)
            .WithObjectSize(_size), _cancellation);
        
        // file exists
        var stat = await _minioClient.StatObjectAsync(new StatObjectArgs()
            .WithBucket(BucketName)
            .WithObject(fileName), _cancellation);
        if (stat == null)
        {
            throw new Exception("File not exists");
        }
        
        _outputData.Seek(0, SeekOrigin.Begin);
        
        // download file
        var result = await _minioClient.GetObjectAsync(new GetObjectArgs()
            .WithBucket(BucketName)
            .WithObject(fileName)
            .WithCallbackStream((file, ct) => file.CopyToAsync(_outputData, ct)), _cancellation);
        
        if (result == null || _outputData.Length != _size)
        {
            throw new Exception("Download error");
        }

        // delete file
        await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(BucketName)
                .WithObject(fileName),
            _cancellation);
        
        // delete bucket
        await _minioClient.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(BucketName),
            _cancellation);
    }

    [Benchmark]
    public async Task AmazonAwsSdk()
    {
        // make bucket
        await _amazonS3Client.PutBucketAsync(BucketName, _cancellation);
        
        // bucket exists
        var bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(_amazonS3Client, BucketName);
        if (!bucketExists)
        {
            throw new Exception("Bucket not exists");
        }
        
        _memoryStream.Seek(0, SeekOrigin.Begin);
        var fileName = "file_" + Guid.NewGuid()+"_привет.wav";
        
        // upload file
        await _transferUtility.UploadAsync(new TransferUtilityUploadRequest
        {
            BucketName = BucketName,
            ContentType = ContentType,
            InputStream = _memoryStream,
            AutoCloseStream = false,
            Key = fileName,
        }, _cancellation);
        
        // file exists
        await _amazonS3Client.GetObjectMetadataAsync(BucketName, fileName, _cancellation);
        
        _outputData.Seek(0, SeekOrigin.Begin);
        
        // download file
        var file = await _amazonS3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = BucketName,
            Key = fileName
        }, _cancellation);
        
        await using (var stream = file.ResponseStream)
        {
            await stream.CopyToAsync(_outputData, _cancellation);
        }
        
        if (file == null || _outputData.Length != _size)
        {
            throw new Exception("Download error");
        }
        
        // delete file
        await _amazonS3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = BucketName,
            Key = fileName
        },
        _cancellation);
        
        // delete bucket
        await _amazonS3Client.DeleteBucketAsync(BucketName, _cancellation);
    }

    [Benchmark(Baseline = true)]
    public async Task S3Kira()
    {
        // make bucket
        await _s3Kira.CreateBucketAsync(BucketName, _cancellation);

        // bucket exists
        var bucketExists = await _s3Kira.IsBucketExistsAsync(BucketName, _cancellation);
        if (!bucketExists)
        {
            throw new Exception("Bucket not exists");
        }

        _memoryStream.Seek(0, SeekOrigin.Begin);
        var fileName = "file_" + Guid.NewGuid()+"_привет.wav";

        // upload file
        await _s3Kira.UploadFileAsync(new UploadFileCommand
        {
            Bucket = BucketName,
            FileName = fileName,
            ContentType = ContentType,
            Size = _size,
            Stream = _memoryStream
        }, _cancellation);
        
        // file exists
        var fileExists = await _s3Kira.IsFileExistsAsync(new FileExistsQuery
        {
            Bucket = BucketName,
            FileName = fileName
        }, _cancellation);
        
        if (!fileExists)
        {
            throw new Exception("File not exists");
        }
        
        _outputData.Seek(0, SeekOrigin.Begin);
        
        // download file
        var stream = await _s3Kira.GetFileStreamAsync(new FileStreamQuery
        {
            Bucket = BucketName,
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
        await _s3Kira.DeleteFileAsync(new DeleteFileCommand
        {
            Bucket = BucketName,
            FileName = fileName
        },
        _cancellation);
        
        // delete bucket
        await _s3Kira.DeleteBucketAsync(BucketName, _cancellation);
    }
}