using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Serilog;

namespace opentuner
{
    public class TSStreamMediaInput : LibVLCSharp.Shared.MediaInput
    {

        CircularBuffer ts_data_queue;
        public bool ts_sync = false;
        public volatile bool end = false;

        public TSStreamMediaInput(CircularBuffer _ts_data_queue )
        {
            // we can't seek live data
            CanSeek = false;
            ts_data_queue = _ts_data_queue;
        }

        public override bool Open(out ulong size)
        {
            Log.Information("Open Media Input");

            bool success = true;
            size = ulong.MaxValue;

            return success;
        }

        public override void Close()
        {
            Log.Information("Close Media Input");
        }


        public override int Read(IntPtr buf, uint len)
        {
            if (end || len == 0) return 0;

            // Every dequeue is performed atomically by CircularBuffer. Count
            // can change between a separate Count check and Dequeue when
            // another consumer clears the queue (retuning, stop/recovery).
            // The old loop never advanced if Count became zero, repeatedly
            // logging "Failing to dequeue" and hanging the VLC input thread.
            int request = (int)Math.Min(len, 65536U);
            byte[] data = new byte[request];
            int waitMs = 0;
            const int maxWaitMs = 10000;
            const int pollMs = 5;

            while (!end)
            {
                int received = ts_data_queue.Dequeue(data, 0, request);
                if (received > 0)
                {
                    int start = 0;
                    if (!ts_sync)
                    {
                        // Locate a TS sync byte without fabricating any bytes.
                        // A retune may temporarily start in the middle of a
                        // packet; VLC can resynchronise from the real stream.
                        while (start < received && data[start] != 0x47)
                            start++;
                        if (start == received)
                            continue;
                        ts_sync = true;
                    }

                    int available = received - start;
                    Marshal.Copy(data, start, buf, available);
                    return available;
                }

                // No bytes currently available: wait, never spin or spam the
                // log. A read timeout ends this VLC input; new playback on
                // retune creates a fresh TSStreamMediaInput.
                if (waitMs >= maxWaitMs)
                {
                    Log.Warning("TSStreamMediaInput: no transport data for {WaitMs} ms; ending current media input", waitMs);
                    return 0;
                }
                Thread.Sleep(pollMs);
                waitMs += pollMs;
            }
            return 0;
        }

        public override bool Seek(ulong offset)
        {
            // seeking is not allowed/possible
            Log.Information("VLC Trying to Seek" + offset.ToString());
            return false;
        }

    }
}
