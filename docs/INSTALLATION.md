# Out of Bounds Radio: installation and modding guide

**Version 1.0.0 · Vintage Story 1.22.7 · Prepared 20 October 2026**

This project turns your Out of Bounds station into an optional, personal radio
player inside Vintage Story. Your circular logo appears in the player and the
mod manager. A compiled ZIP is included, so you can try it without learning C#
or installing development tools first.

## 1. How this works on a server

This is a **universal code mod**: install the same mod ZIP on the server and on
the computers that join it. The server makes the feature available and sends
its enable/disable setting. Each listener's game connects directly to your
station, decodes MP3 audio, and plays it locally.

The server cannot provide a custom window or a new streaming decoder to an
otherwise unmodified client. Marking a mod as server-only would not satisfy
those requirements. The package therefore declares both sides as required.

Every player controls their own Play, Stop, and volume settings. Turning on your
radio does not turn it on for everybody else. There is no placed radio block,
world coordinate, range check, or proximity attenuation. You can walk, travel,
teleport, and change dimension without moving out of range.

Clients connect separately, so they hear the live broadcast with their own
network/buffer delay. This is not sample-synchronized group playback. Each
listener also counts as a listener on your station and consumes their own
internet bandwidth. The game server does not relay or record the audio.

## 2. What to download and install

The project bundle contains these important parts:

| Item | What to do with it |
| --- | --- |
| `Releases/ooberadio_1.0.0.zip` | The actual compiled mod; install this smaller ZIP |
| `docs/INSTALLATION.html` | Open in any browser for this guide |
| `README.md` | Project summary and code map |
| `src/`, `OobeRadio.csproj`, `Build.ps1`, `Build.sh` | Keep if you want to modify or rebuild it |
| `tests/`, `docs/VALIDATION.md` | Test code, verification details, and remaining checks |

The separate `ooberadio_1.0.0.zip` download is identical to the one in the bundle.
**Do not put the full source bundle into the game's Mods folder.**

Use **Vintage Story 1.22.7** for your first test. The DLL targets .NET 10, matching
the current 1.22 mod template. It is not a build for 1.21 or an assurance of
compatibility with a future major game version. Check the version in the game's
main menu and in your server control panel before installation.

## 3. Install on your own computer

1. Close Vintage Story completely.
2. Find your personal Vintage Story data folder. The most reliable method is to
   open the game's Mod Manager and use its control for opening the Mods folder,
   then close the game before copying the file.
3. On a standard Windows install, you can also press **Win+R**, enter
   `%APPDATA%\VintagestoryData\Mods`, and press Enter. A custom `--dataPath`
   installation uses its own data folder instead.
4. Copy **ooberadio_1.0.0.zip** into that Mods folder. Leave the ZIP intact.
5. Remove any older OOBE Radio version from that same folder so only one is
   enabled. Keep a backup outside the Mods folder if needed.
6. Start the game. In Mod Manager, confirm that **Out of Bounds Radio** appears
   and is enabled. Its icon should be your circular station logo.
7. For an initial test, load a local single-player test world. Single-player
   also runs a local server component, so the universal mod can work there.

The standard data folders are commonly `%APPDATA%\VintagestoryData` on Windows,
`~/.config/VintagestoryData` on Linux, and
`~/Library/Application Support/VintagestoryData` on macOS. A launcher, sandboxed
package, or custom data path can change these; the game's own Mods-folder
control avoids guessing.

## 4. Install on your multiplayer server

1. Stop the server from its console or hosting panel.
2. Locate the **server's data-path Mods folder**. Ask your host where it stores
   Vintage Story data if this is not visible in the panel.
3. Upload **ooberadio_1.0.0.zip**, unchanged, into that folder.
4. Ensure that all participating players have the same mod version in their
   own game. Manual installation is sufficient even if your server's join flow
   offers mod downloads.
5. Start the server. Its log should include:
   `[OOBE Radio] Ready. Playback takes place on each listener's client.`
