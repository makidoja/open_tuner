# OpenTuner M0CKE v1.5.0 — Signal Meters

## New

- **Signal tab** (fourth tab in each receiver panel, beside Current, TS Info and Tuner Data) for a **MiniTiouner or PicoTuner connected by USB**:
  - Four dial meters in the MiniTioune style: **Carrier Lock**, **SR Lock**, **RF Power** and **C/N MER**, each with a white peak-hold tick that holds for 2 seconds.
  - **Carrier**, **SR** and **Full** lock LEDs between the meters: green when locked, amber when getting close, red when not locked.
  - The constellation alongside.
  - Fast updates: while the Signal tab is on screen the meters refresh about every 40 ms, for peaking a dish or antenna on a weak signal. Nothing extra is read from the tuner when the tab is not shown.
  - Works on RX A with the MiniTiouner (one receiver) and RX A and RX B with the PicoTuner.

## Improved

- **Constellation:** square plot area (points are no longer stretched) and the panel's background colour on the Current tab.

## Notes

- MER is shown once the demodulator has locked.
- The Signal tab is not available with WinterHill, whose network status does not include these readings.

## Included from earlier releases

- v1.4.0: H.266 (VVC) playback via the bundled MPV player; high-DPI fix.
- v1.3.9: Quick Tune spectrum fixes (adjacent channels, wider spectrum, labels).

## Installation

Run `OpenTuner-M0CKE-1.5.0-Setup.exe` on Windows. Existing settings are retained.

**73 de M0CKE**
