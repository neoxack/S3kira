using System.Security.Cryptography;
using System.Text;
using s3kira.Utils;

namespace s3kira;

internal sealed class Signature
{
    private readonly string _region;
    private readonly byte[] _secretKey;
    private readonly string _scope;
    private readonly string _service;

    public Signature(string secretKey, string region, string service)
    {
        _region = region;
        _secretKey = Encoding.UTF8.GetBytes($"AWS4{secretKey}");
        _scope = $"/{region}/{service}/aws4_request\n";
        _service = service;
    }

    public string Calculate(HttpRequestMessage request, HeaderValues headerValues,
        string payloadHash, DateTime requestDate)
    {
        var builder = new ValueStringBuilder(stackalloc char[512]);

        AppendStart(ref builder, requestDate);
        AppendRequestHash(ref builder, request, headerValues, payloadHash);

        Span<byte> signature = stackalloc byte[32];
        CreateSigningKey(ref signature, requestDate);

        signature = signature[..SignContent(ref signature, signature, builder.AsReadonlySpan())];

        return StringUtils.ToHexString(signature);
    }

    private static void AppendHeaders(ref ValueStringBuilder builder, HeaderValues headerValues)
    {
        builder.Append(Headers.Host);
        builder.Append(':');
        builder.Append(headerValues.Host);
        builder.Append(NewLine);
        
        builder.Append(Headers.XAmzContentSha);
        builder.Append(':');
        builder.Append(headerValues.XAmzContentSha);
        builder.Append(NewLine);
        
        builder.Append(Headers.XAmzDate);
        builder.Append(':');
        builder.Append(headerValues.XAmzDate);
        builder.Append(NewLine);
        
        builder.Append(NewLine);
        builder.Append(Headers.SignatureHeadersStr);
    }

    private static void AppendQueryParameters(ref ValueStringBuilder builder, string? query)
    {
        if (string.IsNullOrEmpty(query) || query == "?") return;

        var scanIndex = 0;
        if (query[0] == '?') scanIndex = 1;

        var textLength = query.Length;
        var equalIndex = query.IndexOf('=');
        if (equalIndex == -1) equalIndex = textLength;

        while (scanIndex < textLength)
        {
            var delimiter = query.IndexOf('&', scanIndex);
            if (delimiter == -1) delimiter = textLength;

            if (equalIndex < delimiter)
            {
                while (scanIndex != equalIndex && char.IsWhiteSpace(query[scanIndex])) ++scanIndex;
                
                builder.Append(query.AsSpan(scanIndex, equalIndex - scanIndex));
                builder.Append('=');
                
                builder.Append(query.AsSpan(equalIndex + 1, delimiter - equalIndex - 1));
                builder.Append('&');

                equalIndex = query.IndexOf('=', delimiter);
                if (equalIndex == -1) equalIndex = textLength;
            }
            else
            {
                if (delimiter > scanIndex)
                {
                    builder.Append(query.AsSpan(scanIndex, delimiter - scanIndex));
                    builder.Append('=');
                    builder.Append('&');
                }
            }

            scanIndex = delimiter + 1;
        }

        builder.RemoveLast();
    }
    
    private const char NewLine = '\n';

    private static void AppendRequestHash(ref ValueStringBuilder builder, HttpRequestMessage request,
        HeaderValues headerValues, string payloadHash)
    {
        var canonical = new ValueStringBuilder(stackalloc char[512]);
        var uri = request.RequestUri!;
        
        canonical.Append(request.Method.Method);
        canonical.Append(NewLine);
        canonical.Append(uri.AbsolutePath);
        canonical.Append(NewLine);

        AppendQueryParameters(ref canonical, uri.Query);
        canonical.Append(NewLine);

        AppendHeaders(ref canonical, headerValues);
        canonical.Append(NewLine);
        
        canonical.Append(payloadHash);
        
        builder.Append(HashUtils.GetSha256(canonical.AsReadonlySpan()));
    }

    private void AppendStart(ref ValueStringBuilder builder, DateTime requestDate)
    {
        builder.Append("AWS4-HMAC-SHA256\n");
        builder.Append(requestDate, Formats.Iso8601DateTime);
        builder.Append("\n");
        builder.Append(requestDate, Formats.Iso8601Date);
        builder.Append(_scope);
    }
    
    private void CreateSigningKey(ref Span<byte> buffer, DateTime requestDate)
    {
        Span<char> dateBuffer = stackalloc char[16];

        Sign(ref buffer, _secretKey, dateBuffer[..StringUtils.FormatDate(ref dateBuffer, requestDate, Formats.Iso8601Date)]);
        Sign(ref buffer, buffer, _region);
        Sign(ref buffer, buffer, _service);
        Sign(ref buffer, buffer, "aws4_request");
    }

    private static void Sign(ref Span<byte> buffer, ReadOnlySpan<byte> key, scoped ReadOnlySpan<char> content)
    {
        Span<byte> byteBuffer = stackalloc byte[32];
        var encoded = Encoding.UTF8.GetBytes(content, byteBuffer);
        HMACSHA256.TryHashData(key, byteBuffer[..encoded], buffer, out _);
    }
    
    private static int SignContent(ref Span<byte> buffer, ReadOnlySpan<byte> key, scoped ReadOnlySpan<char> content)
    {
        Span<byte> byteBuffer = stackalloc byte[512];
        var encoded = Encoding.UTF8.GetBytes(content, byteBuffer);
        return HMACSHA256.TryHashData(key, byteBuffer[..encoded], buffer, out var written)
            ? written
            : -1;
    }
}