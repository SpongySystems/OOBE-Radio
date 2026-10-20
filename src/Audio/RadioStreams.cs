namespace OobeRadio.Audio;

// NLayer reads synchronously. This wrapper uses cancellable async HTTP reads
// underneath, so a dead station cannot strand a worker thread or hold music mute.
public sealed class TimedReadStream(Stream inner, CancellationToken cancellation,
    TimeSpan timeout) : Stream
{
    private long position;
    public override int Read(byte[] buffer, int offset, int count)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        budget.CancelAfter(timeout);
        try
        {
            int read = inner.ReadAsync(buffer.AsMemory(offset, count), budget.Token)
                .AsTask().GetAwaiter().GetResult();
            position += read;
            return read;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new IOException("No audio bytes arrived before the stream timeout.");
        }
    }

    public override int Read(Span<byte> buffer)
    {
        var temporary = new byte[buffer.Length];
        int read = Read(temporary, 0, temporary.Length);
        temporary.AsSpan(0, read).CopyTo(buffer);
        return read;
    }

    public override int ReadByte()
    {
        byte[] one = new byte[1];
        return Read(one, 0, 1) == 0 ? -1 : one[0];
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    // NLayer checks Position even on a non-seekable stream. A byte counter
    // provides it without introducing seeking or an unbounded history buffer.
    public override long Position { get => position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    // The HTTP response owns inner; disposing it elsewhere also aborts reads.
}

// Some Icecast/Shoutcast servers interleave ICY metadata with MP3 bytes even
// when metadata was not requested. Strip it before handing bytes to the decoder.
public sealed class IcyAudioStream(Stream inner, int interval) : Stream
{
    private int remaining = interval > 0 ? interval : throw new ArgumentOutOfRangeException(nameof(interval));
    private readonly byte[] scratch = new byte[4080];
    private long position;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0) return 0;
        if (remaining == 0)
        {
            int size = inner.ReadByte();
            if (size < 0) return 0;
            int bytes = size * 16;
            while (bytes > 0)
            {
                int read = inner.Read(scratch, 0, Math.Min(bytes, scratch.Length));
                if (read == 0) throw new EndOfStreamException("Incomplete ICY metadata.");
                bytes -= read;
            }
            remaining = interval;
        }
        int result = inner.Read(buffer, offset, Math.Min(count, remaining));
        remaining -= result;
        position += result;
        return result;
    }

    public override int ReadByte()
    {
        byte[] one = new byte[1];
        return Read(one, 0, 1) == 0 ? -1 : one[0];
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
