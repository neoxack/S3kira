using System.Globalization;

namespace s3kira.Utils;

internal ref struct ValueStringBuilder
{
    private Span<char> _buffer;
    private int _length;

    public ValueStringBuilder(Span<char> buffer)
    {
        _buffer = buffer;
        _length = 0;
    }

    public void Append(char c)
    {
        var pos = _length;
        if ((uint)pos < (uint)_buffer.Length)
        {
            _buffer[pos] = c;
            _length = pos + 1;
        }
        else
            ThrowDoesNotFit();
    }

    public void Append(int value)
    {
        Span<char> buffer = stackalloc char[10];
        var pos = _length;
        if (value.TryFormat(buffer, out var written, default, NumberFormatInfo.InvariantInfo))
        {
            if (pos > _buffer.Length - written)
                ThrowDoesNotFit();
            buffer.CopyTo(_buffer[pos..]);

            _length = pos + written;
        }
        else 
            CantFormatToString(value);
    }

    public void Append(DateTime value, string format)
    {
        Span<char> buffer = stackalloc char[16];
        var pos = _length;
        if (value.TryFormat(buffer, out var written, format, CultureInfo.InvariantCulture))
        {
            if (pos > _buffer.Length - written) 
                ThrowDoesNotFit();
            buffer.CopyTo(_buffer[pos..]);

            _length = pos + written;
        }
        else 
            CantFormatToString(value);
    }
    
    public void Append(string s)
    {
        var pos = _length;
        if (s.Length == 1 && (uint)pos < (uint)_buffer.Length)
        {
            _buffer[pos] = s[0];
            _length = pos + 1;
        }
        else 
            Append(s.AsSpan());
    }
    
    public void Append(scoped ReadOnlySpan<char> value)
    {
        var pos = _length;
        var valueLength = value.Length;

        if (pos > _buffer.Length - valueLength) 
            ThrowDoesNotFit();
        value.CopyTo(_buffer[pos..]);

        _length = pos + valueLength;
    }

    private static void CantFormatToString<T>(T value) where T : struct
    {
        throw new ArgumentException($"Can't format '{value}' to string");
    }

    private static void ThrowDoesNotFit()
    {
        throw new ArgumentException($"Value does not fit");
    }
    
    public readonly ReadOnlySpan<char> AsReadonlySpan() => _buffer[.._length];
    
    public readonly string Flush()
    {
        var result = _length == 0
            ? string.Empty
            : _buffer[.._length].ToString();
        return result;
    }

    public void RemoveLast() => _length--;
    
    public readonly override string ToString() => _length == 0
        ? string.Empty
        : _buffer[.._length].ToString();
    
}