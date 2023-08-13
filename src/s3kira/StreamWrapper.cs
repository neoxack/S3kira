﻿namespace s3kira;

internal sealed class StreamWrapper: Stream
{
    public override long Length
    {
        get
        {
            _length ??= _response.Content.Headers.ContentLength ?? _stream.Length;
            return _length.Value;
        }
    }

    private long? _length;
    private readonly Stream _stream;
    private readonly HttpResponseMessage _response;

    public StreamWrapper(HttpResponseMessage response, Stream stream)
    {
        _response = response;
        _stream = stream;
    }

    protected override void Dispose(bool disposing)
    {
        _stream.Dispose();
        _response.Dispose();
    }
    
    public override bool CanRead => _stream.CanRead;

    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => _stream.CanWrite;
    
    public override void Flush() => _stream.Flush();
    
    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _stream.ReadAsync(buffer, offset, count, cancellationToken);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _stream.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
    public override void SetLength(long value) => _stream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);
}