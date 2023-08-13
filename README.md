# S3Kira 🪓


S3Kira is a lightweight C# library that provides a simple interface to interact with Amazon S3-like services. The library supports basic operations such as creating, deleting, and managing buckets and files.

## Features

- Simple interface for managing buckets and files
- Low memory footprint

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

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.