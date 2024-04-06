using BenchmarkDotNet.Attributes;
using Minio;
using Minio.DataModel.Args;

namespace s3kira.Benchmark;

[MemoryDiagnoser]
public class UploadFileBenchmark
{
    private S3Kira _s3Kira = null!;
    private IMinioClient _minioClient = null!;
    private CancellationToken _cancellation;
    private readonly MemoryStream _memoryStream = new MemoryStream();
    private long _size;
    
    private const string FileName = "AST_LM_80_music_loop_embers_A.wav";
    private const string ContentType = "audio/wav";
    private const string Bucket = "s3kira";
    
    
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
        
        var path = Path.Combine("files", FileName);
        var fi = new FileInfo(path);
        _size = fi.Length;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            fs.CopyTo(_memoryStream);
        }
        _memoryStream.Seek(0, SeekOrigin.Begin);
    }
    

    [Benchmark]
    public async Task Upload_Then_Delete_File_Minio()
    {
        var args = new PutObjectArgs()
            .WithBucket(Bucket)
            .WithObject(FileName)
            .WithContentType(ContentType)
            .WithStreamData(_memoryStream)
            .WithObjectSize(_size);
        
        await _minioClient.PutObjectAsync(args, _cancellation);
        _memoryStream.Seek(0, SeekOrigin.Begin);
        
        await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(Bucket)
            .WithObject(FileName), _cancellation);
    }
    
    [Benchmark(Baseline = true)]
    public async Task Upload_Then_Delete_File_S3Kira()
    {
        await _s3Kira.UploadFileAsync(new UploadFileCommand
        {
            Bucket = Bucket,
            FileName = FileName,
            Size = _size,
            ContentType = ContentType,
            Stream = _memoryStream
        }, _cancellation);

        _memoryStream.Seek(0, SeekOrigin.Begin);
        
        await _s3Kira.DeleteFileAsync(new DeleteFileCommand
        {
            Bucket = Bucket,
            FileName = FileName
        }, _cancellation);
    }
}