# Validation record

Prepared 20 October 2026. Release candidate: Out of Bounds Radio 1.0.0.

## Completed

- Compiled the full C# mod against `VintagestoryAPI.dll` and the GUI/protobuf
  dependencies from the official Vintage Story **1.22.7** Linux server archive.
  The output targets .NET 10 and uses the official .NET 10 C# compiler.
- Used NLayer **3.0.0** from NuGet. Version 2.0.1 exhibited an integer overflow
  in its unknown-length stream handling during live testing; the final package
  uses 3.0.0, whose corrected implementation passed that same live test.
- Passed **11 core checks**: exact volume restoration, preserving initially
  muted music, repeated acquire/release, a changed music preference,
  post-crash recovery, preserving a manually changed post-crash volume,
  stripping ICY metadata, rejecting truncated metadata, read timeout,
  decoder byte positions on forward-only streams, and read cancellation.
- Passed a native OpenAL integration check: a queued source advanced and
  reclaimed buffers using the game's bundled `libopenal.so.1` with the
  `ALC_EXT_thread_local_context` extension.
- Passed a live HTTPS integration check: fetched the supplied station,
  decoded its MP3 audio, streamed it through OpenAL for more than four seconds,
  then canceled the worker within the test's four-second shutdown budget.
  A test settings adapter verified that mute and restore returned to the
  exact original music level.
- The audio integration checks used OpenAL Soft's **null output driver** and
  zero source gain. They verify processing and source advancement; no actual
  speakers were available to listen to the result.
- Both supplied endpoints independently returned HTTP 200 with `audio/mpeg`
  content and audio bytes during preparation. The live decoder/output test
  specifically used the HTTPS endpoint.
- Parsed all C# source files and checked Bash build-script syntax.
- Included the user's unchanged 250 × 250 RGBA PNG as both the GUI logo and
  mod icon, and preserved the unchanged 2048 × 2048 original in the source bundle.

## Still requires a game session

The game client itself was not available for an interactive session. These
checks remain for the installation guide's first-run trial:

- Actual mod discovery and loading in the Vintage Story client/server.
- The player window, hotkey, chat command, GUI scaling, and visible branding.
- Audible sound quality, speaker/headset routing, and game-master-volume response.
- Vanilla music muting/restoration through the game's real settings adapter
  and music scheduler, rather than the test adapter.
- Real multiplayer policy delivery and independent listeners.
- Windows/macOS native-library loading and audio-device behavior.
- Longer playback, loss/recovery of the actual network, and interaction with
  other installed audio/music mods.

The included compiled mod is suitable for a first controlled test on 1.22.7.
Compilation and headless audio tests do not establish a fully in-game-tested
release or compatibility with other game versions.

## Reproduce core checks

Install .NET 10 SDK and run from the source folder:

```bash
dotnet run --project tests/OobeRadio.Tests.csproj
```

On headless Linux with a matching game installation:

```bash
ALSOFT_DRIVERS=null dotnet run --project tests/OobeRadio.Tests.csproj -- --audio "/path/to/VintageStory"
ALSOFT_DRIVERS=null dotnet run --project tests/OobeRadio.Tests.csproj -- --live "/path/to/VintageStory"
```

The live test connects to the public station. It does not save the broadcast.

## Build and dependencies

The distributed mod ZIP contains only OOBE Radio, NLayer, logo assets, metadata,
and license files. It does not contain Vintage Story DLLs or a .NET runtime.
The normal build scripts reference the user's existing game installation and
restore NLayer from NuGet. They were reviewed, but were not executed under
Windows PowerShell or a normal local desktop SDK installation in this workspace.

Workspace compilation used the same source and actual game reference assemblies
with the standalone C# compiler. Standard assembly-version unification warnings
were emitted for older `System.Runtime` references in dependency assemblies;
there were no C# compilation errors. Native streaming successfully ran on .NET 10.
