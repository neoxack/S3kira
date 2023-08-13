using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace s3kira.Utils;

internal static class HashUtils
{
    public static readonly string EmptyPayloadHash = GetSha256(string.Empty);
    
    public static string GetSha256(ReadOnlySpan<char> value)
    {
        var count = Encoding.UTF8.GetByteCount(value);
        var pool = ArrayPool<byte>.Shared;
        var buffer = pool.Rent(count);
        var encoded = Encoding.UTF8.GetBytes(value, buffer);
        var result = GetSha256(buffer.AsSpan(0, encoded));
        pool.Return(buffer);
        return result;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetSha256(ReadOnlySpan<byte> data)
    {
        Span<byte> hashBuffer = stackalloc byte[32];
        return SHA256.TryHashData(data, hashBuffer, out _)
            ? StringUtils.ToHexString(hashBuffer)
            : string.Empty;
    }
}