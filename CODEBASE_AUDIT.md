# OpenTuner modern-ui codebase audit

Audit date: 2026-10-13. Scope: tracked repository files and selected build configurations; **not** a full static dependency graph or a Windows runtime test.

## Completed, low-risk

- Removed duplicate `ModernDashboard.cs` and `ModernTunerLayout.cs` Compile declarations from `Directory.Build.targets`; they remain in `Directory.Build.props`.
- Updated `MODERN_UI.md` to describe the current installer and regression checks.

## Candidates requiring validation

| Item | Finding | Before removal |
|---|---|---|
| `tools/GenerateModernIcon.ps1` | A second generator alongside actively used `build/GenerateBrandIcon.ps1` | Check references in workflows and developer instructions; compare icon output |
| `ModernDashboardStartup.cs` | Empty partial `MainForm` placeholder included from `Directory.Build.targets` | Verify no external/build dependency, remove file and Compile item together |
| `Directory.Build.targets` Release OutputType | May be redundant with `Directory.Build.props` | Verify both Debug and Release effective MSBuild properties |
| `README.md` | Minimal upstream introduction | Expand project fork/build instructions without losing upstream attribution |
| `packages.config` | Many direct and transitive .NET Framework packages; usage uncertain | Establish NuGet dependency graph and compare actual project references and Windows build output |
| `FTD2XX_NET.XML` | API documentation shipped in source tree | Check whether useful to developers; installer should not include source-only docs |
| `tested_picotuner_firmware/` | Firmware and update instructions, not normal app runtime | Retain in repository or move to documented firmware subfolder; verify not included in installer |
| `tv.ico` | Legacy icon remains in repository | Check usages in forms/resources and project before removal |
| `Modern*.cs` incremental UI passes | Many passes and bridges are intentionally layered | Map call sites and ordering; avoid mechanical consolidation |

## High-risk: preserve until verified

- FFmpeg runtime, Flyleaf, LibVLC, audio libraries, native media DLLs.
- Minitiouner/FTDI, PicoTuner, WinterHill and Longmynd source and device interactions.
- BATC, MQTT, WebSocket, UDP and related integrations.
- Embedded resources, language translations, WinForms designer files and .resx assets.

## Packaging checks

- Confirm installer copies exactly the required runtime, native codecs, resources and configuration.
- Confirm application icon, installer icon, Start Menu icon and desktop icon on a clean Windows machine.
- Confirm install, launch, upgrade, uninstall and user settings persistence.
- Compare portable vs installed live video/audio and tuner operation.

**Status:** The cleanup is conservative and requires successful CI build plus a real Windows playback/hardware regression pass before further deletion.
