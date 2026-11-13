# OpenTuner M0CKE v1.3.1 — Release Notes

**Dual PicoTuner Ethernet support, four-channel WinterHill reception and major user-interface improvements**

## Highlights

- **Two PicoTuner Ethernet units** in WinterHill-compatible mode, with independently configured device IPs and UDP base ports: PicoTuner 1 provides RX A/B, PicoTuner 2 provides RX C/D. Four simultaneous receiving video streams have been demonstrated.
- **Four-receiver 2×2 display** alongside single- and dual-channel views.
- **Independent per-receiver tuning** including frequency, symbol rate, LO offset and RF A/B choice.
- **Per-device LNB power controls** for two PicoTuners (OFF / 13V / 18V; physical outputs depend on the hardware). Separate power settings are saved and restored.
- **Receiver status improvements**: readable frequency, MER, decode margin, demodulator lock and service labels; Current / TS Info / Tuner Data pages and supported constellation plots.
- **BATC QO-100 wideband quick tune**, Auto Scan controls and optional 30-second rotation.
- **Recording, snapshots, fullscreen and audio controls**, presets and BATC Chat support.
- **Improved connection behaviour** and startup update checks for published GitHub releases.
- **Interface-specific Source Settings**, including one/two PicoTuner configuration, WinterHill UDP and WebSocket settings.
- **PicoTuner Broadcast Listener** now separates devices by IP address, reports their base ports, and targets remote Reset / Reboot / Bootsel / Change Base Port actions to the chosen device.
- **HELP button and offline user manual**, bundled in the Windows installer.

## Hardware and diagnostics

Supported interfaces include PicoTuner Ethernet, BATC WinterHill UDP four-channel hardware, WinterHill WebSocket, MiniTiouner USB and the other receiver sources provided by OpenTuner. Feature availability varies by source and firmware. Constellation, RF level, BER, carrier offset and LDPC metrics are only displayed when real data is provided.

## Getting started

1. Install the Windows package.
2. Open **SOURCE SETTINGS**, select the correct interface and IP/UDP configuration.
3. Connect the hardware, choose a receiver, then tune with frequency/SR/LO/RF input.
4. Click **HELP** for the full user manual. In the installed package, it opens offline.

**Dual PicoTuner example:** device 1 at `192.168.1.91` / base `9900`, device 2 at `192.168.1.152` / base `9800`. Replace these sample addresses with your own network values.

## Status

The dual-PicoTuner four-video layout has been demonstrated on hardware. This development branch remains subject to testing on other firmware versions, sound/display configurations and receiver combinations. Verify individual LNB outputs before relying on power settings.

## Downloads and support

- [GitHub releases](https://github.com/makidoja/open_tuner/releases)
- [Development build artifacts](https://github.com/makidoja/open_tuner/actions)
- [Full user manual](docs/OpenTuner-User-Manual.html)
- [Report an issue](https://github.com/makidoja/open_tuner/issues)

Credits to the original Open Tuner/ZR6TG project and BATC hardware/software community.

**73 de M0CKE**
