# OpenTuner M0CKE v1.5.1 — Smoother Signal Meters

## Improved

- **Moving-coil meter needles.** On the **Signal** tab the Carrier Lock, SR Lock, RF Power and C/N MER needles now sweep smoothly like a moving-coil meter, settling in about 0.3 seconds with a slight overshoot, instead of jumping between readings. The dials redraw at about 60 frames per second while a needle is moving.
  - Readings still arrive every ~40 ms, so the meters stay responsive.
  - The peak-hold ticks and lock LEDs still follow the actual readings, so brief peaks are not lost.

## Included from earlier releases

- v1.5.0: Signal tab for USB MiniTiouner / PicoTuner (dial meters, lock LEDs, constellation); square constellation plot.
- v1.4.0: H.266 (VVC) playback via the bundled MPV player; high-DPI fix.

## Installation

Run `OpenTuner-M0CKE-1.5.1-Setup.exe` on Windows. Existing settings are retained.

**73 de M0CKE**
