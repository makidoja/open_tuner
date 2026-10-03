using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;

namespace opentuner
{
    public delegate void FlushTS();
    public delegate byte ReadTS(ref byte[] data, ref uint BytesRead);

    public class TSThread
    {
        private volatile bool ts_build_queue = false;
        private readonly List<CircularBuffer> registered_consumers = new List<CircularBuffer>();
        private readonly FlushTS flush_ts_callback;
        private readonly ReadTS read_ts_callback;
        private readonly string identifier;

        private readonly EventWaitHandle thread_wait_event_handle;
        private volatile bool worker_thread_stopped = false;
        private volatile bool shutdown_worker_thread = false;

        public TSThread(CircularBuffer _raw_ts_data_queue, FlushTS _flush_ts_callback, ReadTS _read_ts_callback, string _identifier)
        {
            Log.Information(" >> Starting TS Thread <<");
            Log.Information(" >> Registering Raw TS Queue << ");
            thread_wait_event_handle = new EventWaitHandle(false, EventResetMode.AutoReset);
            registered_consumers.Add(_raw_ts_data_queue);
            flush_ts_callback = _flush_ts_callback;
            read_ts_callback = _read_ts_callback;
            identifier = _identifier;
        }

        public void NewDataPresent()
        {
            thread_wait_event_handle.Set();
        }

        public void RegisterTSConsumer(CircularBuffer raw_ts_data_queue)
        {
            if (raw_ts_data_queue == null) return;
            Log.Information(" >> Registering New Queue << ");
            lock (registered_consumers)
                registered_consumers.Add(raw_ts_data_queue);
        }

        public void stop_ts()
        {
            Log.Information("Stopping TS: " + identifier);
            ts_build_queue = false;
            thread_wait_event_handle.Set();
        }

        public void start_ts()
        {
            Log.Information("Starting TS:" + identifier);
            ts_build_queue = true;
            thread_wait_event_handle.Set();
        }

        public void Stop(ref bool stopped)
        {
            Log.Verbose("TS Thread: Stopping Worker Thread...");
            ts_build_queue = false;
            shutdown_worker_thread = true;
            thread_wait_event_handle.Set();

            for (int count = 0; count < 20 && !worker_thread_stopped; count++)
                Thread.Sleep(50);

            stopped = worker_thread_stopped;
        }

        public void worker_thread()
        {
            bool bufferingData = false;
            Log.Information(">> Starting TS Worker Thread <<");

            try
            {
                byte[] data = new byte[4096];

                while (!shutdown_worker_thread)
                {
                    thread_wait_event_handle.WaitOne();

                    if (!ts_build_queue)
                    {
                        if (flush_ts_callback != null) flush_ts_callback();
                        bufferingData = false;
                        ClearConsumers();
                        continue;
                    }

                    if (!bufferingData)
                    {
                        if (flush_ts_callback != null) flush_ts_callback();
                        ClearConsumers();
                        bufferingData = true;
                    }

                    // Drain all currently available chunks before going back to sleep.
                    while (ts_build_queue && !shutdown_worker_thread)
                    {
                        uint dataRead = 0;
                        if (read_ts_callback(ref data, ref dataRead) != 0)
                            Log.Information("Read Error");

                        if (dataRead == 0)
                            break;

                        int length = (int)Math.Min(dataRead, (uint)data.Length);
                        lock (registered_consumers)
                        {
                            for (int i = 0; i < registered_consumers.Count; i++)
                            {
                                CircularBuffer consumer = registered_consumers[i];
                                if (consumer != null)
                                    consumer.Enqueue(data, 0, length);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "TS worker thread failed: " + identifier);
            }
            finally
            {
                worker_thread_stopped = true;
                Log.Information("TS worker thread stopped: " + identifier);
            }
        }

        private void ClearConsumers()
        {
            lock (registered_consumers)
            {
                for (int i = 0; i < registered_consumers.Count; i++)
                    registered_consumers[i]?.Clear();
            }
        }
    }
}
