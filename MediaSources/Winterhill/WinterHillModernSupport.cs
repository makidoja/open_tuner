using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace opentuner.MediaSources.WinterHill
{
    public partial class WinterHillSource
    {
        private long lastPicoStatusTicks;

        public bool ModernPicoStatusLive
        {
            get
            {
                long ticks = System.Threading.Interlocked.Read(ref lastPicoStatusTicks);
                return ticks > 0 && DateTime.UtcNow.Ticks - ticks < TimeSpan.FromSeconds(5).Ticks;
            }
        }

        public void ModernRetryPicoConnection()
        {
            if (!ModernIsPicoTunerEthernet) return;
            ModernDiscoverPicoTuner(1200);
            ModernReapplyStartupTune();
        }

        public string ModernConfiguredPicoAddress { get { return _settings == null ? "" : _settings.WinterHillUdpHost; } }

        public bool ModernIsPicoTunerEthernet
        {
            get { return _settings != null && _settings.DefaultInterface == 2; }
        }

        public string ModernInterfaceDescription
        {
            get
            {
                if (_settings == null) return "WinterHill";
                switch (_settings.DefaultInterface)
                {
                    case 1: return "WebSocket (ZR6TG)";
                    case 2: return "PicoTuner Ethernet (G4EWJ)";
                    default: return "Always Ask";
                }
            }
        }

        public void ModernSetVolume(int tuner, int volume)
        {
            if (_settings == null || tuner < 0 || tuner >= _settings.DefaultVolume.Length)
                return;

            volume = System.Math.Max(0, System.Math.Min(100, volume));

            if (volume <= 0)
            {
                _settings.DefaultMuted[tuner] = true;
                muted[tuner] = true;
            }
            else
            {
                _settings.DefaultMuted[tuner] = false;
                _settings.DefaultVolume[tuner] = (uint)volume;
                preMute[tuner] = volume;
                muted[tuner] = false;
            }

            if (_media_player != null && tuner < _media_player.Length && _media_player[tuner] != null)
            {
                try { _media_player[tuner].SetVolume(volume); } catch { }
            }

            try { _settingsManager.SaveSettings(_settings); } catch { }
        }

        public int ModernGetVolume(int tuner)
        {
            if (_settings == null || tuner < 0 || tuner >= _settings.DefaultVolume.Length)
                return 0;

            if (_media_player != null && tuner < _media_player.Length && _media_player[tuner] != null)
            {
                try { return _media_player[tuner].GetVolume(); } catch { }
            }

            return _settings.DefaultMuted[tuner] ? 0 : (int)_settings.DefaultVolume[tuner];
        }

        public void ModernEnsureAudible(int tuner, int fallbackVolume)
        {
            if (tuner < 0 || tuner >= _settings.DefaultVolume.Length)
                return;

            int volume = ModernGetVolume(tuner);
            if (volume <= 0)
                volume = fallbackVolume;

            ModernSetVolume(tuner, volume);
        }

        // PicoTuner sends a regular discovery broadcast on UDP 9997. The UDP sender
        // address is authoritative and is safer than parsing the human-readable payload,
        // which some firmware versions can format in a way that drops part of the address.
        public bool ModernDiscoverPicoTuner(int timeoutMs)
        {
            // Discovery is owned by the shared broadcast listener.
            // Do not open a second socket on UDP 9997.
            try
            {
                PicoWHBroadcastListener.EnsureStarted();
                string address = PicoWHBroadcastListener.LastAddress;
                if (PicoWHBroadcastListener.RecentlySeen(address, 10) &&
                    !string.IsNullOrWhiteSpace(address))
                {
                    runtimeUdpHost = address;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Information("PicoTuner shared discovery unavailable: " + ex.Message);
            }
            return false;
        }

        public void ModernReapplyStartupTune()
        {
            if (!ModernIsPicoTunerEthernet)
                return;

            if (_settings != null && _settings.LNBVoltage != null)
            {
                int lnbCount = System.Math.Min(2, _settings.LNBVoltage.Length);
                for (int output = 0; output < lnbCount; output++)
                {
                    Serilog.Log.Information(
                        "Reapplying PicoTuner LNB {Output} power: {Voltage} V",
                        output == 0 ? "A" : "B", _settings.LNBVoltage[output]);
                    UDPSetVoltage(output, _settings.LNBVoltage[output]);
                }
            }

            int count = System.Math.Min(2, _current_frequency.Length);
            for (int tuner = 0; tuner < count; tuner++)
            {
                if (_current_frequency[tuner] <= 0 || _current_sr[tuner] <= 0)
                    continue;

                Serilog.Log.Information(
                    "Reapplying PicoTuner startup tune T{Tuner}: {Frequency} kHz / {SymbolRate} kS",
                    tuner + 1, _current_frequency[tuner], _current_sr[tuner]);

                UDPSetFrequency(tuner, _current_frequency[tuner], _current_sr[tuner]);
            }
        }
    }
}
