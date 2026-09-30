# OpenTuner Modern UI

This branch is an incremental visual and usability redesign of OpenTuner while retaining the existing receiver, transport, media-player and hardware code.

## Current test build

- Dark modern application shell using Segoe UI typography.
- Persistent source selector, Connect, Presets and Settings controls in the header.
- Live connected/ready state in the header and selected-source status in the footer.
- Existing WinForms controls automatically receive the modern palette.
- Controls created dynamically after tuner connection are themed automatically.
- Existing MiniTiouner, Longmynd, WinterHill, BATC, MQTT, QuickTune, DATV Reporter, recording and UDP behaviour remains in the original code path.

## Build

Open `opentuner.sln` in Visual Studio on Windows, restore NuGet packages, select Release / x64 and build.

GitHub Actions on this branch also performs a Windows Release build and uploads `OpenTuner-modern-ui` as a workflow artifact when Actions is enabled for the fork.

## Hardware testing

Test source discovery/connection first, then video/audio playback, tuner controls, recording, UDP output and optional integrations. Report any layout issue with a screenshot and display scaling percentage.

Build validation is run automatically for every push to `modern-ui`.
