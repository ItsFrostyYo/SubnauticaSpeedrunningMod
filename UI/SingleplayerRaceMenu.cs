using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ItsFrosti.SpeedrunningMod.Seeds.Ruleset8;

namespace ItsFrosti.SpeedrunningMod.Ui
{
    internal static partial class MainMenuUI
    {
        private static void EnsureRankedPracticeNewGameGroup(MainMenuRightSide rightSide)
        {
            if (FindGroup(rightSide, ModPracticeNewGameGroupName) != null) return;
            GameObject source = FindGroup(rightSide, "NewGame");
            if (source == null) throw new InvalidOperationException("The native New Game panel is unavailable.");
            GameObject group = UnityEngine.Object.Instantiate(source, source.transform.parent, false);
            group.name = ModPracticeNewGameGroupName;
            group.SetActive(false);
            rightSide.groups.Add(group);
            var enabledButtons = new List<Button>();
            var modeButtons = new List<Button>();
            foreach (Button button in group.GetComponentsInChildren<Button>(true))
                if (!string.IsNullOrEmpty(NativeModeMethod(button))) modeButtons.Add(button);
            modeButtons.Sort((a, b) => ((RectTransform)b.transform).anchoredPosition.y.CompareTo(((RectTransform)a.transform).anchoredPosition.y));
            var nativeSlots = new List<Vector2>();
            foreach (Button button in modeButtons) nativeSlots.Add(((RectTransform)button.transform).anchoredPosition);
            int visibleSlot = 0;
            foreach (Button button in modeButtons)
            {
                string method = NativeModeMethod(button);
                if (method == "OnButtonFreedom")
                {
                    button.gameObject.SetActive(false);
                    RectTransform panel = button.transform.parent as RectTransform;
                    if (panel != null)
                    {
                        float removedHeight = ((RectTransform)button.transform).rect.height;
                        Vector2 position = panel.anchoredPosition;
                        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panel.rect.height - removedHeight);
                        position.y += (1f - panel.pivot.y) * removedHeight;
                        panel.anchoredPosition = position;
                    }
                    continue;
                }
                GameMode mode;
                if (method == "OnButtonSurvival") mode = GameMode.Survival;
                else if (method == "OnButtonCreative") mode = GameMode.Creative;
                else if (method == "OnButtonHardcore") mode = GameMode.Hardcore;
                else continue; // Preserve native Back/help controls.
                ((RectTransform)button.transform).anchoredPosition = nativeSlots[visibleSlot++];
                button.onClick = new Button.ButtonClickedEvent();
                bool enabled = mode != GameMode.Hardcore && Ruleset8SeedPool.HasSeeds(mode);
                button.interactable = enabled;
                if (enabled)
                {
                    button.onClick.AddListener(delegate { StartRankedPracticeNewGame(mode); });
                    enabledButtons.Add(button);
                }
                else
                {
                    SetAlpha(button.gameObject, ComingSoonRowAlpha);
                    EventTrigger hover = button.GetComponent<EventTrigger>();
                    if (hover != null) hover.enabled = false;
                    Navigation navigation = button.navigation;
                    navigation.mode = Navigation.Mode.None;
                    button.navigation = navigation;
                }
            }
            if (enabledButtons.Count == 0) throw new InvalidOperationException("Native race mode buttons could not be resolved.");
            for (int i = 0; i < enabledButtons.Count; i++)
            {
                Navigation navigation = enabledButtons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = navigation.selectOnLeft = enabledButtons[(i + enabledButtons.Count - 1) % enabledButtons.Count];
                navigation.selectOnDown = navigation.selectOnRight = enabledButtons[(i + 1) % enabledButtons.Count];
                enabledButtons[i].navigation = navigation;
            }
        }

        private static string NativeModeMethod(Button button)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                string method = button.onClick.GetPersistentMethodName(i);
                if (method == "OnButtonFreedom" || method == "OnButtonSurvival" ||
                    method == "OnButtonCreative" || method == "OnButtonHardcore") return method;
            }
            return string.Empty;
        }

        private static void SetNativePanelTitle(GameObject group, string title)
        {
            foreach (Text text in group.GetComponentsInChildren<Text>(true))
            {
                if (text.GetComponentInParent<Button>() != null) continue;
                // Keep the native heading's font, size, rectangle and alignment.
                RemoveLocalizationComponents(text.gameObject);
                text.text = title;
                text.gameObject.SetActive(true);
                return;
            }
        }
    }
}