6. Join it with your mod-enabled client.

The server data folder is separate from the game installation directory. Do
not overwrite the built-in `VSEssentials`, `VSSurvivalMod`, or other game DLLs.
No station port needs to be opened on the game server: the audio connection
goes from each player's computer to your existing radio host.

The default universal-mod requirements mean a joining player must have this
mod. Playback still remains off until that player presses Play. If you later
want installation to be optional for joining players, `requiredOnClient` can
be changed to `false` in `modinfo.json`, then the package must be redistributed.
Players without the mod will have no radio UI or audio; they cannot receive
those features from the server alone.

## 5. Open and use the player

1. Wait until your character has entered the world.
2. Press **Ctrl+Alt+R**, or open chat and enter **.ooberadio**. Include the leading
   dot: this is a client command, not a server slash command.
3. Your logo, a connection/status line, Play and Stop buttons, and a radio-volume
   slider appear.
4. Press **Play**. The status changes through Connecting and Buffering to
   **Live · Game music muted**.
5. Set the **Radio volume** slider to your preference. It is separate from the
   music slider in the game's audio settings and starts at 50%.
6. Close the player window if you want. The station keeps playing.
7. Open it again and press **Stop** to stop the connection and restore game music.

The hotkey can be changed in the game's controls under **Out of Bounds Radio**.
The chat command remains available if that key combination conflicts with
another mod or your operating system.

There is no autoplay on joining a server or starting a world. Radio stops when
you leave the world. Returning to the world requires pressing Play again.
The radio uses the operating system's default audio output, which can differ
from a device you selected specifically for Vintage Story. The radio also
follows the game's master volume. Ambient sound, creatures, footsteps, and
other sound effects retain their normal settings.

## 6. Exactly what happens to game music

When enough radio audio has buffered, the mod first remembers your current
game-music volume, saves a small recovery value, and sets `musicLevel` to zero.
Only then does it allow the audio worker to start the radio source.

If your music was at 37%, stopping the radio restores 37%. If you had already
muted game music before starting the radio, it stays muted afterwards. The mod
does not force it to 100%.

The original level is restored when you press Stop, leave the world, or the
stream runs out of buffered audio while reconnecting. A connection failure
before playback starts leaves the game's music setting alone. Reconnection
mutes the game music again just before radio playback resumes.

Restoring the volume enables normal game music behavior. Vintage Story may
wait until its music scheduler chooses another track, so immediate silence
after Stop does not by itself mean that music is still disabled.

If you select a new nonzero music volume in the game's audio settings while
the radio is playing, the mod remembers that new preference, keeps game music
muted during the radio, and restores that new preference when you stop. Use
**Stop** when you want game music back; setting only the radio slider to 0%
keeps the radio session active.

If the game crashes, its stored settings could still contain zero. On the next
load of this mod, the recovery value restores your volume if it is still zero.
If you already changed the volume to another nonzero value after the crash,
the mod preserves your new setting. If you remove the mod before it can
recover, you can manually restore the Music slider in the game's audio options.

## 7. Configure the server

On first startup the server creates:

```text
<server data path>/ModConfig/ooberadio-server.json
```

Its default contents are:

```json
{
  "Enabled": true,
  "AllowHttpFallback": true
}
```

| Setting | Meaning |
| --- | --- |
| `Enabled` | Makes the Play button available to this server's players |
| `AllowHttpFallback` | Tries your supplied HTTP endpoint on alternate failed connection attempts |

To change these, stop the server, edit the JSON with a text editor, save it,
and start the server again. Use lowercase `true`/`false`, keep double quotes,
and do not add a trailing comma. Clients receive the new policy when joining.

The default primary stream is:

```text
https://radio.outofbounds.live/listen/out_of_bounds/radio.mp3
```

Your supplied backup is:

```text
http://radio.outofbounds.live:8000/radio.mp3
```

