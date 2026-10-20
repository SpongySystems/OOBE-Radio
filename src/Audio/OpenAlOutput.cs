using System.Runtime.InteropServices;

namespace OobeRadio.Audio;

public sealed class AudioDeviceException(string message) : Exception(message);

// All calls, including disposal, belong on ONE worker thread. Never replace
// Vintage Story's process-wide current context with alcMakeContextCurrent.
// OpenAL Soft's thread-local context extension isolates this radio completely.
public sealed class OpenAlOutput : IDisposable
{
    public const int BufferCount = 6;
    private const int SourceRelative = 0x202, Position = 0x1004, Gain = 0x100A;
    private const int Rolloff = 0x1021, SourceState = 0x1010, Playing = 0x1012;
    private const int BuffersQueued = 0x1015, BuffersProcessed = 0x1016;
    private readonly Bindings al;
    private readonly int owner = Environment.CurrentManagedThreadId;
    private IntPtr device, context;
    private uint source;
    private readonly List<uint> buffers = [];
    private readonly Queue<uint> free = new();
    private bool disposed;

    public OpenAlOutput(string gameDirectory)
    {
        al = Bindings.Get(gameDirectory);
        try
        {
            device = al.OpenDevice(IntPtr.Zero);
            if (device == IntPtr.Zero) throw new AudioDeviceException("Could not open the default audio output device.");
            if (al.IsExtensionPresent(device, "ALC_EXT_thread_local_context") == 0)
                throw new AudioDeviceException("The audio library does not support isolated thread contexts (OpenAL Soft required).");
            context = al.CreateContext(device, IntPtr.Zero);
            if (context == IntPtr.Zero || al.SetThreadContext(context) == 0)
                throw new AudioDeviceException("Could not create the radio's audio context.");
            al.GenSources(1, out source);
            al.Sourcei(source, SourceRelative, 1);
            al.Sourcef(source, Rolloff, 0);
            al.Source3f(source, Position, 0, 0, 0);
            al.Sourcef(source, Gain, 0);
            for (int i = 0; i < BufferCount; i++)
            {
                al.GenBuffers(1, out uint buffer);
                buffers.Add(buffer);
                free.Enqueue(buffer);
            }
            Check();
        }
        catch { Dispose(); throw; }
    }

    public int FreeBuffers { get { AssertThread(); return free.Count; } }
    public int QueuedBuffers { get { AssertThread(); al.GetSourcei(source, BuffersQueued, out int value); Check(); return value; } }
    public bool IsPlaying { get { AssertThread(); al.GetSourcei(source, SourceState, out int value); Check(); return value == Playing; } }

    public void Reclaim()
    {
        AssertThread();
        al.GetSourcei(source, BuffersProcessed, out int count);
        while (count-- > 0)
        {
            al.UnqueueBuffers(source, 1, out uint buffer);
            free.Enqueue(buffer);
        }
        Check();
    }

    public void Queue(PcmChunk chunk)
    {
        AssertThread();
        uint buffer = free.Dequeue();
        var pinned = GCHandle.Alloc(chunk.Samples, GCHandleType.Pinned);
        try
        {
            al.BufferData(buffer, chunk.Channels == 1 ? 0x1101 : 0x1103,
                pinned.AddrOfPinnedObject(), checked(chunk.Samples.Length * sizeof(short)), chunk.SampleRate);
        }
        finally { pinned.Free(); }
        al.QueueBuffers(source, 1, ref buffer);
        Check();
    }

    public void SetGain(float value) { AssertThread(); al.Sourcef(source, Gain, Math.Clamp(value, 0, 1)); Check(); }
    public void Play() { AssertThread(); al.SourcePlay(source); Check(); }

    private void Check()
    {
        int code = al.GetError();
        if (code != 0) throw new AudioDeviceException($"OpenAL reported audio error 0x{code:X}.");
    }

    private void AssertThread()
    {
        if (Environment.CurrentManagedThreadId != owner)
            throw new InvalidOperationException("Radio audio output used from the wrong thread.");
    }

    public void Dispose()
    {
        if (disposed) return;
        AssertThread();
        disposed = true;
        if (source != 0)
        {
            al.SourceStop(source);
            al.DeleteSources(1, ref source);
            source = 0;
        }
        foreach (uint id in buffers)
        {
            uint buffer = id;
            al.DeleteBuffers(1, ref buffer);
        }
        if (context != IntPtr.Zero)
        {
            al.SetThreadContext(IntPtr.Zero);
            al.DestroyContext(context);
            context = IntPtr.Zero;
        }
        if (device != IntPtr.Zero) { al.CloseDevice(device); device = IntPtr.Zero; }
    }

