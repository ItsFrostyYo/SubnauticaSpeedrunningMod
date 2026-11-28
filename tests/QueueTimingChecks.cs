public static class QueueTimingChecks
{
    public static void Run()
    {
        var timing = new ItsFrosti.SpeedrunningMod.QueueTiming();
        timing.Start(100);
        Check(!timing.HasOpponent && timing.Elapsed(165) == "1:05", "Elapsed clock failed.");
        timing.Found("Opponent", 170);
        Check(!timing.ServerReady && timing.Countdown(170) == 0, "Countdown ran before server connection.");
        timing.Connected(172);
        Check(timing.Countdown(172) == 5 && timing.Countdown(172.1) == 5, "Countdown did not start at five after connecting.");
        timing.Found("Updated name", 175);
        timing.Connected(175);
        Check(timing.Countdown(175) == 2 && timing.OpponentName == "Updated name", "A poll reset the countdown.");
        Check(timing.Countdown(176.99) == 1 && timing.Countdown(177) == 0, "Five-second transition deadline failed.");
        timing.Start(250);
        Check(!timing.HasOpponent && !timing.ServerReady && timing.Elapsed(250) == "0:00", "New queue retained old match state.");
        timing.Found("Slow connection", 251);
        timing.Connected(256.1);
        Check(timing.ServerReady && timing.Countdown(256.1) == 0, "Slow connection added another countdown.");
        timing.Start(300);
        timing.Found("Unavailable server", 301);
        Check(!timing.ConnectionTimedOut(420.9) && timing.ConnectionTimedOut(421), "Unavailable server had no bounded timeout.");
    }
    private static void Check(bool value, string message) { if (!value) throw new System.Exception(message); }
}
