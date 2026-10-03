using System;

namespace opentuner
{
    internal sealed class ModernTunerDiagnosticSnapshot
    {
        public byte DemodStatus;
        public uint Modcode;
        public byte[,] Constellation;
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
                    Constellation = Clone(s.Constellation)
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
