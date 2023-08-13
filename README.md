[![.NET](https://github.com/neoxack/S3kira/actions/workflows/dotnet.yml/badge.svg)](https://github.com/neoxack/S3kira/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/Neoxack.S3Kira.svg)](https://www.nuget.org/packages/Neoxack.S3Kira)
[![NuGet](https://img.shields.io/nuget/dt/Neoxack.S3Kira.svg)](https://www.nuget.org/packages/Neoxack.S3Kira)
[![CodeFactor](https://www.codefactor.io/repository/github/neoxack/s3kira/badge)](https://www.codefactor.io/repository/github/neoxack/s3kira)

# S3Kira 🪓


S3Kira is a lightweight C# library that provides a simple interface to interact with Amazon S3-like services. The library supports basic operations such as creating, deleting, and managing buckets and files.

## Features

- Simple interface for managing buckets and files
- [Low memory footprint](#benchmarks)
- Force delete buckets with files in [Minio S3](https://min.io)
- Configured PooledConnectionLifetime for reflect the DNS or other network changes

## Getting Started

### Prerequisites

- .NET SDK

### Installation

To use S3Kira in your project, add the library as a dependency using your preferred method (e.g., NuGet).

### Usage

Here's an example of how to use S3Kira:

```csharp
using s3kira;
using System.Threading;

var settings = new S3KiraSettings
{
    AccessKey = "your-access-key",
    SecretKey = "your-secret-key",
    Endpoint = "your-s3-endpoint",
    Region = "your-region"
};

var s3Kira = new S3Kira(settings);

// Check if a bucket exists
var bucketExists = await s3Kira.IsBucketExistsAsync("my-bucket", CancellationToken.None);

// Create a new bucket
await s3Kira.CreateBucketAsync("new-bucket", CancellationToken.None);

// Upload a file
using (var fileStream = File.OpenRead("path/to/your/file"))
{
    var uploadFileCommand = new UploadFileCommand
    {
        Bucket = "new-bucket",
        FileName = "your-file-name",
        Size = fileStream.Length,
        ContentType = "your-content-type",
        Stream = fileStream
    };
    await s3Kira.UploadFileAsync(uploadFileCommand, CancellationToken.None);
}
```

### Methods

- IsBucketExistsAsync: Checks if a bucket exists
- CreateBucketAsync: Creates a new bucket
- DeleteBucketAsync: Deletes an existing bucket
- UploadFileAsync: Uploads a file to a specified bucket
- DeleteFileAsync: Deletes a file from a specified bucket
- IsFileExistsAsync: Checks if a file exists in a specified bucket
- GetFileStreamAsync: Retrieves a file stream for a file in a specified bucket

## Benchmarks

```
BenchmarkDotNet v0.13.7, macOS Ventura 13.4.1 (c) (22F770820d) [Darwin 22.5.0]
Apple M2 Pro, 1 CPU, 12 logical and 12 physical cores
.NET SDK 7.0.306
 [Host]     : .NET 7.0.9 (7.0.923.32018), Arm64 RyuJIT AdvSIMD
 DefaultJob : .NET 7.0.9 (7.0.923.32018), Arm64 RyuJIT AdvSIMD
```
| Method       |      Mean |    Error |   StdDev | Ratio | RatioSD |  Allocated | Alloc Ratio |
|--------------|----------:|---------:|---------:|------:|--------:|-----------:|------------:|
| minio-dotnet | 101.78 ms | 1.821 ms | 1.703 ms |  1.20 |    0.02 | 6463.49 KB |      114.26 |
| S3Kira       |  84.79 ms | 1.122 ms | 1.050 ms |  1.00 |    0.00 |   56.57 KB |        1.00 |


## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.