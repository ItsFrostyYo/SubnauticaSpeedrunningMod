using SNSRModLoader;
using System.Reflection;
using System.Runtime.InteropServices;
using System;
using HarmonyLib;
using UnityEngine;
using ItsFrosti.SpeedrunningMod.Seeds;
using ItsFrosti.SpeedrunningMod.Seeds.Ruleset8;
using ItsFrosti.SpeedrunningMod.RunTracking;
using ItsFrosti.SpeedrunningMod.Ui;
using ModPlugin = ItsFrosti.SpeedrunningMod.Plugin;

[assembly: AssemblyTitle(ModPlugin.PluginName)]
[assembly: AssemblyProduct(ModPlugin.PluginName)]
[assembly: AssemblyVersion(ModPlugin.PluginVersion)]
[assembly: AssemblyFileVersion(ModPlugin.PluginVersion)]
[assembly: AssemblyInformationalVersion(ModPlugin.PluginVersion)]
[assembly: AssemblyDescription("BetterRNG and Race Mod for Subnautica using SNSRModLoader | Specifically built for the September 2018 Speedrun Version.")]
[assembly: AssemblyCopyright("Copyright © 2026 ItsFrosti")]
[assembly: ComVisible(false)]

namespace ItsFrosti.SpeedrunningMod
{
    [SNSRModLoader(PluginId, PluginName, PluginVersion,
        AutomaticUpdate = Plugin.AutomaticUpdateEnabled,
        UpdateRepository = Plugin.UpdateRepository,
        UpdateManifestUrl = Plugin.UpdateManifestUrl)]
    [TargetVersion(GameVersion.September2018)]
    public sealed class Plugin : SNSRModLoader.Plugin
    {
        public const string PluginId = "ItsFrosti.SpeedrunningMod";
        public const string PluginName = "Race & BetterRNG";
        public const string PluginVersion = "1.0.0";
        public const bool AutomaticUpdateEnabled = true;
        public const string UpdateRepository = "ItsFrostyYo/SubnauticaSpeedrunningMod";
        public const string UpdateManifestUrl = "https://raw.githubusercontent.com/" + UpdateRepository + "/main/dist/mod-update.xml";
        internal static PluginLogger Logger { get; private set; }
        internal static Harmony Harmony { get; private set; }
        internal static ModServerApiClient Server { get; private set; }
        private GameObject host;
        private static Plugin activePlugin;
        internal static bool IsShuttingDown { get; private set; }
        internal static void Shutdown() { if (activePlugin != null) activePlugin.Unload(); }

        public override void Load(PluginContext context)
        {
            Logger = context.Logger;
            activePlugin = this;
            IsShuttingDown = false;
            NativeHttp.Initialize();
            Harmony = context.Harmony;
            PluginSettings.Initialize(context.Settings);
            Server = new ModServerApiClient(PluginSettings.ServerUrl);
            host = new GameObject(PluginId);
            UnityEngine.Object.DontDestroyOnLoad(host);
            // SceneCleaner removes even DontDestroyOnLoad roots on return to menu.
            host.AddComponent<SceneCleanerPreserve>();
            host.AddComponent<PluginRuntime>();
            ModSeedRuntimeHost.Install(context);
            SeedPatches.Install();
            ModSeedRuntimeHost.EnsureStartupHooksInstalled();
            ModRankedMatchmakingRuntimeHost.Install(context);
            ModPrivateRaceRoomRuntimeHost.Install(context);
            SingleplayerRunRuntimeHost.Install(context);
            Harmony.PatchAll(typeof(Plugin).Assembly);
            context.Logger.Info("Subnautica Speedrunning Mod Loaded");
        }

        public override void Unload()
        {
            if (IsShuttingDown) return;
            IsShuttingDown = true;
            // Cancel transport before resetting state. Workers are background threads;
            // quitting never waits for HTTP responses or attempts another request.
            Cleanup(NativeHttp.Shutdown);
            Cleanup(ModRankedMatchmakingRuntimeHost.Shutdown);
            Cleanup(ModPrivateRaceRoomRuntimeHost.Shutdown);
            Cleanup(ModPrivateRaceCountdownRuntimeHost.Shutdown);
            Cleanup(SingleplayerRunRuntimeHost.Unload);
            Cleanup(Ruleset8RaceSession.Reset);
            Cleanup(ModClientSessionMode.ResetForMainMenu);
            Cleanup(ModOverlayRuntime.Unload);
            Cleanup(MainMenuUI.Unload);
            if (host != null) Cleanup(delegate { UnityEngine.Object.DestroyImmediate(host); });
            host = null;
            activePlugin = null;
        }

        private static void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception ex) { Logger.Warning("Plugin shutdown cleanup failed: " + ex.Message); }
        }

        internal static void LogError(string message, Exception error = null)
        {
            Logger.Error(error == null ? message : message + " " + error);
        }

        internal static void StartGame(GameMode mode)
        {
            var menu = uGUI_MainMenu.main;
            if (menu == null) throw new InvalidOperationException("The main menu is unavailable.");
            menu.OnHome();
            menu.ShowPrimaryOptions(true);
            switch (mode)
            {
                case GameMode.Creative: menu.OnButtonCreative(); break;
                case GameMode.Hardcore: menu.OnButtonHardcore(); break;
                case GameMode.Survival: menu.OnButtonSurvival(); break;
                default: throw new ArgumentOutOfRangeException("mode");
            }
        }
    }

    internal sealed class PluginRuntime : MonoBehaviour
    {
        private void OnApplicationQuit() { Plugin.Shutdown(); }

        private void Update()
        {
            if (Plugin.IsShuttingDown) return;
            NativeHttp.Update();
            ModRankedMatchmakingRuntimeHost.Update();
            ModPrivateRaceRoomRuntimeHost.Update();
            SingleplayerRunRuntimeHost.Update();
            MainMenuUI.Update();
        }

    }
}
