# OpenTuner M0CKE v1.3.4 — Four-Receiver QO-100 Quick Tune

A new OpenTuner release extending BATC wideband Quick Tune to **all four supported receivers**.

## What's new

- **RX1–RX4 Quick Tune lanes** in the QO-100 wideband spectrum when a four-channel receiver configuration is active and the layout is set to **4 RX**.
- **Click-to-tune RX3 and RX4** directly from their corresponding spectrum lanes; commands go to the matching backend receiver.
- **Independent tuned markers and colours** for each of the four receivers.
- **Individual Auto Scan and optional Rotate 30s** controls for RX3 and RX4 as well as RX1/RX2.
- **Four-way signal reservation:** automatic scanning avoids the beacon and signals already assigned to other active receivers.
- **Adaptive display:** one- and two-receiver layouts continue to show only their active receiver controls; the wider four-receiver strip is presented only when four channels are available.

## Included fixes and existing features

- v1.3.2 VLC transport-stream reader fix for freezes caused by an empty-queue busy loop.
- Two independent PicoTuner Ethernet units operating as four receiver channels in WinterHill/PicoTuner mode.
- Receiver-specific source configuration, LNB controls and PicoTuner Broadcast Listener.
- BATC Chat, snapshot and recording controls, presets, HELP and offline HTML user manual.

## Installation

Run `OpenTuner-M0CKE-1.3.4-Setup.exe` on Windows. Existing settings should be retained. The HTML user manual and an installer SHA256 checksum are included as release attachments.

Thank you to everyone testing and reporting issues.

**73 de M0CKE**
