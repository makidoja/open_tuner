# OpenTuner M0CKE

**A modern Windows DATV receiver interface for DVB-S and DVB-S2 amateur television.**

OpenTuner M0CKE is an actively developed interface based on the original **[Open Tuner](https://www.zr6tg.co.za/open-tuner/)** project by ZR6TG. It brings DATV reception, receiver controls, live transport-stream information, and QO-100 tools together in a dark Windows desktop UI.

> **Development status — v1.3.2:** The features described here are being developed and hardware-tested on the [`connect-state-test` branch](https://github.com/makidoja/open_tuner/tree/connect-state-test). Builds are experimental. The existing `modern-ui` branch is being kept separate during this work.

## Highlights

- **One, two, or four receiver views**, including a working 2×2 four-receiver display.
- **Native BATC Winter Hill UDP mode** for the original Raspberry Pi-based four-channel Winter Hill receiver.
- **PicoTuner Ethernet** (two receivers per device, up to four with two devices), **PicoTuner USB**, **MiniTiouner USB**, and other supported OpenTuner source options.
- Independent tuning and live receiver controls, including RF input, symbol rate and optional LO offset.
- Live **frequency, MER, decode margin, DVB mode, MODCOD, service and provider** information when exposed by the receiver.
- **Current**, **TS Info**, and **Tuner Data** pages for each receiver.
- Actual received transport-stream bitrate history; null-packet percentage and media information where provided.
- Video/audio codec details and video resolution when the selected media player reports them.
- Constellation displays **where real hardware constellation data is available**; missing data is not fabricated.
- QO-100 BATC wideband spectrum integration, quick tuning, RX markers, automatic scanning and optional 30-second receiver rotation.
- Recording, snapshots, fullscreen playback, volume and mute controls.

## Receiver hardware and interfaces

| Source | Receiver channels | Notes |
| --- | ---: | --- |
| BATC Winter Hill UDP | 4 | Original Raspberry Pi-based Winter Hill running BATC UDP software, such as `winterhill-3v20`. Experimental native four-channel support. |
| PicoTuner Ethernet / Winter Hill firmware | 2 or 4 | One dual-channel Ethernet PicoTuner or two independent devices for four receivers (RX A/B and RX C/D), each with separate IP/UDP base-port settings. |
| PicoTuner USB | 2 | Dual receiver support, subject to connected hardware and drivers. |
| MiniTiouner USB | Up to 2 | Based on the installed hardware configuration. |
| Winter Hill WebSocket | 4 | Separate legacy/ZR6TG-compatible interface; requires an appropriate WebSocket service. |
| Other original OpenTuner sources | Varies | Availability and capabilities depend on the source implementation. |

**Two independent Ethernet PicoTuners can now be combined into one four-channel source.** This is separate from the native mode for a single four-receiver BATC WinterHill unit. Four live streams from two PicoTuners have been demonstrated in v1.3.1.

### Native BATC Winter Hill UDP setup

Example configuration for a Winter Hill Raspberry Pi:

| Setting | Example |
| --- | --- |
| Media source | WinterHill Variant |
| Default interface | **Winter Hill UDP (BATC 4 RX)** |
| Winter Hill/PicoTuner UDP IP address | `192.168.1.172` (replace with your Pi's address) |
| UDP base port | `9900` |

The default four receiver command ports are **9921–9924** for a base port of `9900`. The Windows PC must be on a network that can reach the Pi and must allow the required UDP traffic.

This native mode does **not** require a WebSocket server on TCP port 8080. That setting applies to the separate WebSocket interface.

For a **single PicoTuner Ethernet** device, choose the PicoTuner Ethernet option instead of the four-channel BATC Winter Hill UDP option.

## Modern interface

The interface provides independent receiver cards and three information views:

| Page | Displays |
| --- | --- |
| **Current** | Live frequency, MER, decode margin, optional constellation. |
| **TS Info** | Actual received TS bitrate graph, null packets, stream status, recording status and media information where available. |
| **Tuner Data** | Demodulation state, DVB system, MODCOD, RF input, frequency, NIM frequency, LO configuration, symbol rate, service/provider and other supported readings. |

The **4 RX** view uses a 2×2 video layout with compact left-side receiver information and tuning controls. The 1–2 RX modes retain the larger information layout.

**Diagnostic availability is hardware-dependent.** RF input level, BER, carrier frequency offset, LDPC errors and constellation coordinates may not be provided by the selected device or its protocol. Unsupported values are displayed as unavailable rather than estimated.

## v1.3.2 service patch

**v1.3.2** fixes a VLC transport-stream empty-queue busy loop that could make OpenTuner stop responding during receiver stop/restart or retuning, particularly with four receivers. The reader now uses atomic bulk dequeues, bounded waiting and one timeout warning instead of flooding the log. This fix was validated against a follow-up dual-PicoTuner log.

See [v1.3.2 Service Patch Notes](RELEASE_NOTES_1.3.2.md). The full user manual is available through the **HELP** button.

## New in v1.3.1

- Four simultaneous receivers using **two PicoTuner Ethernet devices**, each with a separate IP and UDP base port.
- Independent LNB power controls for each configured PicoTuner (subject to physical hardware).
- Improved receiver control layout and signal displays.
- Source Settings now show the relevant options for PicoTuner Ethernet, WinterHill UDP or WebSocket.
- PicoTuner Broadcast Listener can select each discovered unit by IP before changing its base port or sending reset/reboot commands.
- Main toolbar includes **MEDIA SETTINGS** and a **HELP** button opening an installed offline user manual.

**Documentation:** [OpenTuner User Manual](docs/OpenTuner-User-Manual.html) · [v1.3.1 Release Notes](RELEASE_NOTES_1.3.1.md)

## Download and install

1. Open the [GitHub Actions builds](https://github.com/makidoja/open_tuner/actions/workflows/modern-ui-build.yml).
2. Select a successful run for the **`connect-state-test`** branch.
3. Download its **Windows installer artifact** from the run's **Artifacts** section.
4. Install OpenTuner on Windows, select your media source, and configure **Source Settings** before connecting.

GitHub may require you to sign in to download workflow artifacts. A successful build verifies that the installer was produced; it **does not** confirm every receiver mode has been hardware-tested.

For the latest v1.3.1 installer build, select the newest **successful** workflow run on the development branch. Older artifacts may have outdated version numbers.

## QO-100 support

OpenTuner M0CKE includes BATC wideband spectrum integration and quick-tune features such as receiver markers, candidate selection, autoscan and optional rotation. These are intended to help find and switch between amateur DVB signals. Some tools currently focus on RX1/RX2, even while the four-receiver display is being developed.

## Development and testing notes

The **v1.3.1** development branch includes work on:

- Native standard-BATC Winter Hill UDP support for four channels.
- Separate PicoTuner Ethernet and USB receiver connectivity.
- Avoiding unnecessary new video windows and repeated native renderer reordering.
- Real transport-stream statistics and forwarded decoder metadata.
- Compact four-channel controls, header buttons and receiver information sidebars.
- Independent RX1–RX4 status handling.

**Still being validated:** layouts on different Windows DPI settings, hardware-specific LNB outputs, and advanced diagnostic availability across firmware versions.

Please include the **receiver model, firmware/software version, connection mode, Windows display scaling, screenshots and relevant logs** when reporting a problem.

## Links and credits

- [OpenTuner M0CKE repository](https://github.com/makidoja/open_tuner)
- [Development branch](https://github.com/makidoja/open_tuner/tree/connect-state-test)
- [Actions / Windows builds](https://github.com/makidoja/open_tuner/actions)
- [Original Open Tuner project — ZR6TG](https://www.zr6tg.co.za/open-tuner/)
- [BATC Wiki](https://wiki.batc.org.uk/)

Built on the work of the original Open Tuner contributors and the BATC DATV hardware/software community.

**73 de M0CKE**
