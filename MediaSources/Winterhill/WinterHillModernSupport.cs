using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace opentuner.MediaSources.WinterHill
{
    public partial class WinterHillSource
    {
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

        // PicoTuner sends a regular discovery broadcast on UDP 9997 containing its
        // current IP address. Listen briefly before the first tune command so a stale
        // WinterHillUdpHost (for example the Windows PC's own address) cannot create a
        // chicken-and-egg situation where no status arrives and therefore the correct
        // receiver address is never learned.
        public bool ModernDiscoverPicoTuner(int timeoutMs)
        {
            if (!ModernIsPicoTunerEthernet)
                return false;

            UdpClient listener = null;
            try
            {
                listener = new UdpClient(9997);
                listener.Client.ReceiveTimeout = System.Math.Max(250, timeoutMs);

                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] bytes = listener.Receive(ref remote);
                string text = Encoding.ASCII.GetString(bytes);

                string detectedIp = null;
                string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.IndexOf("IP address", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int colon = line.IndexOf(':');
                        if (colon >= 0 && colon + 1 < line.Length)
                            detectedIp = line.Substring(colon + 1).Trim();
                        else if (line.Length > 17)
                            detectedIp = line.Substring(17).Trim();
                    }
                }

                IPAddress parsed;
                if (string.IsNullOrWhiteSpace(detectedIp) || !IPAddress.TryParse(detectedIp, out parsed))
                {
                    // The sender address is also authoritative for the PicoTuner broadcast.
                    detectedIp = remote.Address.ToString();
                }

                if (!string.IsNullOrWhiteSpace(detectedIp))
                {
                    runtimeUdpHost = detectedIp;
                    Serilog.Log.Warning("PicoTuner discovered at " + detectedIp + " - using this address for startup control");
                    return true;
                }
            }
            catch (SocketException ex)
            {
                // Timeout or another listener already using 9997: retain configured host
                // and allow the normal live-status learning path to correct it later.
                Serilog.Log.Information("PicoTuner discovery did not complete: " + ex.Message);
            }
            catch (Exception ex)
            {
                Serilog.Log.Information("PicoTuner discovery skipped: " + ex.Message);
            }
            finally
            {
                try { listener?.Close(); } catch { }
            }

            return false;
        }

        // PicoTuner status packets teach the modern build the receiver's real IP address.
        // Once status is flowing, send the exact same current tuner setup again as a
        // second safety net in case startup discovery was unavailable.
        public void ModernReapplyStartupTune()
        {
            if (!ModernIsPicoTunerEthernet)
                return;

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