    private sealed class Bindings
    {
        private static readonly object sync = new();
        private static Bindings? instance;
        public static Bindings Get(string directory)
        {
            lock (sync) return instance ??= new Bindings(directory);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate IntPtr DeviceOpen(IntPtr name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate byte DeviceClose(IntPtr device);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate IntPtr ContextCreate(IntPtr device, IntPtr attributes);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void ContextDestroy(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate byte ContextSet(IntPtr context);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate byte ExtensionCheck(IntPtr device, [MarshalAs(UnmanagedType.LPStr)] string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate IntPtr ProcGet(IntPtr device, [MarshalAs(UnmanagedType.LPStr)] string name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Gen(int count, out uint value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void Delete(int count, ref uint value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SourceInt(uint source, int parameter, int value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SourceFloat(uint source, int parameter, float value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SourceVector(uint source, int parameter, float x, float y, float z);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SourceGet(uint source, int parameter, out int value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SourceAction(uint source);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void BufferUpload(uint buffer, int format, IntPtr data, int bytes, int frequency);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void BufferQueue(uint source, int count, ref uint buffer);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void BufferUnqueue(uint source, int count, out uint buffer);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ErrorGet();

        public readonly DeviceOpen OpenDevice;
        public readonly DeviceClose CloseDevice;
        public readonly ContextCreate CreateContext;
        public readonly ContextDestroy DestroyContext;
        public readonly ContextSet SetThreadContext;
        public readonly ExtensionCheck IsExtensionPresent;
        public readonly Gen GenSources, GenBuffers;
        public readonly Delete DeleteSources, DeleteBuffers;
        public readonly SourceInt Sourcei;
        public readonly SourceFloat Sourcef;
        public readonly SourceVector Source3f;
        public readonly SourceGet GetSourcei;
        public readonly SourceAction SourcePlay, SourceStop;
        public readonly BufferUpload BufferData;
        public readonly BufferQueue QueueBuffers;
        public readonly BufferUnqueue UnqueueBuffers;
        public readonly ErrorGet GetError;

        private Bindings(string directory)
        {
            string[] names = OperatingSystem.IsWindows() ? ["OpenAL32.dll", "openal32.dll", "soft_oal.dll"]
                : OperatingSystem.IsMacOS() ? ["libopenal.dylib", "libopenal.1.dylib"]
                : ["libopenal.so.1", "libopenal.so"];
            IntPtr handle = IntPtr.Zero;
            foreach (string name in names)
            {
                foreach (string path in new[] { Path.Combine(directory, "Lib", name),
                    Path.Combine(directory, "lib", name), Path.Combine(directory, name), name })
                {
                    if (NativeLibrary.TryLoad(path, out handle)) break;
                }
                if (handle != IntPtr.Zero) break;
            }
            if (handle == IntPtr.Zero)
                throw new AudioDeviceException("Could not load Vintage Story's OpenAL audio library. See the installation guide.");

            T Load<T>(string name) where T : Delegate =>
                Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(handle, name));
            OpenDevice = Load<DeviceOpen>("alcOpenDevice");
            CloseDevice = Load<DeviceClose>("alcCloseDevice");
            CreateContext = Load<ContextCreate>("alcCreateContext");
            DestroyContext = Load<ContextDestroy>("alcDestroyContext");
            IsExtensionPresent = Load<ExtensionCheck>("alcIsExtensionPresent");
            var getProc = Load<ProcGet>("alcGetProcAddress");
            IntPtr setThread = getProc(IntPtr.Zero, "alcSetThreadContext");
            if (setThread == IntPtr.Zero)
                throw new AudioDeviceException("OpenAL Soft thread-local contexts are unavailable.");
            SetThreadContext = Marshal.GetDelegateForFunctionPointer<ContextSet>(setThread);
            GenSources = Load<Gen>("alGenSources"); GenBuffers = Load<Gen>("alGenBuffers");
            DeleteSources = Load<Delete>("alDeleteSources"); DeleteBuffers = Load<Delete>("alDeleteBuffers");
            Sourcei = Load<SourceInt>("alSourcei"); Sourcef = Load<SourceFloat>("alSourcef");
            Source3f = Load<SourceVector>("alSource3f"); GetSourcei = Load<SourceGet>("alGetSourcei");
            SourcePlay = Load<SourceAction>("alSourcePlay"); SourceStop = Load<SourceAction>("alSourceStop");
            BufferData = Load<BufferUpload>("alBufferData"); QueueBuffers = Load<BufferQueue>("alSourceQueueBuffers");
            UnqueueBuffers = Load<BufferUnqueue>("alSourceUnqueueBuffers"); GetError = Load<ErrorGet>("alGetError");
            // Keep the native library loaded for the lifetime of the game.
        }
    }
}
