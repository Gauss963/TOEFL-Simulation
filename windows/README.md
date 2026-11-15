# TOEFL Simulation for Windows

The Windows version of the TOEFL Simulation app: a WinUI 3 (Windows App SDK) desktop app with the same features, content, version number, and releases as the Mac app. The Mac app in `TOEFL Simulation/` is the main line; this folder only follows it.

## Use

1. From the [latest release](https://github.com/Gauss963/TOEFL-Simulation/releases/latest), download `TOEFL-Simulation-<version>-Windows-x64.zip` (most PCs) or `-Windows-arm64.zip` (Snapdragon / ARM PCs).
2. Unzip it anywhere and run `TOEFL Simulation\TOEFLSimulation.exe`. Nothing is installed; Windows 10 (19041) or later, Windows 11 recommended.
3. The app is not code-signed, so the first launch may show **Windows protected your PC**. Choose **More info → Run anyway**.
4. For Speaking, allow microphone access in **Settings → Privacy & security → Microphone → Let desktop apps access your microphone**. Responses are transcribed on the PC with the bundled Whisper model; audio never leaves the device.

Progress, settings, and score history are saved in `%LOCALAPPDATA%\TOEFL Simulation` (same keys and JSON format as the Mac app), so replacing the app with a newer zip keeps them.

## Layout

- `src/TOEFLSimulation.Core` — platform-neutral logic ported from the Mac app (question bank, adaptive forms, scoring, drills, saved progress). See its `README.md` for the API.
- `src/TOEFLSimulation.App` — the WinUI 3 app (pages, exam screens, audio playback, recording, Whisper transcription, Windows spell checking).
- `tests/TOEFLSimulation.Core.Tests` — parity tests against output recorded from the Mac app's Swift code (`Golden/`). They run on macOS too: `dotnet test --project tests/TOEFLSimulation.Core.Tests` from this folder.
- The question bank and audio are the Mac app's `TOEFL Simulation/Resources`, linked into the build, never copied. The version comes from the Xcode project (`Directory.Build.props`).

## Build

GitHub Actions (`.github/workflows/build-windows-app.yml`) builds x64 and ARM64 on every push, runs the core tests, downloads the Whisper `base.en` model (hash-checked), and runs a smoke test that opens every page and practice mode and saves screenshots. Pushing a version tag attaches the zips to that version's release. On Windows you can also build locally with `dotnet publish src/TOEFLSimulation.App -c Release -r win-x64 -p:Platform=x64` (place `ggml-base.en-q5_1.bin` in `Models\` next to the exe for Speaking scoring).
