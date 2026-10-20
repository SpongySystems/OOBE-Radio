namespace OobeRadio.Core;

// This class is independent of the game so restoration can be tested directly.
public interface IMusicSettings
{
    int MusicLevel { get; set; }
    int? RecoveryLevel { get; set; }
    void SaveRecovery();
    void RefreshTrackVolume();
}

public sealed class MusicLease(IMusicSettings settings)
{
    public bool IsHeld { get; private set; }
    private bool writing;

    public void RecoverPreviousSession()
    {
        if (settings.RecoveryLevel is not int saved) return;
        // A nonzero value means someone already changed the setting after a crash.
        if (settings.MusicLevel == 0)
        {
            settings.MusicLevel = saved;
            settings.RefreshTrackVolume();
        }
        settings.RecoveryLevel = null;
        settings.SaveRecovery();
    }

    public void Acquire()
    {
        if (IsHeld) return;
        settings.RecoveryLevel = settings.MusicLevel;
        // Persist the restoration value BEFORE changing the game's setting.
        settings.SaveRecovery();
        IsHeld = true;
        SetLevel(0);
    }

    public void OnMusicSettingChanged(int value)
    {
        if (!IsHeld || writing || value == 0) return;
        // Respect a new volume chosen in the game's settings, when radio stops.
        settings.RecoveryLevel = value;
        settings.SaveRecovery();
        SetLevel(0);
    }

    public void Release()
    {
        if (!IsHeld) return;
        IsHeld = false;
        if (settings.RecoveryLevel is int saved) SetLevel(saved);
        settings.RecoveryLevel = null;
        settings.SaveRecovery();
    }

    private void SetLevel(int value)
    {
        writing = true;
        try
        {
            settings.MusicLevel = value;
            settings.RefreshTrackVolume();
        }
        finally { writing = false; }
    }
}
