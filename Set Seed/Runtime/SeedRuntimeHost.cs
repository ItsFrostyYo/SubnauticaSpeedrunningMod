using System;
using UnityEngine;
using ItsFrosti.SpeedrunningMod.Seeds.Ruleset8;

namespace ItsFrosti.SpeedrunningMod.Seeds
{
    internal static class ModSeedRuntimeHost
    {
        private static readonly ModSeedRuntimeProfile BetterRngFixedProfile = new ModSeedRuntimeProfile();
        private static bool _installed;
        private static bool _startupHooksInstallAttempted;
        private static bool _fishSchoolHookInstalled;
        private static bool _stalkerToothHookInstalled;
        private static bool _betterRngHookInstalled;

        public static void Install(SNSRModLoader.PluginContext context)
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
            Plugin.Logger.Info("Race and BetterRNG runtime initialized.");
        }

        public static ModSeedRuntimeProfile GetProfile()
        {
            if (ShouldApplyBetterRngRules())
            {
                return BetterRngFixedProfile;
            }

            return null;
        }

        public static bool IsRankedSingleplayerSeedActive()
        {
            return SeedRuntime.Active && ModClientSessionMode.IsRankedSingleplayerPracticeSelected;
        }

        public static bool IsRankedMultiplayerSeedActive()
        {
            return SeedRuntime.Active && ModClientSessionMode.IsRankedMultiplayerSelected;
        }

        public static bool IsSupportedGameplayMode()
        {
            GameMode mode = Utils.GetLegacyGameMode();
            return mode == GameMode.Creative || mode == GameMode.Survival || mode == GameMode.Hardcore;
        }

        public static bool IsCreativeMode()
        {
            return Utils.GetLegacyGameMode() == GameMode.Creative;
        }

        public static bool IsSurvivalMode()
        {
            return Utils.GetLegacyGameMode() == GameMode.Survival;
        }

        public static bool IsHardcoreMode()
        {
            return Utils.GetLegacyGameMode() == GameMode.Hardcore;
        }

        public static bool IsSurvivalLikeMode()
        {
            GameMode mode = Utils.GetLegacyGameMode();
            return mode == GameMode.Survival || mode == GameMode.Hardcore;
        }

        public static void EnsureStartupHooksInstalled()
        {
            if (_startupHooksInstallAttempted)
            {
                return;
            }

            _startupHooksInstallAttempted = true;
            _betterRngHookInstalled = ModBetterRngRuntimeHost.EnsureInstalled();
            bool forceSecondGoldInstalled = ModForceSecondGoldRuntime.EnsureInstalled();
            bool ruleset8Installed = Ruleset8RaceSession.EnsureInstalled();
            _fishSchoolHookInstalled = ModFishSchoolHookRuntime.EnsureInstalled();
            _stalkerToothHookInstalled = ModStalkerToothHookRuntime.EnsureInstalled();
            Plugin.Logger.Info(
                "Initialized SNSRModLoader seed support. " +
                "BetterRngHooksInstalled=" +
                _betterRngHookInstalled +
                ", " +
                "Ruleset8RaceInstalled=" +
                ruleset8Installed +
                ", " +
                "ForceSecondGoldInstalled=" +
                forceSecondGoldInstalled +
                ", FishSchoolHooksInstalled=" +
                _fishSchoolHookInstalled +
                ", StalkerToothHooksInstalled=" +
                _stalkerToothHookInstalled +
                ".");
            if (!_betterRngHookInstalled || !forceSecondGoldInstalled || !ruleset8Installed || !_fishSchoolHookInstalled || !_stalkerToothHookInstalled)
                throw new InvalidOperationException("Required speedrunning hooks could not be installed. See the loader log.");
        }

        public static void UpdateSharedRuleState(string saveSlot, bool inMainMenu, bool continueMode)
        {
            if (SeedRuntime.Active)
            {
                ModForceSecondGoldRuntime.Reset();
                return;
            }

            string normalizedSaveSlot = saveSlot ?? string.Empty;
            if (inMainMenu ||
                !normalizedSaveSlot.StartsWith("slot", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, "Main", StringComparison.OrdinalIgnoreCase) ||
                !ShouldApplyAnyModdedSeedRules())
            {
                ModForceSecondGoldRuntime.Reset();
                return;
            }

            bool eligibleFreshRunState = IsSurvivalLikeMode() && !continueMode;
            ModForceSecondGoldRuntime.UpdateSessionState(normalizedSaveSlot, eligibleFreshRunState);
        }

        public static bool ShouldApplyAnyModdedSeedRules()
        {
            return ShouldApplyBetterRngRules();
        }

        public static bool ShouldApplyBetterRngRules()
        {
            if (!IsSupportedGameplayMode())
            {
                return false;
            }

            if (!ModClientSessionMode.IsBetterRngSingleplayerSelected)
            {
                return false;
            }

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.Equals(sceneName, "XMenu", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

    }
}
