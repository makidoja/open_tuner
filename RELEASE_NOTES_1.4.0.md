# OpenTuner M0CKE v1.4.0 — H.266 (VVC) Playback

## New

- **H.266 / VVC decoding.** OpenTuner now ships with the **MPV** player and a current libmpv whose FFmpeg includes the H.266 (VVC) decoder. To use it, open **Media Settings** and set a receiver's **Media Player** to **MPV (H.264/H.265/H.266)**, then Save. MPV also plays H.264 and H.265 as before.
  - Decoding is done in software on the CPU (hardware VVC decode only exists on very recent GPUs), which is fine at typical DATV resolutions and bit rates.
  - VLC and FFmpeg remain available and are unchanged; they do not decode H.266.
  - The bundled libmpv build is recorded in `libmpv-version.txt` in the install folder.

## Fixed

- **High display scaling:** at Windows display scaling above 100% the receiver title (“● RX A”) and the **Current / TS Info / Tuner Data** tabs could be clipped. They are now sized from the rendered text. (Thanks Mike for the report.)

## Notes

- The installer is larger because it now includes libmpv.

## Included from earlier releases

- v1.3.9: Quick Tune spectrum fixes (adjacent channels, wider spectrum, labels).
- v1.3.8: VLC freeze fix, right-click full screen, BATC chat fix.

## Installation

Run `OpenTuner-M0CKE-1.4.0-Setup.exe` on Windows. Existing settings are retained.

**73 de M0CKE**
