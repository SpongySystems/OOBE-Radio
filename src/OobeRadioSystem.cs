using OobeRadio.Audio;
using OobeRadio.Core;
using OobeRadio.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace OobeRadio;

public sealed class OobeRadioSystem : ModSystem, IMusicSettings
{
    private const string ClientFile = "ooberadio-client.json", ServerFile = "ooberadio-server.json";
    private ICoreClientAPI? api;
    private RadioPlayer? player;
    private RadioDialog? dialog;
    private MusicLease? music;
    private IClientNetworkChannel? channel;
    private ClientConfig config = new();
    private bool inWorld, policyReceived, enabled, allowFallback, disposed;
    private long tickId;
    private float policyTimer;
    private RadioState state = RadioState.Off;
    public int VolumePercent => config.VolumePercent;
    public bool IsRequested => player?.IsRequested == true;
    public bool CanPlay => inWorld && policyReceived && enabled && !IsRequested;

    public string StatusText => !policyReceived ? "Waiting for the server's radio settings…"
        : !enabled ? "Radio is disabled by this server."
        : state switch
        {
            RadioState.Connecting => "Connecting to Out of Bounds…",
            RadioState.Buffering => "Buffering live audio…",
            RadioState.Playing => "Live • Game music muted",
            RadioState.Reconnecting => "Connection lost. Reconnecting…",
            RadioState.Error => "Radio unavailable. Game music restored. Press Play to retry.",
            _ => "Radio off • Game music restored"
        };

    public override void StartServerSide(ICoreServerAPI server)
    {
        var settings = server.LoadModConfig<ServerConfig>(ServerFile) ?? new ServerConfig();
        server.StoreModConfig(settings, ServerFile);
        var policy = new StationPolicy { Enabled = settings.Enabled, AllowHttpFallback = settings.AllowHttpFallback };
        var network = server.Network.RegisterChannel("ooberadio")
            .RegisterMessageType<StationRequest>().RegisterMessageType<StationPolicy>();
        network.SetMessageHandler<StationRequest>((from, request) => network.SendPacket(policy, from));
        server.Logger.Notification("[OOBE Radio] Ready. Playback takes place on each listener's client.");
    }

    public override void StartClientSide(ICoreClientAPI client)
    {
        api = client;
        config = client.LoadModConfig<ClientConfig>(ClientFile) ?? new ClientConfig();
        config.VolumePercent = Math.Clamp(config.VolumePercent, 0, 100);
        music = new MusicLease(this);
        music.RecoverPreviousSession();
        client.Settings.Int.AddWatcher("musicLevel", OnMusicChanged);
        string directory = Path.GetDirectoryName(typeof(ICoreClientAPI).Assembly.Location)!;
        player = new RadioPlayer(directory);
        channel = client.Network.RegisterChannel("ooberadio")
            .RegisterMessageType<StationRequest>().RegisterMessageType<StationPolicy>();
        channel.SetMessageHandler<StationPolicy>(policy => client.Event.EnqueueMainThreadTask(() =>
        {
            if (disposed || !inWorld) return;
            policyReceived = true;
            enabled = policy.Enabled;
            allowFallback = policy.AllowHttpFallback;
            if (!enabled) StopPlayback();
            dialog?.Refresh();
        }, "ooberadio-policy"));

        client.Input.RegisterHotKey("ooberadio", "Out of Bounds Radio", GlKeys.R,
            HotkeyType.GUIOrOtherControls, altPressed: true, ctrlPressed: true);
        client.Input.SetHotKeyHandler("ooberadio", _ => { ToggleDialog(); return true; });
        client.ChatCommands.Create("ooberadio").WithDescription("Open the Out of Bounds radio player")
            .HandleWith(_ => { ToggleDialog(); return TextCommandResult.Success(); });
        client.Event.LevelFinalize += OnWorldReady;
        client.Event.LeaveWorld += OnLeaveWorld;
        tickId = client.Event.RegisterGameTickListener(OnTick, 50);
    }

    private void ToggleDialog()
    {
        if (!inWorld || api == null) return;
        dialog ??= new RadioDialog(api, this);
        dialog.OpenPlayer();
    }

    private void OnWorldReady()
    {
        inWorld = true;
        policyTimer = 3;
    }

    private void OnTick(float seconds)
    {
        if (player == null || api == null || disposed) return;
        if (inWorld && !policyReceived)
        {
            policyTimer += seconds;
            if (policyTimer >= 3 && channel?.Connected == true)
            {
                policyTimer = 0;
                channel.SendPacket(new StationRequest());
            }
        }
        float master = Math.Clamp(api.Settings.Int.Get("masterSoundLevel", 100), 0, 100) / 100f;
        player.SetGain(config.VolumePercent / 100f * master);

        while (player.Notices.TryDequeue(out var notice))
        {
            if (notice.Session != player.Session || !inWorld)
            {
                notice.BeforePlayback?.TrySetResult(false);
                continue;
            }
            try
            {
                if (notice.State == RadioState.Playing) music?.Acquire();
                else music?.Release();
                state = notice.State;
                if (state == RadioState.Error) player.MarkFailed();
                if (!string.IsNullOrEmpty(notice.Detail))
                    api.Logger.Warning("[OOBE Radio] {0}", notice.Detail);
                notice.BeforePlayback?.TrySetResult(true);
            }
            catch (Exception error)
            {
                notice.BeforePlayback?.TrySetResult(false);
                api.Logger.Error("[OOBE Radio] Could not update music settings: {0}", error);
                StopPlayback();
                state = RadioState.Error;
            }
            dialog?.Refresh();
        }
    }

    public bool PlayFromUi()
    {
        if (!CanPlay || player == null || api == null) return true;
        float master = Math.Clamp(api.Settings.Int.Get("masterSoundLevel", 100), 0, 100) / 100f;
        player.SetGain(config.VolumePercent / 100f * master);
        player.Start(allowFallback);
        state = RadioState.Connecting;
        dialog?.Refresh();
        return true;
    }

    public bool StopFromUi() { StopPlayback(); return true; }
    public bool SetVolume(int volume)
    {
        config.VolumePercent = Math.Clamp(volume, 0, 100);
        SaveRecovery();
        return true;
    }

    private void StopPlayback()
    {
        player?.Stop();
        try { music?.Release(); }
        catch (Exception error) { api?.Logger.Error("[OOBE Radio] Music restoration: {0}", error); }
        state = RadioState.Off;
        dialog?.Refresh();
    }

    private void OnMusicChanged(int value) => music?.OnMusicSettingChanged(value);

    private void OnLeaveWorld()
    {
        inWorld = false;
        StopPlayback();
        policyReceived = false;
        enabled = false;
        dialog?.TryClose();
        dialog?.Dispose();
        dialog = null;
    }

    public int MusicLevel { get => api!.Settings.Int.Get("musicLevel", 100); set => api!.Settings.Int["musicLevel"] = value; }
    public int? RecoveryLevel { get => config.MusicLevelBeforeRadio; set => config.MusicLevelBeforeRadio = value; }
    public void SaveRecovery() => api?.StoreModConfig(config, ClientFile);
    public void RefreshTrackVolume() => api?.CurrentMusicTrack?.UpdateVolume();

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        StopPlayback();
        if (api != null)
        {
            api.Settings.Int.RemoveWatcher("musicLevel", OnMusicChanged);
            api.Event.LevelFinalize -= OnWorldReady;
            api.Event.LeaveWorld -= OnLeaveWorld;
            if (tickId != 0) api.Event.UnregisterGameTickListener(tickId);
        }
        player?.Dispose();
        dialog?.Dispose();
        base.Dispose();
    }
}
