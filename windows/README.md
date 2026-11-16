# TOEFL Simulation for Windows

The Windows version of the TOEFL Simulation app: a WinUI 3 (Windows App SDK) desktop app with the same features, content, version number, and releases as the Mac app. The Mac app in `TOEFL Simulation/` is the main line; this folder only follows it.

## Use

1. From the [latest release](https://github.com/Gauss963/TOEFL-Simulation/releases/latest), download `TOEFL-Simulation-<version>-Windows-Setup.exe`. One installer works on every Windows 10 (19041) / Windows 11 PC; it installs the x64 or ARM64 build to match the machine.
2. Double-click it. The app isn't code-signed, so Windows may show **Windows protected your PC**: choose **More info → Run anyway**. Then click through the installer; no administrator rights are needed.
3. Open **TOEFL Simulation** from the Start menu (or the desktop shortcut). To update, run a newer Setup.exe over it. To remove it, use **Settings → Apps → Installed apps**.
4. For Speaking, allow microphone access in **Settings → Privacy & security → Microphone → Let desktop apps access your microphone**. Responses are transcribed on the PC with the bundled Whisper model; audio never leaves the device.

The app installs to `%LOCALAPPDATA%\Programs\TOEFL Simulation`. Progress, settings, and score history are saved separately in `%LOCALAPPDATA%\TOEFL Simulation` (same keys and JSON format as the Mac app), so updating or uninstalling keeps them.

## Layout

- `src/TOEFLSimulation.Core` — platform-neutral logic ported from the Mac app (question bank, adaptive forms, scoring, drills, saved progress). See its `README.md` for the API.
- `src/TOEFLSimulation.App` — the WinUI 3 app (pages, exam screens, audio playback, recording, Whisper transcription, Windows spell checking).
- `tests/TOEFLSimulation.Core.Tests` — parity tests against output recorded from the Mac app's Swift code (`Golden/`). They run on macOS too: `dotnet test --project tests/TOEFLSimulation.Core.Tests` from this folder.
- The question bank and audio are the Mac app's `TOEFL Simulation/Resources`, linked into the build, never copied. The version comes from the Xcode project (`Directory.Build.props`).

## Build

GitHub Actions (`.github/workflows/build-windows-app.yml`) builds x64 and ARM64 on every push, runs the core tests, packs both builds into one Setup.exe with Inno Setup (`installer/Setup.iss`, adding the hash-checked Whisper `base.en` model), installs it silently on a Windows runner, runs a smoke test on the installed app that opens every page and practice mode and saves screenshots, and checks that uninstalling keeps saved progress. Pushing a version tag attaches the Setup.exe to that version's release. On Windows you can also build locally with `dotnet publish src/TOEFLSimulation.App -c Release -r win-x64 -p:Platform=x64` (place `ggml-base.en-q5_1.bin` in `Models\` next to the exe for Speaking scoring).
