using System;
using System.Collections;
using HarmonyLib;
using ItsFrosti.SpeedrunningMod.Seeds;
using UnityEngine;
using UnityEngine.UI;

namespace ItsFrosti.SpeedrunningMod.Ui
{
    // Preserve the original native pause-menu labels, confirmation subscreen,
    // and race-only saving rules. No packaged-practice-save behavior is included.
    [HarmonyPatch(typeof(IngameMenu))]
    internal static class RacePauseMenu
    {
        private const string RankedSaveBlockedMessage = "Saving is disabled for race runs.";
        private const string SaveDisabledLabel = "Save Disabled";
        private const string ForfeitLabel = "Forfeit";
        private const string ForfeitConfirmationLabel = "Forfeit this race?";
        private static string ReturnConfirmation { get { return ModClientSessionMode.IsRandomQueueRace ? "Return to Main Menu?" : "Return to the private race room?"; } }
        private static float _lastSaveBlockedMessageAt = -1000f;

        [HarmonyPostfix, HarmonyPatch("Update")]
        private static void UpdatePostfix(IngameMenu __instance)
        {
            ApplyReadOnlyMenuState(__instance);
        }

        [HarmonyPostfix, HarmonyPatch("OnSelect")]
        private static void OnSelectPostfix(IngameMenu __instance)
        {
            ApplyReadOnlyMenuState(__instance);
        }

        [HarmonyPrefix, HarmonyPatch("SaveGame")]
        private static bool SaveGamePrefix()
        {
            if (!IsRankedSessionActive())
            {
                return true;
            }

            ShowSaveBlockedMessage();
            return false;
        }

        [HarmonyPrefix, HarmonyPatch("SaveGameAsync")]
        private static bool SaveGameAsyncPrefix(ref IEnumerator __result)
        {
            if (!IsRankedSessionActive())
            {
                return true;
            }

            __result = EmptyCoroutine();
            return false;
        }

        [HarmonyPrefix, HarmonyPatch("QuitSubscreen")]
        private static bool QuitSubscreenPrefix(IngameMenu __instance)
        {
            if (!IsRankedSessionActive() || __instance == null)
            {
                return true;
            }

            if (ShouldUseReturnToRoomMenuState())
            {
                __instance.ChangeSubscreen("QuitConfirmation");
                ApplyReturnToRoomMenuState(__instance);
                return false;
            }

            if (ShouldUseForfeitMenuState())
            {
                __instance.ChangeSubscreen("QuitConfirmation");
                ApplyForfeitMenuState(__instance);
                return false;
            }

            return true;
        }

        private static IEnumerator EmptyCoroutine()
        {
            yield break;
        }

        private static void ApplyReadOnlyMenuState(IngameMenu menu)
        {
            if (menu == null || !IsRankedSessionActive())
            {
                return;
            }

            try
            {
                if (menu.saveButton != null)
                {
                    menu.saveButton.interactable = false;
                    menu.saveButton.gameObject.SetActive(!GameModeUtils.IsPermadeath());
                    Text label = menu.saveButton.GetComponentInChildren<Text>(includeInactive: true);
                    if (label != null)
                    {
                        label.text = SaveDisabledLabel;
                    }
                }

                if (menu.quitToMainMenuButton != null)
                {
                    menu.quitToMainMenuButton.interactable = true;
                }

                if (IsRankedSessionActive())
                {
                    if (ShouldUseReturnToRoomMenuState())
                    {
                        ApplyReturnToRoomMenuState(menu);
                    }
                    else if (ShouldUseForfeitMenuState())
                    {
                        ApplyForfeitMenuState(menu);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.Warning("Could not update the native race pause menu: " + ex.Message);
            }
        }

        private static void ApplyForfeitMenuState(IngameMenu menu)
        {
            if (menu == null)
            {
                return;
            }

            if (menu.quitToMainMenuText != null)
            {
                menu.quitToMainMenuText.text = ForfeitLabel;
            }

            if (menu.quitLastSaveText != null)
            {
                menu.quitLastSaveText.text = ForfeitConfirmationLabel;
            }

            ApplyConfirmationText(menu.transform.Find("QuitConfirmation"), ForfeitConfirmationLabel);
            ApplyConfirmationText(menu.transform.Find("QuitConfirmationWithSaveWarning"), ForfeitConfirmationLabel);
        }

        private static void ApplyReturnToRoomMenuState(IngameMenu menu)
        {
            if (menu == null)
            {
                return;
            }

            if (menu.quitToMainMenuText != null)
            {
                menu.quitToMainMenuText.text = ModClientSessionMode.IsRandomQueueRace ? "Return to Main Menu" : "Return to Private Race Room";
            }

            if (menu.quitLastSaveText != null)
            {
                menu.quitLastSaveText.text = ReturnConfirmation;
            }

            ApplyConfirmationText(menu.transform.Find("QuitConfirmation"), ReturnConfirmation);
            ApplyConfirmationText(menu.transform.Find("QuitConfirmationWithSaveWarning"), ReturnConfirmation);
        }

        private static void ApplyConfirmationText(Transform screenTransform, string replacementText)
        {
            if (screenTransform == null)
            {
                return;
            }

            Text[] texts = screenTransform.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                string current = text.text ?? string.Empty;
                if (string.Equals(current, "Yes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(current, "No", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (current.IndexOf("quit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.IndexOf("sure", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.IndexOf("main menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.IndexOf("forfeit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.IndexOf("private race room", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    text.text = replacementText;
                    text.resizeTextForBestFit = false;
                    text.fontSize = Math.Min(text.fontSize > 0 ? text.fontSize : 28, 24);
                }
            }
        }

        private static bool IsRankedSessionActive()
        {
            return ModSeedRuntimeHost.IsRankedSingleplayerSeedActive() ||
                   ModSeedRuntimeHost.IsRankedMultiplayerSeedActive();
        }

        private static bool ShouldUseForfeitMenuState()
        {
            return ModSeedRuntimeHost.IsRankedMultiplayerSeedActive() &&
                   ModPrivateRaceCountdownRuntimeHost.ShouldUseForfeitMenu;
        }

        private static bool ShouldUseReturnToRoomMenuState()
        {
            return ModSeedRuntimeHost.IsRankedMultiplayerSeedActive() &&
                   ModPrivateRaceCountdownRuntimeHost.ShouldUseReturnToRoomMenu;
        }

        private static void ShowSaveBlockedMessage()
        {
            if (Time.unscaledTime - _lastSaveBlockedMessageAt < 1f)
            {
                return;
            }

            _lastSaveBlockedMessageAt = Time.unscaledTime;
            if (IsRankedSessionActive())
            {
                ErrorMessage.AddMessage(RankedSaveBlockedMessage);
                Plugin.Logger.Info("Blocked manual save while race session was active.");
                return;
            }

        }
    }
}
