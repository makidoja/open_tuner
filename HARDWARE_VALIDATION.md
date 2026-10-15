# OpenTuner M0CKE 0.C.v1.0.2 — hardware validation

Current development branch: `modern-ui`. Do not start v2.0 Standard/Enhanced branch split until this baseline is verified on real hardware.

## 1. PicoTuner USB recognition (priority)
1. Confirm both PicoTuner USB functions enumerate in Windows Device Manager and drivers are usable.
2. In MiniTiouner Source Settings choose **PicoTuner** as the Hardware Interface (not FTDI).
3. Connect, then capture the application log lines beginning `PicoTuner USB scan:`, and any `I2C interface not found`, `TS interface not found` or `endpoint not found` messages.
4. If discovery fails, note Device Manager hardware IDs (VID/PID), device names and driver names for both USB functions. Do not change drivers without identifying what is installed.
5. Check that both transport-stream inputs can receive a known signal.

## 2. USB MiniTiouner RF selection
Verify Tuner A then Tuner B on real RF inputs. Confirm the selector remains selected after automatic refresh, and changing RF input retunes the expected channel.

## 3. Constellation
With a known DVB-S2 QPSK locked signal, record MER, modulation, lock state and screenshot of the constellation. Expect four clusters if valid demodulator I/Q samples are returned. If random points persist, capture raw I/Q bytes and check STV0910 sampling semantics; do not synthesise a constellation. Test each demodulator independently.

## 4. Layout and regression
At 100% and 125% Windows scaling, verify constellation checkbox and receiver details do not overlap. Confirm video, audio, recording, presets and source switching still work.

## Build status
GitHub Actions compilation is a packaging/build check only, **not** verification of USB enumeration, RF reception, or correct constellation clustering. 
