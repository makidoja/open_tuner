# OpenTuner M0CKE v1.3.6 — Full-Screen Screen-Move Fix

A service patch for v1.3.5.

## Fixed

- **Moving full-screen video between monitors.** In full screen, **←** / **→** now move the video to the previous / next screen as intended. In v1.3.5 the arrow keys were taken by Windows control navigation and did nothing. **ESC** still exits full screen.

## Included from v1.3.5

- MiniTiouner / PicoTuner USB **LO box, L tick and LNB A/B selectors** working from the main window, with start-up defaults in **Source Settings → Default Tuner Properties** (LO in MHz).
- Full screen opens on the screen OpenTuner is on and remembers the last screen used.
- QO-100 Quick Tune RX3/RX4 markers follow receiver lock.
- Tidier receiver header and entry fields; CONNECT, LOCKED and resize-leak fixes.

See [v1.3.5 release notes](RELEASE_NOTES_1.3.5.md) for full details.

## Installation

Run `OpenTuner-M0CKE-1.3.6-Setup.exe` on Windows. Existing settings are retained.

**73 de M0CKE**
