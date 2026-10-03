using Serilog;
using System;

namespace opentuner
{
    public class CircularBuffer
    {
        private readonly byte[] buffer;
        private int head;
        private int tail;
        private readonly int capacity;
        private readonly object syncRoot = new object();

        public int Count { get; private set; }

        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Buffer capacity must be positive.", nameof(capacity));

            this.capacity = capacity;
            buffer = new byte[capacity];
        }

        public void Enqueue(byte item)
        {
            lock (syncRoot)
            {
                if (Count == capacity)
                {
                    tail = (tail + 1) % capacity;
                    Count--;
                }

                buffer[head] = item;
                head = (head + 1) % capacity;
                Count++;
            }
        }

        public void Enqueue(byte[] items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            Enqueue(items, 0, items.Length);
        }

        public void Enqueue(byte[] items, int offset, int count)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (offset < 0 || count < 0 || offset + count > items.Length)
                throw new ArgumentOutOfRangeException();
            if (count == 0) return;

            lock (syncRoot)
            {
                // If more than a complete buffer arrives, retain the newest bytes only.
                if (count >= capacity)
                {
                    offset += count - capacity;
                    count = capacity;
                    head = tail = Count = 0;
                }

                int overflow = (Count + count) - capacity;
                if (overflow > 0)
                {
                    tail = (tail + overflow) % capacity;
                    Count -= overflow;
                }

                int first = Math.Min(count, capacity - head);
                Array.Copy(items, offset, buffer, head, first);
                int remaining = count - first;
                if (remaining > 0)
                    Array.Copy(items, offset + first, buffer, 0, remaining);

                head = (head + count) % capacity;
                Count += count;
            }
        }

        public byte Dequeue()
        {
            lock (syncRoot)
            {
                if (Count == 0)
                {
                    Log.Warning("CircularBuffer.Dequeue: Buffer is empty.");
                    return 0;
                }

                byte item = buffer[tail];
                tail = (tail + 1) % capacity;
                Count--;
                return item;
            }
        }

        public int Dequeue(byte[] destination, int offset, int count)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (offset < 0 || count < 0 || offset + count > destination.Length)
                throw new ArgumentOutOfRangeException();

            lock (syncRoot)
            {
                int bytesToRead = Math.Min(count, Count);
                if (bytesToRead == 0) return 0;

                int first = Math.Min(bytesToRead, capacity - tail);
                Array.Copy(buffer, tail, destination, offset, first);
                int remaining = bytesToRead - first;
                if (remaining > 0)
                    Array.Copy(buffer, 0, destination, offset + first, remaining);

                tail = (tail + bytesToRead) % capacity;
                Count -= bytesToRead;
                return bytesToRead;
            }
        }

        public byte Peek()
        {
            lock (syncRoot)
            {
                if (Count == 0)
                {
                    Log.Warning("CircularBuffer.Peek: Buffer is empty.");
                    return 0;
                }
                return buffer[tail];
            }
        }

        public byte TryPeek()
        {
            lock (syncRoot)
                return Count == 0 ? (byte)0 : buffer[tail];
        }

        public byte[] DequeueBytes(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            lock (syncRoot)
            {
                int bytesToRead = Math.Min(count, Count);
                if (bytesToRead == 0) return new byte[0];
                byte[] result = new byte[bytesToRead];
                int first = Math.Min(bytesToRead, capacity - tail);
                Array.Copy(buffer, tail, result, 0, first);
                int remaining = bytesToRead - first;
                if (remaining > 0) Array.Copy(buffer, 0, result, first, remaining);
                tail = (tail + bytesToRead) % capacity;
                Count -= bytesToRead;
                return result;
            }
        }

        public void Clear()
        {
            lock (syncRoot)
                head = tail = Count = 0;
        }
    }
}
