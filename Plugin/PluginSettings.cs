using HarmonyLib;

namespace ItsFrosti.SpeedrunningMod
{
    [HarmonyPatch(typeof(uGUI_OptionsPanel), "AddTabs")]
    internal static class PluginSettings
    {
        private static void Postfix(uGUI_OptionsPanel __instance)
        {
            int tab = __instance.AddTab("Race");
            RaceAccount.AddOptions(__instance, tab);
            Plugin.Logger.Info("Race options initialized with Google account controls.");
        }
    }
}
