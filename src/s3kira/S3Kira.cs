using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using s3kira.Utils;

namespace s3kira;

public sealed class S3Kira: IDisposable
{
    private const string Service = "s3";

    private readonly HttpClient _httpClient;
    private readonly S3KiraSettings _settings;
    private readonly Signature _signature;
    
    private readonly string _authHeaderStart;
    private readonly string _authHeaderEnd;

    private readonly Uri _baseUri;


    public S3Kira(S3KiraSettings settings) : this(settings,
        new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        })
    )
    {
    }

    public S3Kira(S3KiraSettings settings, HttpClient httpClient)
    {
        if (string.IsNullOrEmpty(settings.SecretKey))
        {
            throw new ArgumentException("Invalid SecretKey", nameof(settings));
        }
        
        if (string.IsNullOrEmpty(settings.AccessKey))
        {
            throw new ArgumentException("Invalid AccessKey", nameof(settings));
        }
        
        var scheme = settings.UseHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        var baseUriString = $"{scheme}://{settings.Endpoint}";
        if (!Uri.TryCreate(baseUriString, UriKind.Absolute, out var baseUri))
        {
            throw new ArgumentException("Invalid Endpoint", nameof(settings));
        }
        _baseUri = baseUri;
        
        _settings = settings;
        _httpClient = httpClient;
        _signature = new Signature(settings.SecretKey, settings.Region, Service);
        _authHeaderStart = $"AWS4-HMAC-SHA256 Credential={settings.AccessKey}/";
        _authHeaderEnd = $"/{settings.Region}/{Service}/aws4_request, SignedHeaders={Headers.SignatureHeadersStr}, Signature=";
    }

    public async Task<bool> IsBucketExistsAsync(string bucket, CancellationToken cancellation)
    {
        using (var request = CreateBucketHttpRequest(HttpMethod.Head, bucket))
        {
            var response = await SendHttpRequest(request, cancellation);
            using (response)
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        return true;
                    case HttpStatusCode.NotFound:
                        return false;
                    default:
                        throw Error(response);
                }
            }
        }
    }

    public async Task CreateBucketAsync(string bucket, CancellationToken cancellation)
    {
        using (var request = CreateBucketHttpRequest(HttpMethod.Put, bucket))
        {
            var response = await SendHttpRequest(request, cancellation);
            using (response)
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                    case HttpStatusCode.Conflict:
                        return;
                    default:
                        throw Error(response);
                }
            }
        }
    }
    
    public Task DeleteBucketAsync(string bucket, CancellationToken cancellation)
    {
        return DeleteBucketAsync(bucket, false, cancellation);
    }

    public async Task DeleteBucketAsync(string bucket, bool minioForceDelete, CancellationToken cancellation)
    {
        using (var request = CreateDeleteBucketHttpRequest(bucket, minioForceDelete))
        {
            var response = await SendHttpRequest(request, cancellation);
            using (response)
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                    case HttpStatusCode.NoContent:
                    case HttpStatusCode.NotFound:
                        return;
                    default:
                        throw Error(response);
                }
            }
        }
    }

    private const int BasicUploadFileSizeLimit = 5 * 1024 * 1024; // 5 MB
    
    public Task UploadFileAsync(UploadFileCommand command, CancellationToken cancellation)
    {
        if (command.Size > BasicUploadFileSizeLimit)
            return MultipartUploadFileAsync(command, cancellation);
        
        return BasicUploadFileAsync(command, cancellation);
    }
    
    private async Task BasicUploadFileAsync(UploadFileCommand command, CancellationToken cancellation)
    {
        var pool = ArrayPool<byte>.Shared;
        var buffer = pool.Rent((int) command.Size);
        try
        {
            var dataSize = await command.Stream.ReadAsync(buffer, cancellation);
            using (var request = CreateUploadFileHttpRequest(command.Bucket, command.FileName, command.ContentType, buffer, dataSize))
            {
                var response = await SendHttpRequest(request, cancellation);
                using (response)
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw Error(response);
                    }
                }
            }
        }
        finally
        {
            pool.Return(buffer);
        }
    }
    
    private async Task MultipartUploadFileAsync(UploadFileCommand command, CancellationToken cancellation)
    {
        var uploadId = await MultipartStartAsync(command.Bucket, command.FileName, command.ContentType, cancellation);
        if (string.IsNullOrEmpty(uploadId))
            throw new FormatException("Can't read UploadId");
        
        var bufferPool = ArrayPool<byte>.Shared;
        var stringPool = ArrayPool<string>.Shared;

        var etags = stringPool.Rent((int)(command.Size / BasicUploadFileSizeLimit));
        var buffer = bufferPool.Rent(BasicUploadFileSizeLimit);
        
        try
        {
            var partNumber = 0;
            while (command.Stream.Position < command.Size)
            {
                partNumber++;
                try
                {
                    var chunkSize = await command.Stream.ReadAsync(buffer, cancellation);
                    var etag = await UploadPartAsync(command.Bucket, command.FileName, uploadId, partNumber, buffer,
                        chunkSize, cancellation);
                    etags[partNumber - 1] = etag;
                }
                catch (Exception)
                {
                    await MultipartAbortAsync(command.Bucket, command.FileName, uploadId, cancellation);
                    throw;
                }
            }

            try
            {
                await MultipartCompleteAsync(command.Bucket, command.FileName, uploadId, etags, partNumber,
                    cancellation);
            }
            catch (Exception)
            {
                await MultipartAbortAsync(command.Bucket, command.FileName, uploadId, cancellation);
                throw;
            }
        }
        finally
        {
            bufferPool.Return(buffer);
            stringPool.Return(etags);
        }
    }

    private async Task MultipartAbortAsync(string bucket, string fileName, string uploadId, CancellationToken cancellation)
    {
        using (var request = CreateMultipartAbortHttpRequest(bucket, fileName, uploadId))
        {
            var response = await SendHttpRequest(request, cancellation);
            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw Error(response);
                }
            }
        }
    }

    private async Task MultipartCompleteAsync(string bucket, string fileName, string uploadId, string[] etags, int etagsCount, CancellationToken cancellation)
    {
        using (var request = CreateMultipartCompleteHttpRequest(bucket, fileName, uploadId, etags, etagsCount))
        {
            var response = await SendHttpRequest(request, cancellation);
            using (response)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw Error(response);
                }
            }
        }
    }

    private async Task<string> UploadPartAsync(string bucket, string fileName, string uploadId, int partNumber,
        byte[] buffer, int chunkSize, CancellationToken cancellation)
    {
        using (var request = CreateUploadPartHttpRequest(bucket, fileName, uploadId, partNumber, buffer, chunkSize))
        {
            var response = await SendHttpRequest(request, cancellation);
            using (response)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw Error(response);
                }
                return response.Headers.ETag!.Tag;
            }
        }
    }

    private async Task<string> MultipartStartAsync(string bucket, string fileName, string contentType, CancellationToken cancellation)
    {
        using (var request = CreateMultipartStartHttpRequest(bucket, fileName, contentType))
        {
            var response = await SendHttpRequest(request, cancellation);
            using (response)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw Error(response);
                }
                
                await using (var stream = await response.Content.ReadAsStreamAsync(cancellation))
                {
                    return XmlStreamReader.ReadValue(stream, "UploadId");
                }
            }
        }
    }

    public async Task DeleteFileAsync(DeleteFileCommand command, CancellationToken cancellation)
    {
        using (var request = CreateFileHttpRequest(HttpMethod.Delete, command.Bucket, command.FileName))
        {
            var response = await SendHttpRequest(request, cancellation);
            using (response)
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.NoContent:
                        return;
                    default:
                        throw Error(response);
                }
            }
        }
    }
    
    public async Task<bool> IsFileExistsAsync(FileExistsQuery query, CancellationToken cancellation)
    {
        using (var request = CreateFileHttpRequest(HttpMethod.Head, query.Bucket, query.FileName))
        {
            var response = await SendHttpRequest(request, cancellation);
            using (response)
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        return true;
                    case HttpStatusCode.NotFound:
                        return false;
                    default:
                        throw Error(response);
                }
            }
        }
    }
    
    public async Task<Stream> GetFileStreamAsync(FileStreamQuery query, CancellationToken cancellation)
    {
        HttpResponseMessage response;
        using (var request = CreateFileHttpRequest(HttpMethod.Get, query.Bucket, query.FileName))
        {
            response = await SendHttpRequest(request, cancellation);
        }
        
        var needDispose = true;
        try
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var stream = await response.Content.ReadAsStreamAsync(cancellation);
                    needDispose = false;
                    return new StreamWrapper(response, stream);
                default:
                    throw Error(response);
            }
        }
        finally
        {
            if(needDispose)
                response.Dispose();
        }
    }

    private async Task<HttpResponseMessage> SendHttpRequest(HttpRequestMessage request,
        CancellationToken cancellation)
    {
        try
        {
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            return response;
        }
        finally
        {
            request.Content?.Dispose();
        }
    }
    
    private static HttpRequestException Error(HttpResponseMessage response)
    {
        var reason = response.ReasonPhrase ?? response.ToString();
        var exception = new HttpRequestException("Storage has returned an unexpected result: " +
                                                 $"{response.StatusCode} ({reason})");
        return exception;
    }

    private Uri CreateUri(string bucketName)
    {
        return new Uri(_baseUri, bucketName);
    }
    
    private Uri CreateUri(string bucket, string fileName)
    {
        var urlBuilder = new ValueStringBuilder(stackalloc char[512]);
        urlBuilder.Append(bucket);
        urlBuilder.Append('/');
        StringUtils.AppendUrlEncoded(ref urlBuilder, fileName);
        return new Uri(_baseUri, urlBuilder.Flush());
    }
    
    private Uri CreateMultipartStartUri(string bucket, string fileName)
    {
        var urlBuilder = new ValueStringBuilder(stackalloc char[512]);
        urlBuilder.Append(bucket);
        urlBuilder.Append('/');
        StringUtils.AppendUrlEncoded(ref urlBuilder, fileName);
        urlBuilder.Append("?uploads");
        return new Uri(_baseUri, urlBuilder.Flush());
    }

    private Uri CreateMultipartUri(string bucket, string fileName, string uploadId, int partNumber = 0)
    {
        var urlBuilder = new ValueStringBuilder(stackalloc char[512]);
        urlBuilder.Append(bucket);
        urlBuilder.Append('/');
        StringUtils.AppendUrlEncoded(ref urlBuilder, fileName);
        if (partNumber > 0)
        {
            urlBuilder.Append("?partNumber=");
            urlBuilder.Append(partNumber);
            urlBuilder.Append("&uploadId=");
            urlBuilder.Append(uploadId);
        }
        else
        {
            urlBuilder.Append("?uploadId=");
            urlBuilder.Append(uploadId);
        }
        return new Uri(_baseUri, urlBuilder.Flush());
    }

    private HttpRequestMessage CreateBucketHttpRequest(HttpMethod method, string bucket)
    {
        var uri = CreateUri(bucket);
        return CreateHttpRequestMessage(method, uri, HashUtils.EmptyPayloadHash);
    }
    
    private HttpRequestMessage CreateDeleteBucketHttpRequest(string bucket, bool minioForceDelete)
    {
        var uri = CreateUri(bucket);
        var httpRequest = CreateHttpRequestMessage(HttpMethod.Delete, uri, HashUtils.EmptyPayloadHash);
        if (minioForceDelete)
        {
            httpRequest.Headers.TryAddWithoutValidation(Headers.MinioForceDelete, "true");
        }
        return httpRequest;
    }
    
    private HttpRequestMessage CreateFileHttpRequest(HttpMethod method, string bucket, string fileName)
    {
        var uri = CreateUri(bucket, fileName);
        return CreateHttpRequestMessage(method, uri, HashUtils.EmptyPayloadHash);
    }
    
    private HttpRequestMessage CreateUploadFileHttpRequest(string bucket, string fileName, string contentType, byte[] buffer, int size)
    {
        var uri = CreateUri(bucket, fileName);
        var payloadHash = HashUtils.GetSha256(buffer.AsSpan(0, size));
        var httpRequest = CreateHttpRequestMessage(HttpMethod.Put, uri, payloadHash);
        var content = new ByteArrayContent(buffer, 0, size);
        content.Headers.TryAddWithoutValidation(Headers.ContentType, contentType);
        httpRequest.Content = content;
        return httpRequest;
    }
    
    private HttpRequestMessage CreateMultipartStartHttpRequest(string bucket, string fileName, string contentType)
    {
        var uri = CreateMultipartStartUri(bucket, fileName);
        var httpRequest = CreateHttpRequestMessage(HttpMethod.Post, uri, HashUtils.EmptyPayloadHash);
        httpRequest.Headers.TryAddWithoutValidation(Headers.ContentType, contentType);
        return httpRequest;
    }
    
    private HttpRequestMessage CreateUploadPartHttpRequest(string bucket, string fileName, string uploadId, int partNumber, byte[] buffer, int size)
    {
        var uri = CreateMultipartUri(bucket, fileName, uploadId, partNumber);
        var payloadHash = HashUtils.GetSha256(buffer.AsSpan(0, size));
        var httpRequest = CreateHttpRequestMessage(HttpMethod.Put, uri, payloadHash);
        var content = new ByteArrayContent(buffer, 0, size);
        content.Headers.TryAddWithoutValidation(Headers.ContentLength, size.ToString(NumberFormatInfo.InvariantInfo));
        httpRequest.Content = content;
        return httpRequest;
    }
    
    private HttpRequestMessage CreateMultipartAbortHttpRequest(string bucket, string fileName, string uploadId)
    {
        var uri = CreateMultipartUri(bucket, fileName, uploadId);
        return CreateHttpRequestMessage(HttpMethod.Delete, uri, HashUtils.EmptyPayloadHash);
    }
    
    private HttpRequestMessage CreateMultipartCompleteHttpRequest(string bucket, string fileName, string uploadId, string[] etagsBuffer, int etagsCount)
    {
        var builder = new StringBuilder(2048);
        builder.Append("<CompleteMultipartUpload>");
        for (var i = 0; i < etagsBuffer.Length; i++)
        {
            if (i == etagsCount) break;
            
            builder.Append("<Part>");
            builder.Append("<PartNumber>");
            builder.Append(i + 1);
            builder.Append("</PartNumber>");
            builder.Append("<ETag>");
            builder.Append(etagsBuffer[i]);
            builder.Append("</ETag>");
            builder.Append("</Part>");
        }
        builder.Append("</CompleteMultipartUpload>");
        var data = builder.ToString();

        var uri = CreateMultipartUri(bucket, fileName, uploadId);
        var payloadHash = HashUtils.GetSha256(data);
        var httpRequest = CreateHttpRequestMessage(HttpMethod.Post, uri, payloadHash);
        var content = new StringContent(data, Encoding.UTF8);
        httpRequest.Content = content;
        return httpRequest;
    }

    private HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, Uri uri, string payloadHash)
    {
        var request = new HttpRequestMessage(method, uri);
        var now = DateTime.UtcNow;
        var headerValues = GetHeaderValues(payloadHash, now);

        var headers = request.Headers;
        headers.TryAddWithoutValidation(Headers.Host, headerValues.Host);
        headers.TryAddWithoutValidation(Headers.XAmzContentSha, headerValues.XAmzContentSha);
        headers.TryAddWithoutValidation(Headers.XAmzDate, headerValues.XAmzDate);
        
        var signature = _signature.Calculate(request, headerValues, payloadHash, now);
        headers.TryAddWithoutValidation(Headers.Authorization, BuildAuthorizationHeader(now, signature));

        return request;
    }

    private HeaderValues GetHeaderValues(string payloadHash, DateTime now)
    {
        return new HeaderValues(_settings.Endpoint, payloadHash, now.ToString(Formats.Iso8601DateTime, CultureInfo.InvariantCulture));
    }

    private string BuildAuthorizationHeader(DateTime now, ReadOnlySpan<char> signature)
    {
        var builder = new ValueStringBuilder(stackalloc char[512]);
        builder.Append(_authHeaderStart);
        builder.Append(now, Formats.Iso8601Date);
        builder.Append(_authHeaderEnd);
        builder.Append(signature);
        return builder.Flush();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}