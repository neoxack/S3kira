using System.Buffers;
using System.Globalization;
using System.Text;

namespace s3kira.Utils;

internal static class StringUtils
{
    public static int FormatDate(ref Span<char> buffer, DateTime dateTime, string format)
    {
        return dateTime.TryFormat(buffer, out var written, format, CultureInfo.InvariantCulture)
            ? written
            : -1;
    }
    
    public static string ToHexString(ReadOnlySpan<byte> data)
    {
        return Convert.ToHexString(data).ToLowerInvariant();
    }
    
    private const string ValidUrlCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
    
    public static bool AppendUrlEncoded(ref ValueStringBuilder builder, ReadOnlySpan<char> name)
    {
        var count = Encoding.UTF8.GetByteCount(name);
        var hasEncoded = false;

        var pool = ArrayPool<byte>.Shared;
        var byteBuffer = pool.Rent(count);

        Span<char> charBuffer = stackalloc char[2];
        Span<char> upperBuffer = stackalloc char[2];
        
        var encoded = Encoding.UTF8.GetBytes(name, byteBuffer);

        foreach (var byteSymbol in byteBuffer.AsSpan(0, encoded))
        {
            var symbol = (char) byteSymbol;
            if (ValidUrlCharacters.Contains(symbol, StringComparison.Ordinal))
            {
                builder.Append(symbol);
            }
            else
            {
                builder.Append('%');

                FormatX2(ref charBuffer, symbol);
                MemoryExtensions.ToUpperInvariant(charBuffer, upperBuffer);
                builder.Append(upperBuffer);

                hasEncoded = true;
            }
        }
        
        pool.Return(byteBuffer);

        return hasEncoded;
    }
    
    private static void FormatX2(ref Span<char> buffer, char value)
    {
        ((int) value).TryFormat(buffer, out _, "x2", CultureInfo.InvariantCulture);
    }
}