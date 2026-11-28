using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ItsFrosti.SpeedrunningMod.RunTracking
{
    // Inject at the native call sites so even small, inlined game methods are covered.
    internal static class RunLifecycleHooks
    {
        internal static IEnumerable<CodeInstruction> AfterCall(IEnumerable<CodeInstruction> instructions,
            MethodInfo nativeCall, string callbackName)
        {
            int matches = 0;
            MethodInfo callback = AccessTools.Method(typeof(SingleplayerRunRuntimeHost), callbackName);
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (!instruction.Calls(nativeCall)) continue;
                matches++;
                yield return new CodeInstruction(OpCodes.Call, callback);
            }
            if (matches != 1)
                throw new InvalidOperationException("Expected one native timer boundary: " + nativeCall.Name + ", found " + matches + ".");
        }
    }

    [HarmonyPatch(typeof(uGUI_SceneIntro), "ResumeGameTime")]
    internal static class IntroRunStartHook
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return RunLifecycleHooks.AfterCall(instructions, AccessTools.Method(typeof(DayNightCycle), "Resume"),
                "OnIntroGameTimeResumed");
        }
    }

    [HarmonyPatch(typeof(LaunchRocket), "OnHandClick")]
    internal static class RocketRunFinishHook
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return RunLifecycleHooks.AfterCall(instructions, AccessTools.Method(typeof(LaunchRocket), "SetLaunchStarted"),
                "OnRocketLaunchStarted");
        }
    }

    [HarmonyPatch]
    internal static class CreativePauseMenuInputHook
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(IngameMenu), "Open");
            yield return AccessTools.Method(typeof(IngameMenu), "OnDeselect");
        }
        private static void Prefix() { SingleplayerRunRuntimeHost.SuppressPauseMenuInput(); }
    }
}
