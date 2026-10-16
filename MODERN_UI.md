# OpenTuner Modern UI (M0CKE)

This branch modernises the Windows interface while retaining the existing receiver, transport, media-player and hardware code paths. The last user-tested build had working video and audio; changes affecting playback must be separately verified on Windows.

## Build and installer

- Open `opentuner.sln` in Visual Studio on Windows or use the GitHub Actions workflow `.github/workflows/modern-ui-build.yml`.
- The project targets **.NET Framework 4.7.2**, Release / x64.
- The workflow restores NuGet packages, generates `OpenTunerModern.ico`, compiles the application, packages the official OpenTuner FFmpeg runtime and builds the Inno Setup installer.
- Installer configuration: `installer/OpenTuner.iss`.
- Installer branding: **Open Tuner (ZR6TG) Version M0CKE 0.C.v1.0.3**.
- The generated icon is embedded in the EXE and installer and copied for the Start Menu and optional desktop shortcut.
- Successful workflow artifacts include `OpenTuner-modern-ui` (portable build) and `OpenTuner-Windows-Installer-v1.0.3`.

## Functional regression checks

After changes to dependencies, packaging or source files, test hardware detection, source switching, tuner connection, live video and audio, recording, UDP output, and optional BATC, MQTT and DATV Reporter functions. Test the installed build as well as the portable build.

## Repository cleanup

See `CODEBASE_AUDIT.md` for the cleanup inventory, risk classifications and deferred items. Do not remove codecs, hardware interfaces, firmware or NuGet packages solely because a search suggests they are unused. Confirm the dependency graph and run Windows/hardware regression tests first.

## USB MiniTiouner constellation validation

- The optional constellation is now in the receiver-status panel, not over video. MER and margin are stacked to free plot space.
- The MiniTiouner status event passes sampled STV0910 I/Q symbols to the display when locked. Failed USB register reads invalidate a sample rather than displaying stale coordinates.
- **Hardware verification outstanding:** connect a known DVB-S QPSK and DVB-S2 QPSK signal on USB MiniTiouner, inspect clustering and tuner assignment, and compare with a known-good analyser. Raw register coordinates are diagnostic and may require centring/scaling adjustment.
- Verify layout at typical window widths and Windows scaling, and ensure video and audio remain unaffected.
