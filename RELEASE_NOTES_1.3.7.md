# OpenTuner M0CKE v1.3.7 — WinterHill Quick Tune Fix

A service patch for v1.3.6.

## Fixed

- **QO-100 Quick Tune, first click (WinterHill / PicoTuner Ethernet).** Clicking a signal on the BATC wideband spectrum retuned the receiver, but the RX marker and the frequency display stayed on the previous signal until the signal was clicked a second time. The PicoTuner status could keep reporting the previous frequency after a retune; OpenTuner now shows the requested frequency until the hardware confirms it.

## Included from earlier releases

- v1.3.6: full-screen **←** / **→** move the video between monitors.
- v1.3.5: MiniTiouner / PicoTuner USB LO and LNB controls in the main window, full screen on any monitor, RX3/RX4 Quick Tune markers, tidier receiver panels and more.

See the [v1.3.6](RELEASE_NOTES_1.3.6.md) and [v1.3.5](RELEASE_NOTES_1.3.5.md) release notes.

## Installation

Run `OpenTuner-M0CKE-1.3.7-Setup.exe` on Windows. Existing settings are retained.

**73 de M0CKE**
