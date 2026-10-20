using System.Text;
using OobeRadio.Audio;
using OobeRadio.Core;

internal static class Program
{
    private static int count;
    private static async Task<int> Main(string[] args)
    {
        try
        {
            Check("restores exact original volume", () =>
            {
                var fake = new FakeMusic { MusicLevel = 37 };
                var lease = new MusicLease(fake);
                lease.Acquire(); Equal(0, fake.MusicLevel); Equal(37, fake.RecoveryLevel);
                True(fake.SavedBeforeMute);
                lease.Release(); Equal(37, fake.MusicLevel); Equal(null, fake.RecoveryLevel);
            });
            Check("keeps initially muted music muted", () =>
            {
                var fake = new FakeMusic { MusicLevel = 0 };
                var lease = new MusicLease(fake);
                lease.Acquire(); lease.Release(); Equal(0, fake.MusicLevel);
            });
            Check("double acquire does not overwrite original volume", () =>
            {
                var fake = new FakeMusic { MusicLevel = 42 };
                var lease = new MusicLease(fake);
                lease.Acquire(); lease.Acquire(); lease.Release(); lease.Release();
                Equal(42, fake.MusicLevel);
            });
            Check("new music preference is restored on stop", () =>
            {
                var fake = new FakeMusic { MusicLevel = 31 };
                var lease = new MusicLease(fake);
                fake.Changed = lease.OnMusicSettingChanged;
                lease.Acquire(); fake.MusicLevel = 68;
                Equal(0, fake.MusicLevel); lease.Release(); Equal(68, fake.MusicLevel);
            });
            Check("recovers volume after a crash", () =>
            {
                var fake = new FakeMusic { MusicLevel = 0, RecoveryLevel = 64 };
                new MusicLease(fake).RecoverPreviousSession();
                Equal(64, fake.MusicLevel); Equal(null, fake.RecoveryLevel);
            });
            Check("crash recovery preserves an already changed setting", () =>
            {
                var fake = new FakeMusic { MusicLevel = 27, RecoveryLevel = 64 };
                new MusicLease(fake).RecoverPreviousSession(); Equal(27, fake.MusicLevel);
            });
            Check("ICY metadata never reaches the MP3 decoder", () =>
            {
                byte[] input = Encoding.ASCII.GetBytes("abc").Concat(new byte[] { 1 })
                    .Concat(Enumerable.Repeat((byte)'M', 16)).Concat(Encoding.ASCII.GetBytes("def"))
                    .Concat(new byte[] { 0 }).Concat(Encoding.ASCII.GetBytes("ghi")).ToArray();
                using var reader = new IcyAudioStream(new MemoryStream(input), 3);
                using var result = new MemoryStream();
                reader.CopyTo(result, 2); Equal("abcdefghi", Encoding.ASCII.GetString(result.ToArray()));
                Equal(9L, reader.Position); True(!reader.CanSeek);
            });
            Check("incomplete ICY metadata is rejected", () =>
            {
                using var reader = new IcyAudioStream(new MemoryStream(new byte[] { 65, 1, 0 }), 1);
                Equal(65, reader.ReadByte());
                Throws<EndOfStreamException>(() => reader.ReadByte());
            });
            Check("stalled network reads time out", () =>
            {
                using var reader = new TimedReadStream(new StalledStream(), CancellationToken.None, TimeSpan.FromMilliseconds(60));
                Throws<IOException>(() => reader.ReadByte());
            });
            Check("forward-only audio supplies decoder byte positions", () =>
            {
                using var reader = new TimedReadStream(new MemoryStream(new byte[] { 1, 2, 3 }),
                    CancellationToken.None, TimeSpan.FromSeconds(1));
                Equal(0L, reader.Position); Equal(1, reader.ReadByte()); Equal(1L, reader.Position);
                Equal(2, reader.Read(new byte[10], 0, 10)); Equal(3L, reader.Position);
                True(!reader.CanSeek); Throws<NotSupportedException>(() => reader.Position = 0);
            });
            Check("stopping cancels an outstanding network read", () =>
            {
                using var cancellation = new CancellationTokenSource(60);
                using var reader = new TimedReadStream(new StalledStream(), cancellation.Token, TimeSpan.FromSeconds(20));
                Throws<OperationCanceledException>(() => reader.ReadByte());
            });

            if (args.Length == 2 && args[0] == "--audio")
            {
                await Task.Run(() => TestOutput(args[1]));
                Console.WriteLine("PASS: OpenAL queued buffers advanced on an isolated worker context."); count++;
            }
            if (args.Length == 2 && args[0] == "--live")
            {
                await TestLive(args[1]);
                Console.WriteLine("PASS: live station decoded and streamed; Stop canceled the worker and restored music."); count++;
            }
            Console.WriteLine($"All {count} checks passed."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void TestOutput(string directory)
    {
        using var output = new OpenAlOutput(directory);
        for (int i = 0; i < OpenAlOutput.BufferCount; i++) output.Queue(new PcmChunk(new short[8192], 48000, 2));
        output.SetGain(0); output.Play();
        var end = DateTime.UtcNow.AddSeconds(2);
        while (output.QueuedBuffers == OpenAlOutput.BufferCount && DateTime.UtcNow < end)
        { Thread.Sleep(30); output.Reclaim(); }
        True(output.FreeBuffers > 0);
    }

    private static async Task TestLive(string directory)
    {
        using var player = new RadioPlayer(directory);
        var fake = new FakeMusic { MusicLevel = 53 };
        var lease = new MusicLease(fake);
        player.SetGain(0); // Test never emits audible music.
        player.Start(false); // Verify the supplied HTTPS endpoint specifically.
        var end = DateTime.UtcNow.AddSeconds(35);
        DateTime? played = null;
        try
        {
            while (DateTime.UtcNow < end)
            {
                while (player.Notices.TryDequeue(out var notice))
                {
                    if (notice.Session != player.Session) { notice.BeforePlayback?.TrySetResult(false); continue; }
                    Console.WriteLine("LIVE " + notice.State + (notice.Detail.Length > 0 ? ": " + notice.Detail : ""));
                    if (notice.State == RadioState.Playing)
                    { lease.Acquire(); notice.BeforePlayback?.TrySetResult(true); played = DateTime.UtcNow; }
                    else { lease.Release(); played = null; }
                    if (notice.State == RadioState.Error) throw new Exception("Live playback failed: " + notice.Detail);
                }
                if (played.HasValue && DateTime.UtcNow - played.Value > TimeSpan.FromSeconds(4)) break;
                await Task.Delay(20);
            }
            True(played.HasValue && DateTime.UtcNow - played.Value > TimeSpan.FromSeconds(4));
            Equal(0, fake.MusicLevel);
        }
        finally
        {
            player.Stop(); lease.Release();
            await player.Completion.WaitAsync(TimeSpan.FromSeconds(4));
        }
        Equal(53, fake.MusicLevel);
    }

    private static void Check(string label, Action test) { test(); count++; Console.WriteLine("PASS: " + label); }
    private static void True(bool value) { if (!value) throw new Exception("Assertion failed."); }
    private static void Equal<T>(T expected, T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}."); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private sealed class FakeMusic : IMusicSettings
    {
        private int volume;
        public Action<int>? Changed;
        public int MusicLevel { get => volume; set { volume = value; Changed?.Invoke(value); } }
        public int? RecoveryLevel { get; set; }
        public bool SavedBeforeMute;
        public void SaveRecovery() { if (volume == RecoveryLevel) SavedBeforeMute = true; }
        public void RefreshTrackVolume() { }
    }

    private sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
    }
}
