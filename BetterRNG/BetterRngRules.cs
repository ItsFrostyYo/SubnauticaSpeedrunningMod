namespace ItsFrosti.SpeedrunningMod.Seeds
{
    // Fixed values from the original BetterRNG preset. No XML seed profiles.
    internal sealed class ModSeedRuntimeProfile
    {
        internal bool DisableFishSchools { get { return true; } }
        internal float StalkerToothDropProbability { get { return 100f; } }
        internal bool FixBadMetal { get { return true; } }
        internal bool StalkerBitesDropTeeth { get { return true; } }
        internal bool ForceSecondGold { get { return true; } }
    }
}
