using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ItsFrosti.SpeedrunningMod;
using ItsFrosti.SpeedrunningMod.Seeds.Ruleset8;

// No Unity player or game process is started by this check.
internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    public static void Main()
    {
        var rules = new ItsFrosti.SpeedrunningMod.Seeds.ModSeedRuntimeProfile();
        Check(rules.DisableFishSchools && rules.FixBadMetal && rules.StalkerBitesDropTeeth && rules.ForceSecondGold && rules.StalkerToothDropProbability == 100f, "BetterRNG fixed rules changed.");
        string[] survival = Ruleset8SeedPool.Load(GameMode.Survival);
        Check(survival.Length == 1000 && survival.Distinct().Count() == 1000, "Survival pool changed.");
        foreach (string seed in survival) Check(Ruleset8SeedPool.Contains(GameMode.Survival, seed), "Pool rejects its own seed.");
        Check(!Ruleset8SeedPool.Contains(GameMode.Survival, "creative-20261125"), "Legacy Creative token accepted.");
        Check(!Ruleset8SeedPool.Contains(GameMode.Survival, "9223372036854775808"), "Overflowing seed accepted.");
        Check(!Ruleset8SeedPool.Contains(GameMode.Survival, "0"), "Unfiltered seed accepted.");
        for (int i = 0; i < 100; i++) Check(survival.Contains(Ruleset8SeedPool.Choose(GameMode.Survival)), "Selection escaped pool.");
        foreach (GameMode mode in new[] { GameMode.Hardcore, GameMode.Freedom })
        {
            Check(!Ruleset8SeedPool.HasSeeds(mode), "Unavailable race mode gained seeds.");
            Check(!Ruleset8SeedPool.Contains(mode, survival[0]), "Survival seeds leaked into another mode.");
            bool rejected = false;
            try { Ruleset8SeedPool.Choose(mode); } catch (IOException) { rejected = true; }
            Check(rejected, "Empty pool silently fell back to random seeds.");
        }
        string[] creative = Ruleset8SeedPool.Load(GameMode.Creative);
        Check(creative.Length == 10000 && creative.Distinct().Count() == 10000, "Creative list changed.");
        for (int i = 0; i < 100; i++) Check(creative.Contains(Ruleset8SeedPool.Choose(GameMode.Creative)), "Creative selection escaped pool.");
        Check(DeterministicHash.SeedFromText(" -1 ") == ulong.MaxValue, "Numeric seed mapping changed.");
        Check(DeterministicHash.SeedFromText("0") == 0, "Zero seed mapping changed.");
        Check(DeterministicHash.Mix(0) == 0xe220a8397b1dcdafUL, "SplitMix64 golden value changed.");
        var menu = new uGUI_MainMenu();
        ModClientSessionMode.IsRankedSingleplayerPracticeSelected = true;
        Check(Ruleset8RaceSession.Prepare(survival[0], GameMode.Survival), "Valid Survival seed rejected.");
        Check(Start(menu, GameMode.Survival), "Seeded Survival start blocked.");
        Check(SeedRuntime.Active, "Seeded start did not activate generation.");
        int activations = SeedRuntime.Activations;
        typeof(uGUI_MainMenu).GetField("isStartingNewGame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, true);
        Check(Start(menu, GameMode.Survival) && SeedRuntime.Activations == activations, "Double-click changed active seed.");
        typeof(uGUI_MainMenu).GetField("isStartingNewGame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(menu, false);
        ModClientSessionMode.IsRankedSingleplayerPracticeSelected = false;
        Check(Start(menu, GameMode.Survival) && !SeedRuntime.Active, "Race seed leaked into vanilla start.");
        ModClientSessionMode.IsBetterRngSingleplayerSelected = true;
        Check(Start(menu, GameMode.Hardcore) && !SeedRuntime.Active, "Race seeding interfered with BetterRNG.");
        ModClientSessionMode.IsBetterRngSingleplayerSelected = false;
        ModClientSessionMode.IsRankedSingleplayerPracticeSelected = true;
        Check(Start(menu, GameMode.Creative) && SeedRuntime.Active, "Filtered Creative start blocked.");
        Check(!Start(menu, GameMode.Hardcore), "Disabled Hardcore mode started a race.");
        Check(!SeedRuntime.Active, "Cancelled Hardcore start retained a previous seed.");
        Check(!Ruleset8RaceSession.Prepare(survival[0], GameMode.Hardcore), "Hardcore accepted a seed despite being disabled.");
        ModClientSessionMode.IsRankedSingleplayerPracticeSelected = false;
        ModClientSessionMode.IsRankedMultiplayerSelected = true;
        Check(!Start(menu, GameMode.Survival), "Multiplayer started without its assigned seed.");
        Check(Ruleset8RaceSession.Prepare(survival[1], GameMode.Survival), "Assigned multiplayer seed rejected.");
        Check(Start(menu, GameMode.Survival) && SeedRuntime.Active, "Valid multiplayer seed did not activate.");
        Check(!Ruleset8RaceSession.Prepare("creative-legacy", GameMode.Creative) && !SeedRuntime.Active, "Rejected seed retained active generation.");
        Console.WriteLine("PASS: seed pools, parsing, hash golden value, vanilla/BetterRNG isolation, valid race starts, empty-pool cancellation, double-click protection.");
    }
    private static bool Start(uGUI_MainMenu menu, GameMode mode)
    {
        object[] args = { menu, mode, null };
        bool allowed = (bool)typeof(Ruleset8RaceSession).GetMethod("StartNewGamePrefix", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        if (!allowed) Check(args[2] is System.Collections.IEnumerator, "Cancelled start did not return a coroutine.");
        return allowed;
    }
}

// Only enum/value types and the log sink required by these pure components.
public enum GameMode { None, Survival, Creative, Hardcore, Freedom }
namespace ItsFrosti.SpeedrunningMod
{
    internal sealed class Plugin
    {
        internal static readonly Log Logger = new Log();
        internal static readonly HarmonyLib.Harmony Harmony = new HarmonyLib.Harmony();
        internal static void LogError(string text, Exception ex) { }
    }
    internal static class ModClientSessionMode
    {
        internal static bool IsRankedSingleplayerPracticeSelected;
        internal static bool IsRankedMultiplayerSelected;
        internal static bool IsBetterRngSingleplayerSelected;
    }
    internal sealed class Log { public void Info(string text) { } public void Warning(string text) { } }
}
namespace UnityEngine
{
    public struct Vector3 { public float x, y, z; }
    public struct Quaternion { public float x, y, z, w; }
}

public sealed class uGUI_MainMenu
{
#pragma warning disable CS0414
    private bool isStartingNewGame = false;
#pragma warning restore CS0414
}
namespace HarmonyLib
{
    internal static class AccessTools
    {
        public static FieldInfo Field(Type type, string name) { return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); }
    }
    internal sealed class HarmonyMethod { public HarmonyMethod(MethodInfo method) { } }
    internal sealed class Harmony { public void Patch(MethodInfo target, HarmonyMethod prefix) { } }
}
namespace ItsFrosti.SpeedrunningMod.Seeds.Ruleset8
{
    // Test double for the native generator. Exercise the real start gate without
    // invoking Unity's native RNG or loading a world.
    internal static class SeedRuntime
    {
        internal static bool Active;
        internal static int Activations;
        internal static void SetPendingSeed(string value) { }
        internal static void Deactivate() { Active = false; }
        internal static void ActivatePendingForNewGame() { Active = true; Activations++; }
    }
}
