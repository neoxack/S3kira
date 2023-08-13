using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace s3kira.Tests;

public sealed class S3KiraFactory: IAsyncLifetime
{
    private const string AccessKey = "Test";
    private const string SecretKey = "SecretKey";
    private const ushort MinioPort = 9000;
    private const string HealthEndpoint = "/minio/health/ready";
    
    private IContainer _dockerContainer = null!;
    private S3Kira _s3Kira = null!;

    public S3Kira GetS3Kira() => _s3Kira;

    public async Task InitializeAsync()
    {
        _dockerContainer = new ContainerBuilder()
            .WithName("minio_" + Guid.NewGuid().ToString("D"))
            .WithImage("quay.io/minio/minio")
            .WithPortBinding(MinioPort, true)
            .WithEnvironment(new Dictionary<string, string>
            {
                { "MINIO_ROOT_USER", AccessKey },
                { "MINIO_ROOT_PASSWORD", SecretKey }
            })
            .WithCommand("server", "/data")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPath(HealthEndpoint).ForPort(MinioPort)))
            .Build();

        await _dockerContainer.StartAsync();
        
        _s3Kira = new S3Kira(new S3KiraSettings
        {
            Endpoint = $"{_dockerContainer.Hostname}:{_dockerContainer.GetMappedPublicPort(MinioPort)}",
            AccessKey = AccessKey,
            SecretKey = SecretKey
        });
    }

    public Task DisposeAsync()
    {
        return _dockerContainer.StopAsync();
    }
}