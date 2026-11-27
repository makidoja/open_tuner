using System;

namespace opentuner
{
    internal sealed class ModernTunerDiagnosticSnapshot
    {
        public byte DemodStatus;
        public uint Modcode;
        public byte[,] Constellation;

        // Signal tab meters (MiniTiouner / PicoTuner USB only).
        public bool MetersValid;
        public bool CarrierLock;
        public bool TimingLock;
        public bool FullLock;
        public int CarrierLevel;   // LDI, signed 8-bit
        public int TimingLevel;    // TMGLOCK level, signed 16-bit
        public int RfDbm;
        public int MerTenths;
        public long MeterSequence;
        public DateTime MeterUpdatedUtc;
    }

    internal static class ModernTunerDiagnostics
    {
        private static readonly object Sync = new object();
        private static readonly ModernTunerDiagnosticSnapshot[] Snapshots =
        {
            new ModernTunerDiagnosticSnapshot(),
            new ModernTunerDiagnosticSnapshot(),
            new ModernTunerDiagnosticSnapshot(),
            new ModernTunerDiagnosticSnapshot()
        };

        public static void UpdateDemodStatus(int tuner, byte value)
        {
            if (tuner < 0 || tuner >= Snapshots.Length) return;
            lock (Sync) Snapshots[tuner].DemodStatus = value;
        }

        public static void UpdateModcode(int tuner, uint value)
        {
            if (tuner < 0 || tuner >= Snapshots.Length) return;
            lock (Sync) Snapshots[tuner].Modcode = value;
        }

        public static void UpdateConstellation(int tuner, byte[,] value)
        {
            if (tuner < 0 || tuner >= Snapshots.Length) return;
            lock (Sync) Snapshots[tuner].Constellation = Clone(value);
        }

        private static long meterSequence;
        private static long fastMetersRequestedTicks;

        // Called from the NIM thread at every meter read (fast loop and full status poll).
        public static void UpdateSignalMeters(int tuner, byte dstatus, int carrierLevel,
            int timingLevel, int rfDbm, int merTenths)
        {
            if (tuner < 0 || tuner >= Snapshots.Length) return;
            lock (Sync)
            {
                ModernTunerDiagnosticSnapshot s = Snapshots[tuner];
                s.MetersValid = true;
                s.CarrierLock = (dstatus & 0x80) != 0;
                s.TimingLock = ((dstatus >> 5) & 0x03) >= 2;
                s.FullLock = (dstatus & 0x08) != 0;
                s.CarrierLevel = carrierLevel;
                s.TimingLevel = timingLevel;
                s.RfDbm = rfDbm;
                s.MerTenths = merTenths;
                s.MeterSequence = ++meterSequence;
                s.MeterUpdatedUtc = DateTime.UtcNow;
            }
        }

        // The Signal tab calls this while it is on screen. The NIM thread only
        // runs its extra fast meter reads while a request is recent, so there is
        // no added USB/I2C traffic when nobody is looking at the meters.
        public static void RequestFastMeters()
        {
            System.Threading.Interlocked.Exchange(ref fastMetersRequestedTicks, DateTime.UtcNow.Ticks);
        }

        public static bool FastMetersRequested
        {
            get
            {
                long ticks = System.Threading.Interlocked.Read(ref fastMetersRequestedTicks);
                return ticks != 0 && (DateTime.UtcNow.Ticks - ticks) < TimeSpan.TicksPerSecond;
            }
        }

        public static ModernTunerDiagnosticSnapshot GetSnapshot(int tuner)
        {
            if (tuner < 0 || tuner >= Snapshots.Length) return null;
            lock (Sync)
            {
                ModernTunerDiagnosticSnapshot s = Snapshots[tuner];
                return new ModernTunerDiagnosticSnapshot
                {
                    DemodStatus = s.DemodStatus,
                    Modcode = s.Modcode,
                    Constellation = Clone(s.Constellation),
                    MetersValid = s.MetersValid,
                    CarrierLock = s.CarrierLock,
                    TimingLock = s.TimingLock,
                    FullLock = s.FullLock,
                    CarrierLevel = s.CarrierLevel,
                    TimingLevel = s.TimingLevel,
                    RfDbm = s.RfDbm,
                    MerTenths = s.MerTenths,
                    MeterSequence = s.MeterSequence,
                    MeterUpdatedUtc = s.MeterUpdatedUtc
                };
            }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                for (int i = 0; i < Snapshots.Length; i++)
                {
                    Snapshots[i].DemodStatus = 0;
                    Snapshots[i].Modcode = 0;
                    Snapshots[i].Constellation = null;
                    Snapshots[i].MetersValid = false;
                    Snapshots[i].MeterUpdatedUtc = DateTime.MinValue;
                }
            }
        }

        private static byte[,] Clone(byte[,] value)
        {
            if (value == null) return null;
            return (byte[,])value.Clone();
        }
    }
}
