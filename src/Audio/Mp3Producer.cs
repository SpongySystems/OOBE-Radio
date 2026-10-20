using System.Net;
using System.Threading.Channels;
using NLayer;

namespace OobeRadio.Audio;

public sealed record PcmChunk(short[] Samples, int SampleRate, int Channels);

public static class Mp3Producer
{
    public static void Run(string url, ChannelWriter<PcmChunk> writer, CancellationToken cancellation)
    {
        try
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            startup.CancelAfter(TimeSpan.FromSeconds(20));
            using var handler = new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(10),
                AutomaticDecompression = DecompressionMethods.None,
                MaxAutomaticRedirections = 5
            };
            using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("OOBE-Radio/1.0.0");
            request.Headers.TryAddWithoutValidation("Icy-MetaData", "0");
            using var response = http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                startup.Token).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            string type = response.Content.Headers.ContentType?.MediaType ?? "";
            if (type.Contains("html", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Station returned a web page instead of MP3 audio.");

            using var raw = response.Content.ReadAsStream(startup.Token);
            using var timed = new TimedReadStream(raw, startup.Token, TimeSpan.FromSeconds(10));
            Stream audio = timed;
            if (response.Headers.TryGetValues("icy-metaint", out var values) &&
                int.TryParse(values.FirstOrDefault(), out int interval) && interval > 0)
                audio = new IcyAudioStream(timed, interval);

            // A forward-only stream is essential: never query Length/Duration or
            // download a never-ending live stream into a file/MemoryStream.
            using var decoder = new MpegFile(audio);
            int channels = decoder.Channels;
            int rate = decoder.SampleRate;
            if (channels is < 1 or > 2 || rate is < 8000 or > 192000)
                throw new IOException("Unsupported MP3 channel count or sample rate.");
            float[] floats = new float[4096 * channels];
            bool first = true;
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                int count = decoder.ReadSamples(floats, 0, floats.Length);
                if (count == 0) throw new EndOfStreamException("The live MP3 connection ended.");
                if (count % channels != 0) throw new IOException("Incomplete PCM audio frame.");
                short[] samples = new short[count];
                for (int i = 0; i < count; i++)
                {
                    float sample = float.IsFinite(floats[i]) ? Math.Clamp(floats[i], -1f, 1f) : 0;
                    samples[i] = (short)Math.Clamp((int)MathF.Round(sample * 32767), short.MinValue, short.MaxValue);
                }
                if (first)
                {
                    startup.CancelAfter(Timeout.InfiniteTimeSpan);
                    first = false;
                }
                writer.WriteAsync(new PcmChunk(samples, rate, channels), cancellation)
                    .AsTask().GetAwaiter().GetResult();
            }
        }
        catch (Exception error) { writer.TryComplete(error); }
        finally { writer.TryComplete(); }
    }
}
