namespace ItsFrosti.SpeedrunningMod.RunTracking
{
    internal enum ModSingleplayerRunMode { None, Creative, Survival, Hardcore }

    internal static class RunTimingEligibility
    {
        // Race and BetterRNG share mode selection so Creative cannot be silently
        // excluded by a separate gate before its movement-based timer is reached.
        internal static ModSingleplayerRunMode Resolve(GameMode mode, bool raceSelected, bool betterRngSelected, bool continueMode)
        {
            if (continueMode || (!raceSelected && !betterRngSelected)) return ModSingleplayerRunMode.None;
            switch (mode)
            {
                case GameMode.Creative: return ModSingleplayerRunMode.Creative;
                case GameMode.Survival: return ModSingleplayerRunMode.Survival;
                case GameMode.Hardcore: return ModSingleplayerRunMode.Hardcore;
                default: return ModSingleplayerRunMode.None;
            }
        }
    }
}
