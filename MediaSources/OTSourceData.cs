namespace opentuner.MediaSources
{
    public class OTSourceData
    {
        public int video_number = 0;
        public bool demod_locked = false;
        public string demode_state = "";
        public string delivery_system = "";
        public double db_margin = 0.0;
        public double mer = 0.0;
        public long frequency = 0;
        public int symbol_rate = 0;
        public string service_name = "";
        public string service_provider = "";
        public string modcode = "";
        public string video_codec = "";
        public string video_resolution = "";
        public string audio_codec = "";
        public string audio_rate = "";
        public double ts_bitrate_mbps = 0.0;
        public double null_packets_percent = 0.0;
        public int volume = 0;
        public bool streaming = false;
        public bool recording = false;

        // Optional demodulator symbol coordinates. These are diagnostic points only,
        // not raw RF/baseband I/Q. Sources that cannot expose real constellation data
        // leave this null and the UI must not synthesise it from MER or transport data.
        public byte[,] constellation = null;
    }
}
