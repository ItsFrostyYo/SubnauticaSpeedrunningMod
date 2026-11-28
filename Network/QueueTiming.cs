using System;
using System.Globalization;

namespace ItsFrosti.SpeedrunningMod
{
    internal sealed class QueueTiming
    {
        private double startedAt;
        private double foundAt = -1;
        private double readyAt = -1;
        private bool skipCountdown;
        internal bool HasOpponent { get { return foundAt >= 0; } }
        internal bool ServerReady { get { return readyAt >= 0; } }
        internal string OpponentName { get; private set; }
        internal void Start(double now) { startedAt = now; foundAt = readyAt = -1; skipCountdown = false; OpponentName = ""; }
        internal void Found(string name, double now)
        {
            if (!HasOpponent) foundAt = now;
            if (!string.IsNullOrEmpty(name)) OpponentName = name;
        }
        internal void Connected(double now)
        {
            if (!HasOpponent || ServerReady) return;
            readyAt = now;
            skipCountdown = now - foundAt > 5;
        }
        internal bool ConnectionTimedOut(double now) { return HasOpponent && !ServerReady && now - foundAt >= 120; }
        internal int Countdown(double now) { return ServerReady && !skipCountdown ? Math.Max(0, (int)Math.Ceiling(5 - (now - readyAt))) : 0; }
        internal string Elapsed(double now)
        {
            long seconds = (long)Math.Max(0, now - startedAt);
            return (seconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