HTTPS is always attempted first. The HTTP backup is unencrypted. Set
`AllowHttpFallback` to `false` if you want the mod to use HTTPS only. The URLs
are fixed in the source rather than supplied arbitrarily by a server. To
change station endpoints, edit the two constants near the top of
`src/Audio/RadioPlayer.cs`, rebuild, and distribute the resulting mod ZIP.

The mod makes up to six connection attempts with increasing retry delays.
After the final failure, the window shows that radio is unavailable and the
Play button lets the listener retry. Game music is restored during an outage.

## 8. Configure your own listener settings

The client creates:

```text
<your game data path>/ModConfig/ooberadio-client.json
```

```json
{
  "VolumePercent": 50,
  "MusicLevelBeforeRadio": null
}
```

Use the in-game slider for normal volume changes. `MusicLevelBeforeRadio` is
the automatic recovery marker; do not clear it while the radio is playing.
With the game closed, you can change `VolumePercent` to a value from 0 to 100.

## 9. A first-run test before inviting everyone

Use a test world or a small private session first. Complete these checks:

1. Set game music to a recognizable value such as 40%.
2. Open the player. Confirm that your logo is round and transparent around
   the outside, and that the buttons and slider are visible.
3. Press Play. Check that the station starts and normal music becomes muted.
4. Walk well away from the starting location and, if convenient, teleport.
   The station should remain at the same volume.
5. Close the window. Confirm that the station keeps playing.
6. Reopen it and press Stop. Check that the game Music slider returns to 40%.
7. Start the radio again, then leave the world. Verify that playback stops.
8. Join with a second mod-enabled computer. Verify that its Play/Stop choices
   do not change the first player's choices.
9. With the radio playing, briefly disconnect the test computer from the
   internet. After buffered audio drains, confirm that music is restored and
   the status indicates reconnection. Reconnect and check recovery.
10. Check the window once at your preferred GUI scale, and verify that the
    radio uses the expected speakers or headset.

Compilation, core recovery tests, and native streaming checks are documented
in `VALIDATION.md`. These do not replace checking GUI behavior and actual
multiplayer in your game installation.

## 10. Rebuild or customize the mod

You only need this section if you change the code or artwork. The provided
compiled ZIP can be installed directly.

### Windows

1. Install the **.NET 10 SDK** from
   <https://dotnet.microsoft.com/download/dotnet/10.0>. Choose the SDK, not just
   the runtime. A supported copy of VS Code, Visual Studio, or Rider is useful
   for editing, but the build script does not require an IDE.
2. Extract the source bundle to a normal folder, such as `C:\Mods\OOBE-Radio`.
3. Find your Vintage Story 1.22.7 **installation** folder. It contains
   `VintagestoryAPI.dll` and `Lib`. The common Windows path is
   `%APPDATA%\Vintagestory`. This is different from `VintagestoryData`.
4. Open PowerShell in the extracted project folder.
5. Check the SDK with `dotnet --list-sdks`; a `10.0.xxx` SDK should appear.
6. Run:

```powershell
.\Build.ps1 -GamePath "$env:APPDATA\Vintagestory"
```

For a custom install location:

```powershell
.\Build.ps1 -GamePath "D:\Games\Vintagestory"
```

