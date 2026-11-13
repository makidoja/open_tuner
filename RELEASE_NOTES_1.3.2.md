# OpenTuner M0CKE v1.3.2 — Service Patch

**Maintenance release following v1.3.1.**

## Fixed

- Fixed a possible **application freeze / Not Responding** condition in the VLC transport-stream input, especially during stream stop, retune, or rapid changes in multi-receiver mode.
- Removed the transport stream reader's **infinite busy loop** when a queue becomes empty during playback or shutdown. Previously, the log could fill with thousands of `Warning: Failing to dequeue, nothing to dequeue: TSStream` messages in milliseconds.
- The VLC input now uses **atomic, thread-safe bulk buffer reads** and returns only bytes actually read.
- Empty buffers no longer cause CPU-intensive spinning or log flooding; the reader waits briefly for the next transport data.
- Added a bounded, **10-second no-data timeout** for the active media input when data stops completely.

## Validation

Tested with a **dual-PicoTuner Ethernet / four-receiver** configuration. The supplied follow-up log contains **no recurrence of the original empty-queue warning flood**, and the tester reports normal operation.

Some existing cleanup-only messages (e.g. `ThreadAbortException` on stopping workers and UDP listener errors during disconnect) may still appear and are not addressed in this narrowly scoped service patch.

## Included from v1.3.1

- Two independently configured Ethernet PicoTuners (four live receivers)
- Independent tuner controls, LNB options, 2×2 video layout
- Source-specific settings, IP-selectable PicoTuner Broadcast Listener
- QO-100 BATC spectrum, BATC Chat, recording, snapshots, presets
- New HELP button and offline HTML manual

## Install / update

Download `OpenTuner-M0CKE-1.3.2-Setup.exe` and run the installer. Existing settings are preserved. A standalone HTML manual and SHA256 checksum accompany this release.

**73 de M0CKE**
