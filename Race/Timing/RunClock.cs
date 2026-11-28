using System;
using System.Diagnostics;

namespace ItsFrosti.SpeedrunningMod.RunTracking
{
    // Only the HUD refresh depends on frames. Timing uses the monotonic OS clock.
    internal sealed class RunClock
    {
        private readonly Func<long> timestamp;
        private readonly double frequency;
        private long started, stopped, removalStarted, removed;
        internal bool Running { get; private set; }
        internal bool Removing { get; private set; }

        internal RunClock() : this(Stopwatch.GetTimestamp, Stopwatch.Frequency) { }
        internal RunClock(Func<long> timestamp, long frequency)
        {
            if (timestamp == null || frequency <= 0) throw new ArgumentException("Invalid clock source.");
            this.timestamp = timestamp;
            this.frequency = frequency;
        }
        internal long CaptureTimestamp() { return timestamp(); }
        internal void Start() { StartAt(timestamp()); }
        internal void StartAt(long startTimestamp)
        {
            if (Running) return;
            Reset();
            started = stopped = startTimestamp;
            Running = true;
        }
        internal void SetRemoving(bool value)
        {
            value &= Running;
            if (Removing == value) return;
            long now = timestamp();
            if (value) removalStarted = now;
            else removed += Math.Max(0, now - removalStarted);
            Removing = value;
        }
        internal void Read(out double elapsed, out double realTime)
        {
            long now = Running ? timestamp() : stopped;
            long total = Math.Max(0, now - started);
            long excluded = removed + (Removing ? Math.Max(0, now - removalStarted) : 0);
            realTime = total / frequency;
            elapsed = Math.Max(0, total - excluded) / frequency;
        }
        internal void Stop()
        {
            if (!Running) return;
            // Use one timestamp for both the final paused interval and finish.
            stopped = timestamp();
            if (Removing) removed += Math.Max(0, stopped - removalStarted);
            Removing = Running = false;
        }
        internal void Reset()
        {
            Running = Removing = false;
            started = stopped = removalStarted = removed = 0;
        }
    }
}