If PowerShell blocks the downloaded script under your execution policy, run
it in a new process with a policy applying only to that process:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -GamePath "D:\Games\Vintagestory"
```

The build fetches NLayer from NuGet, compiles the mod against your game API, and
creates **Releases/ooberadio_1.0.0.zip**. Stop the game and server before
replacing their old mod ZIPs. Keep only one OOBE Radio version installed.

### Linux and macOS

Install the .NET 10 SDK and the `zip` command. In a terminal in the source folder:

```bash
bash Build.sh "/full/path/to/VintageStory"
```

Use the directory containing `VintagestoryAPI.dll`, including for a macOS app
bundle if your install stores the managed game files inside it. The resulting
managed mod ZIP is the same type of package used on Windows. Native audio
availability still needs testing on the destination platform.

### Replacing the logo

Your supplied 250 × 250 PNG is already copied to both:

```text
modicon.png
assets/ooberadio/textures/gui/logo.png
```

The original 2048 × 2048 PNG is preserved in `branding/oobe-logo-original.png`.
To update the artwork, replace the two small PNGs with a transparent PNG and
rebuild. Keeping a square image with transparency around the circle preserves
the current appearance. The player scales its logo to 250 logical GUI pixels.
The large original is kept in the source bundle and is not needed in the
installed mod ZIP.

### Understanding the code

`OobeRadioSystem` is the game's entry point. `StartServerSide` loads the server
settings and answers small network-policy requests. `StartClientSide` registers
the window hotkey, chat command, settings watcher, and world lifecycle hooks.

`RadioDialog` builds the game UI with the official GUI composer. `RadioPlayer`
owns the background session, reconnect logic, and a bounded queue of PCM chunks.
`Mp3Producer` opens the live HTTP response and incrementally decodes it with
NLayer. `RadioStreams` supports cancellation and strips ICY metadata if needed.

`OpenAlOutput` uses the OpenAL Soft library bundled with the game. A source
relative to its listener and with no distance attenuation provides non-spatial
audio. Its context belongs to a worker thread; it does not replace the game's
global audio context. `MusicLease` handles volume preservation and recovery.

Network reads and decoding stay off the game UI thread. A short handshake
allows the main game thread to mute its music before radio playback starts.
Buffers are bounded and old audio is discarded naturally as it plays.

## 11. Troubleshooting

| Symptom | What to check |
| --- | --- |
| Mod does not appear | Install the smaller compiled ZIP; check that it contains `modinfo.json` at its root |
| Missing mod or network-version error on join | Client and server need the same `ooberadio_1.0.0.zip`; remove older duplicates |
| Waiting for server settings | Confirm that the server also loaded the mod, then reconnect |
| Hotkey does nothing | Enter `.ooberadio`; check controls for a hotkey conflict; wait until the world finishes loading |
| Radio unavailable | Open the HTTPS station link in a browser; check the station and the client's internet connection |
| Live status but no sound | Check radio volume, game master volume, OS volume, and the OS default audio device |
| Game music remains silent after Stop | Check its restored Music slider; its normal scheduler can include silent intervals |
| Audio library error | Verify the game installation and its OpenAL Soft library; do not overwrite game libraries with random DLLs |
| Build cannot find the API | Pass the installation folder containing `VintagestoryAPI.dll`, not the data folder |
| Build reports an unsupported target framework | Install the .NET 10 SDK; confirm a 10.x SDK appears in `dotnet --list-sdks` |
| Missing `NLayer` when launching | Use the complete packaged ZIP; it must contain both `OobeRadio.dll` and `NLayer.dll` |

Details are written to the normal client log, usually
`<your data path>/Logs/client-main.log`, with the prefix **[OOBE Radio]**.
Server initialization appears in the server's normal logs. A useful bug report
includes the game version, operating system, the status shown in the player,
and the relevant OOBE Radio log entries.

To uninstall, first press Stop and leave the world. Close the client, stop the
server, remove the mod ZIP from each Mods folder, and restart. Its small config
files can be deleted afterwards if you no longer want the saved preferences.

## 12. Official references

- [Vintage Story API](https://apidocs.vintagestory.at/)
- [Official mod template and its .NET target](https://github.com/anegostudios/vsmodtemplate/blob/master/ModTemplate/ModTemplate.csproj)
- [Preparing for code mods](https://wiki.vintagestory.at/Modding:Preparing_For_Code_Mods)
- [Mod metadata and client/server requirements](https://wiki.vintagestory.at/Modding:Modinfo)
- [Network API guide](https://wiki.vintagestory.at/Modding:Network_API)
- [NLayer MP3 decoder](https://github.com/naudio/NLayer)
- [OpenAL Soft](https://github.com/kcat/openal-soft)

This implementation uses the documented mod/UI/settings interfaces and the
game's native audio library. It does not replace your game executable or add
music files to the world save.
