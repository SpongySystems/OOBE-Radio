using System.Collections.Concurrent;
using System.Threading.Channels;

namespace OobeRadio.Audio;

public enum RadioState { Off, Connecting, Buffering, Playing, Reconnecting, Error }

public sealed record RadioNotice(int Session, RadioState State, string Detail = "",
    TaskCompletionSource<bool>? BeforePlayback = null);

public sealed class RadioPlayer(string gameDirectory) : IDisposable
{
    public const string PrimaryUrl = "https://radio.outofbounds.live/listen/out_of_bounds/radio.mp3";
    public const string FallbackUrl = "http://radio.outofbounds.live:8000/radio.mp3";
    public ConcurrentQueue<RadioNotice> Notices { get; } = new();
    public int Session { get; private set; }
    public bool IsRequested { get; private set; }
    public Task Completion => previous;
    private CancellationTokenSource? sessionCancellation;
    private Task previous = Task.CompletedTask;
    private float gain = 0.5f;

    public void SetGain(float value) => Volatile.Write(ref gain, Math.Clamp(value, 0, 1));

    // Start/Stop are called only from the game's main thread.
    public void Start(bool allowHttpFallback)
    {
        if (IsRequested) return;
        IsRequested = true;
        int session = ++Session;
        var cancellation = new CancellationTokenSource();
        sessionCancellation = cancellation;
        Task waitForOldSession = previous;
        previous = Task.Run(async () =>
        {
            try { await waitForOldSession.ConfigureAwait(false); } catch { /* already reported */ }
            try { Run(session, allowHttpFallback, cancellation.Token); }
            finally { cancellation.Dispose(); }
        });
    }

    public void Stop()
    {
        IsRequested = false;
        ++Session; // Invalidate late callbacks from canceled sessions immediately.
        try { sessionCancellation?.Cancel(); } catch (ObjectDisposedException) { }
        sessionCancellation = null;
    }

    public void MarkFailed() { IsRequested = false; }

    private void Run(int session, bool allowFallback, CancellationToken cancellation)
    {
        for (int attempt = 0; attempt < 6 && !cancellation.IsCancellationRequested; attempt++)
        {
            string url = allowFallback && attempt % 2 == 1 ? FallbackUrl : PrimaryUrl;
            Notices.Enqueue(new RadioNotice(session, RadioState.Connecting));
            try
            {
                PlayConnection(session, url, cancellation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                bool last = attempt == 5 || error is AudioDeviceException;
                Notices.Enqueue(new RadioNotice(session, last ? RadioState.Error : RadioState.Reconnecting,
                    error.ToString()));
                if (last) return;
                if (cancellation.WaitHandle.WaitOne(TimeSpan.FromSeconds(Math.Min(2 << attempt, 30)))) return;
            }
        }
    }

    private void PlayConnection(int session, string url, CancellationToken cancellation)
    {
        using var connection = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var queue = Channel.CreateBounded<PcmChunk>(new BoundedChannelOptions(12)
        {
            SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait
        });
        Task producer = Task.Run(() => Mp3Producer.Run(url, queue.Writer, connection.Token));
        try
        {
            if (!queue.Reader.WaitToReadAsync(cancellation).AsTask().GetAwaiter().GetResult())
                throw new IOException("The station did not send any audio.");
            using var output = new OpenAlOutput(gameDirectory);
            Notices.Enqueue(new RadioNotice(session, RadioState.Buffering));
            bool announcedPlaying = false;
            int sampleRate = 0, channels = 0;
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                output.Reclaim();
                while (output.FreeBuffers > 0 && queue.Reader.TryRead(out var chunk))
                {
                    if (sampleRate != 0 && (chunk.SampleRate != sampleRate || chunk.Channels != channels))
                        throw new IOException("The station changed its audio format; reconnecting.");
                    sampleRate = chunk.SampleRate; channels = chunk.Channels;
                    output.Queue(chunk);
                }

                bool playing = output.IsPlaying;
                int queued = output.QueuedBuffers;
                if (announcedPlaying && !playing)
                {
                    announcedPlaying = false;
                    Notices.Enqueue(new RadioNotice(session, RadioState.Buffering));
                }
                if (!playing && queued >= 3)
                {
                    // Main thread mutes game music and ACKs before source playback.
                    var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    Notices.Enqueue(new RadioNotice(session, RadioState.Playing, BeforePlayback: ready));
                    if (!ready.Task.WaitAsync(cancellation).GetAwaiter().GetResult())
                        throw new OperationCanceledException(cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    output.SetGain(Volatile.Read(ref gain));
                    output.Play();
                    announcedPlaying = true;
                }
                else if (playing) output.SetGain(Volatile.Read(ref gain));

                if (queue.Reader.Completion.IsCompleted && queued == 0)
                {
                    queue.Reader.Completion.GetAwaiter().GetResult();
                    throw new EndOfStreamException("The station connection ended.");
                }
                // If the producer ended with fewer than 3 chunks, fail instead of
                // waiting forever for an impossible prebuffer.
                if (!playing && queued < 3 && producer.IsCompleted && !queue.Reader.TryPeek(out _))
                {
                    queue.Reader.Completion.GetAwaiter().GetResult();
                    throw new EndOfStreamException("The station sent too little audio to start.");
                }
                if (cancellation.WaitHandle.WaitOne(20)) cancellation.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            connection.Cancel();
            // Producer performs only cancellable HTTP reads and bounded writes.
            // Waiting here is on a worker; the game UI never waits for network I/O.
            try { producer.GetAwaiter().GetResult(); } catch { }
        }
    }

    public void Dispose() => Stop();
}
