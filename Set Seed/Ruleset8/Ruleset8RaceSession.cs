using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace ItsFrosti.SpeedrunningMod.Seeds.Ruleset8
{
    internal static class Ruleset8RaceSession
    {
        private static readonly FieldInfo StartingNewGameField = AccessTools.Field(typeof(uGUI_MainMenu), "isStartingNewGame");
        internal static bool IsStartingNewGame(uGUI_MainMenu menu)
        {
            return menu != null && StartingNewGameField != null && (bool)StartingNewGameField.GetValue(menu);
        }

        private static bool _installAttempted;
        private static bool _installed;
        private static string _pendingSeed = string.Empty;
        private static GameMode _pendingMode = GameMode.None;

        internal static bool EnsureInstalled()
        {
            if (_installAttempted)
            {
                return _installed;
            }

            _installAttempted = true;
            try
            {
                MethodInfo target = typeof(uGUI_MainMenu).GetMethod("StartNewGame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo prefix = typeof(Ruleset8RaceSession).GetMethod("StartNewGamePrefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (target == null || prefix == null)
                {
                    throw new MissingMethodException("Could not resolve uGUI_MainMenu.StartNewGame for Ruleset 8.");
                }

                Plugin.Harmony.Patch(target, new HarmonyMethod(prefix));
                _installed = true;
                Plugin.Logger.Info("Ruleset 8 race session hook installed.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.Warning("Ruleset 8 race session hook failed: " + ex);
            }

            return _installed;
        }

        internal static bool Prepare(string seed, GameMode mode)
        {
            long numeric;
            if (string.IsNullOrEmpty(seed) || !long.TryParse(seed.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
            {
                Plugin.Logger.Warning("Rejected non-numeric Ruleset 8 race seed '" + (seed ?? string.Empty) + "'.");
                Reset();
                return false;
            }

            if (!Ruleset8SeedPool.Contains(mode, seed))
            {
                Plugin.Logger.Warning("Rejected seed outside the filtered " + mode + " race pool.");
                Reset();
                return false;
            }
            _pendingMode = mode;
            _pendingSeed = numeric.ToString(CultureInfo.InvariantCulture);
            SeedRuntime.SetPendingSeed(_pendingSeed);
            return true;
        }

        internal static bool PrepareSingleplayerFromPool(GameMode mode)
        {
            try
            {
                string seed = Ruleset8SeedPool.Choose(mode);
                if (!Prepare(seed, mode))
                {
                    return false;
                }

                Plugin.Logger.Info("Selected Ruleset 8 seed '" + _pendingSeed + "' for Singleplayer Race " + mode + ".");
                return true;
            }
            catch (Exception ex)
            {
                Reset();
                Plugin.LogError("Could not select a Ruleset 8 Singleplayer Race seed.", ex);
                return false;
            }
        }

        internal static bool ShouldActivateForNewGame()
        {
            return !string.IsNullOrEmpty(_pendingSeed) &&
                (ModClientSessionMode.IsRankedSingleplayerPracticeSelected || ModClientSessionMode.IsRankedMultiplayerSelected) &&
                !ModClientSessionMode.IsBetterRngSingleplayerSelected;
        }

        internal static void Reset()
        {
            if (!string.IsNullOrEmpty(_pendingSeed) || SeedRuntime.Active)
            {
                Plugin.Logger.Info("Cleared Ruleset 8 race seed session.");
            }
            _pendingSeed = string.Empty;
            _pendingMode = GameMode.None;
            SeedRuntime.Deactivate();
        }

        private static bool StartNewGamePrefix(uGUI_MainMenu __instance, GameMode gameMode, ref System.Collections.IEnumerator __result)
        {
            if (IsStartingNewGame(__instance)) return true;
            if (!ModClientSessionMode.IsRankedSingleplayerPracticeSelected &&
                !ModClientSessionMode.IsRankedMultiplayerSelected)
            {
                Reset();
                return true;
            }
            if (ModClientSessionMode.IsRankedSingleplayerPracticeSelected &&
                (string.IsNullOrEmpty(_pendingSeed) || _pendingMode != gameMode) &&
                !PrepareSingleplayerFromPool(gameMode))
            {
                __result = CancelledStart();
                return false;
            }
            if (_pendingMode != gameMode || !Ruleset8SeedPool.Contains(gameMode, _pendingSeed))
            {
                Reset();
                __result = CancelledStart();
                Plugin.Logger.Warning("Cancelled race start without a filtered " + gameMode + " seed.");
                return false;
            }

            if (ShouldActivateForNewGame())
            {
                SeedRuntime.ActivatePendingForNewGame();
                Plugin.Logger.Info("Starting " + gameMode + " race with Ruleset 8 seed '" + _pendingSeed + "'.");
            }
            return true;
        }

        private static System.Collections.IEnumerator CancelledStart() { yield break; }
    }
}
