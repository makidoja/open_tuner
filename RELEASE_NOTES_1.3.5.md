# OpenTuner M0CKE v1.3.5 — Receiver UI, MiniTiouner LO/LNB and Full-Screen Update

A maintenance and usability release building on v1.3.4.

## What's new

- **MiniTiouner / PicoTuner USB LO and LNB controls work from the main window.** The LO box and **L** tick now set the LO offset live, and the **LNB A / LNB B** selectors switch the LNB supply (Off, 13 V, 18 V) on both the FTDI module and PicoTuner USB. Previously these did nothing on the MiniTiouner source.
- **MiniTiouner Settings — Default Tuner Properties.** The LO offsets and LNB supply in the settings dialog are now the start-up defaults, and the LO is entered in **MHz** (for example `9750 MHz`) rather than kHz.
- **Full screen on any monitor.** Full-screen video opens on the screen OpenTuner is on; press **←** / **→** to move it to another screen. The last screen used is remembered for the session. **ESC** exits full screen; double-click is left to the player's info bar.
- **QO-100 Quick Tune RX3/RX4 markers** now follow receiver lock automatically, as RX1/RX2 do, rather than appearing only after a click.
- **Tidier receiver header row:** larger **● RX A–D** titles, a SNAPSHOT label, a clear full-screen icon, everything aligned on one line, and much more room for the callsign.
- **Receiver entry fields:** FREQ and LO are left-aligned with larger text; fields are more compact in the four-receiver layout.

## Fixes

- **CONNECT button state:** a second timer no longer overrides the button, and the connection state keeps refreshing for every source type (including USB).
- **LOCKED indicator** text no longer wraps to a lone dot in the four-receiver layout.
- **Memory/handle leak** when resizing the window (fonts were recreated on every resize) fixed.

## Under the hood

- About 1,700 lines of unused legacy UI code removed.
- Internal UI components use typed access instead of reflection, so future changes are checked at build time.

## Installation

Run `OpenTuner-M0CKE-1.3.5-Setup.exe` on Windows. Existing settings should be retained. MiniTiouner users: check **Source Settings → Default Tuner Properties** and set your LO (e.g. 9750 MHz) and LNB supply defaults.

Thank you to everyone testing and reporting issues.

**73 de M0CKE**
