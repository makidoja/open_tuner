# OpenTuner M0CKE v1.3.9 — Quick Tune Spectrum Fixes

A service patch for v1.3.8.

## Fixed

- **Adjacent channels detected as one signal.** Two close channels (for example 10498.75 and 10499.25 MHz at 333 kS) could be detected as a single wide signal centred between them. Clicking it tuned the receiver into the gap (around 10499.0), so the RX marker and frequency sat between the two stations. Each detected block is now split at the dip between channels, so every station is detected and tuned on its own frequency.
- **Missing labels on close channels.** Callsign, dBb, frequency and symbol-rate labels that would overlap a neighbour — such as the last channel next to the band edge — now drop to a second row instead of disappearing.

## Changed

- **Wider QO-100 Quick Tune spectrum.** The spectrum now uses the available window width (up to 1600 px instead of 1140 px), giving more room per channel.
- **RX marker tags** are drawn just under each marker's arrow head (RX1 one line lower, clear of the callsign labels), so they no longer sit on top of the frequency labels.

## Included from earlier releases

- v1.3.8: VLC freeze fix, right-click full screen / left-click information bar, BATC chat fix.
- v1.3.7: WinterHill / PicoTuner Ethernet Quick Tune first-click fix.

## Installation

Run `OpenTuner-M0CKE-1.3.9-Setup.exe` on Windows. Existing settings are retained.

**73 de M0CKE**
