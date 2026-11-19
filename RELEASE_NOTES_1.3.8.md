# OpenTuner M0CKE v1.3.8 — VLC Freeze Fix and Video Mouse Controls

A service patch for v1.3.7.

## Fixed

- **"Not Responding" freeze with the VLC player.** When two receivers retuned at the same moment — for example Auto Scan with Rotate on RX1 and RX2 — OpenTuner could hang. The old VLC player was being released on the UI thread while VLC's video thread needed that same thread. Players are now detached on the UI thread and released in the background.
- **BATC chat:** the message entry box no longer covers the last line of the chat.

## Changed

- **Video mouse controls**
  - **Left-click** on a receiver's video shows or hides the information bar (main window and full screen).
  - **Right-click** on a receiver's video opens it in **full screen**; in full screen, **right-click** (or **ESC**) closes it.
  - **←** / **→** still move full screen to another monitor.

## Included from earlier releases

- v1.3.7: WinterHill / PicoTuner Ethernet Quick Tune first-click fix.
- v1.3.6: full-screen monitor moves with the arrow keys.
- v1.3.5: MiniTiouner / PicoTuner USB LO and LNB controls, full screen on any monitor, RX3/RX4 Quick Tune markers and more.

## Installation

Run `OpenTuner-M0CKE-1.3.8-Setup.exe` on Windows. Existing settings are retained.

**73 de M0CKE**
