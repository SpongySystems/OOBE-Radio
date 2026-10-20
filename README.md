# Out of Bounds Radio for Vintage Story

Version 1.0.0 • Target: Vintage Story **1.22.7**, .NET 10

Start with **docs/INSTALLATION.md** or **docs/INSTALLATION.html**. The bundle
includes a compiled mod in **Releases/ooberadio_1.0.0.zip**. Copy that smaller
ZIP, unchanged, to the server's data-path Mods folder and to each player's
Mods folder. Do not install the entire source bundle as a mod.

In a loaded world, press **Ctrl+Alt+R** or enter **.ooberadio** in chat.
Press **Play** to listen; press **Stop** to restore your previous game-music
volume. Closing the window leaves the radio playing. Playback never starts
automatically when joining a world.

- Your live MP3 station, with HTTPS first and the supplied HTTP backup.
- A branded player and mod icon using your original circular PNG.
- No placed block, distance attenuation, or proximity restriction.
- An independent radio-volume slider; also follows the game's master volume.
- Original music-volume restoration on Stop, playback failure, or leaving a world.
- A persisted recovery value if the game closes unexpectedly while muted.
- Bounded memory buffers; no recording, full-track downloads, or external player.
- A server configuration enabling/disabling the feature and the HTTP backup.

This is a **universal mod**. The server supplies policy; clients fetch, decode,
and play the radio directly. A server-only installation cannot add the player UI
or the MP3 decoder to an unmodified client. The mod is required on both sides.

The included DLL is AnyCPU managed code; audio uses the game's own OpenAL Soft
library. Linux native streaming is tested with a silent output driver. Windows
and macOS clients, UI layout, and real multiplayer need the in-game checks in
the guide. Use 1.22.7 for the first trial. Older 1.21 releases use a different
.NET runtime and need a separate build.

## Build after editing

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
You need a Vintage Story 1.22.7 installation for its API assemblies. No game
binaries are redistributed in this project.

Windows PowerShell, opened in this folder:

```powershell
.\Build.ps1 -GamePath "$env:APPDATA\Vintagestory"
```

Linux/macOS:

```bash
bash Build.sh "/full/path/to/VintageStory"
```

The result is `Releases/ooberadio_1.0.0.zip`. The build downloads the small
MIT-licensed NLayer 3.0.0 MP3 decoder from NuGet. Players installing the compiled
mod do not need the SDK, NuGet, or an editor.

## Tests

```bash
dotnet run --project tests/OobeRadio.Tests.csproj
dotnet run --project tests/OobeRadio.Tests.csproj -- --audio "/path/to/VintageStory"
dotnet run --project tests/OobeRadio.Tests.csproj -- --live "/path/to/VintageStory"
```

The optional live check contacts the real HTTPS station, plays into the audio
output at zero gain, verifies several seconds of streaming, cancels the worker,
and checks music restoration through a fake settings adapter. For a headless
Linux test, set `ALSOFT_DRIVERS=null` before the command. The production mod
never sets that variable. See **docs/VALIDATION.md** for completed checks.

## Project map

| File | Purpose |
| --- | --- |
| `modinfo.json` | Game discovery, version, client/server requirements |
| `src/OobeRadioSystem.cs` | Game lifecycle, UI controls, network policy, music settings |
| `src/Configuration.cs` | Server/client settings and protobuf messages |
| `src/Gui/RadioDialog.cs` | The player and correctly scaled transparent logo |
| `src/Audio/RadioPlayer.cs` | Playback lifecycle, buffer pump, reconnects |
| `src/Audio/Mp3Producer.cs` | HTTP connection and streaming MP3 decoding |
| `src/Audio/RadioStreams.cs` | Read cancellation, timeouts, and ICY metadata removal |
| `src/Audio/OpenAlOutput.cs` | Non-spatial audio on an isolated worker context |
| `src/Core/MusicLease.cs` | Save, mute, restore, and crash recovery |
| `assets/ooberadio/textures/gui/logo.png` | Supplied 250 × 250 transparent logo |
| `modicon.png` | The same logo in the mod manager |
| `branding/oobe-logo-original.png` | Untouched 2048 × 2048 original for future artwork |

Source is MIT-licensed. NLayer's license is included. The logos and broadcast
remain the station owner's material. Vintage Story is not affiliated with this mod.
