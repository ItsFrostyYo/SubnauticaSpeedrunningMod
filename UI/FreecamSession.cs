using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UWE;

namespace ItsFrosti.SpeedrunningMod.Ui
{
    // Use the native camera/input stack and FreezeTime ownership so opening and
    // closing the pause menu cannot accidentally resume a completed race.
    internal static class FreecamSession
    {
        private const string FreezeOwner = "SpeedrunningMod.ForfeitWinner";
        private static readonly MethodInfo Toggle = AccessTools.Method(typeof(FreecamController), "FreecamToggle");
        private static readonly FieldInfo Speed = AccessTools.Field(typeof(FreecamController), "speed");
        private static readonly FieldInfo LastUpdate = AccessTools.Field(typeof(FreecamController), "t");
        private static readonly FieldInfo OldTimeScale = AccessTools.Field(typeof(FreezeTime), "oldTimeScale");
        private static FreecamController _focusedCamera;
        private static int _menuClosedFrame = -1;
        private static bool _spectating;

        internal static bool TrySpectateForfeitWin()
        {
            if (_spectating) return true;
            if (Player.main == null || LaunchRocket.isLaunching) return false;
            var camera = Object.FindObjectOfType<FreecamController>();
            if (camera == null) return false;
            var menu = IngameMenu.main;
            bool reopenMenu = !camera.GetActive() && menu != null && menu.selected;
            if (reopenMenu) menu.Close();
            if (!camera.GetActive()) Toggle.Invoke(camera, null);
            Speed.SetValue(camera, 64f); // Native Alpha5 speed, in units/second.
            LastUpdate.SetValue(camera, Time.realtimeSinceStartup);
            FreezeTime.Begin(FreezeOwner, true);
            _spectating = true;
            if (reopenMenu) menu.Open();
            return true;
        }

        internal static void ObserveCamera(FreecamController camera, GameObject inputDummy, ref float lastUpdate)
        {
            bool focused = camera.GetActive() && inputDummy != null && inputDummy.activeSelf;
            _focusedCamera = focused ? camera : null;
            if (!focused) lastUpdate = Time.realtimeSinceStartup;
        }

        internal static void LateUpdate()
        {
            // Open after native UI updates, otherwise the same Escape key-down
            // can immediately close the newly opened menu in uGUI_InputGroup.
            var menu = IngameMenu.main;
            if (_focusedCamera == null || !_focusedCamera.GetActive() || menu == null ||
                menu.gameObject.activeInHierarchy || _menuClosedFrame == Time.frameCount) return;
            if (Input.GetKeyDown(KeyCode.Escape) || GameInput.GetButtonDown(GameInput.Button.UIMenu))
                menu.Open();
        }

        internal static void RestoreNormalTime()
        {
            _spectating = false;
            _focusedCamera = null;
            FreezeTime.End(FreezeOwner);
            // A native pause opened at speed 0 otherwise remembers 0 and can
            // restore it later, after the Cleaner/main-menu scene has started.
            OldTimeScale.SetValue(null, 1f);
            Time.timeScale = 1f;
        }

        [HarmonyPatch(typeof(FreecamController), "Update")]
        private static class CameraInputPatch
        {
            private static void Prefix(FreecamController __instance, GameObject ___inputDummy, ref float ___t)
            {
                ObserveCamera(__instance, ___inputDummy, ref ___t);
            }
        }

        [HarmonyPatch(typeof(IngameMenu), "OnDeselect")]
        private static class MenuClosedPatch
        {
            private static void Prefix() { _menuClosedFrame = Time.frameCount; }
        }

        [HarmonyPatch]
        private static class ExitSavePatch
        {
            private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(IngameMenu), "QuitGame");
                yield return AccessTools.Method(typeof(IngameMenu), "QuitGameAsync");
                yield return AccessTools.Method(typeof(SceneCleaner), "Open");
            }
            private static void Prefix() { RestoreNormalTime(); }
        }
    }
}
