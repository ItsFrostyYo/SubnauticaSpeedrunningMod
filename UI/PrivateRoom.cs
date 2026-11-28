using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ItsFrosti.SpeedrunningMod.Seeds;
using ItsFrosti.SpeedrunningMod.Seeds.Ruleset8;

namespace ItsFrosti.SpeedrunningMod.Ui
{
    // Original Private Room controls and layout, separated from menu navigation.
    internal static partial class MainMenuUI
    {
        private const string ModPrivateRaceRoomGroupName = "ModPrivateRaceRoom";
        private const string PrivateRaceRoomRootName = "ModPrivateRaceRoomRoot";
        private const string PrivateRaceRoomHostTextName = "ModPrivateRaceRoomHost";
        private const string PrivateRaceRoomJoinerTextName = "ModPrivateRaceRoomJoiner";
        private const string PrivateRaceRoomCopyButtonName = "ModPrivateRaceRoomCopyButton";
        private const string PrivateRaceRoomModeRowName = "ModPrivateRaceRoomMode";
        private const string PrivateRaceRoomStartButtonName = "ModPrivateRaceRoomStart";
        private static string _lastPrivateRaceRoomLayoutState = string.Empty;

        private static void KeepPrivateRaceRoomActive()
        {
            if (uGUI_MainMenu.main == null || MainMenuRightSide.main == null)
            {
                return;
            }
            MainMenuRightSide rightSide = MainMenuRightSide.main;

            EnsurePrivateRaceRoomGroup(rightSide);
            GameObject roomGroup = FindGroup(rightSide, ModPrivateRaceRoomGroupName);
            if (roomGroup == null)
            {
                EnsurePrimaryMenuVisible(uGUI_MainMenu.main, true);
                Plugin.Logger.Warning("Private room panel was unavailable. Falling back to the mode select menu instead of leaving the screen blank.");
                OpenRankedModeSelect(uGUI_MainMenu.main, rightSide, FindPrimaryButton("ButtonRanked"));
                return;
            }

            EnsurePrimaryMenuVisible(uGUI_MainMenu.main, false);
            rightSide.OpenGroup(ModPrivateRaceRoomGroupName);
            SyncPrivateRaceRoomGroup(rightSide);
        }

        private static void EnsurePrivateRaceRoomGroup(MainMenuRightSide rightSide)
        {
            GameObject roomGroup = FindGroup(rightSide, ModPrivateRaceRoomGroupName);
            if (roomGroup == null)
            {
                GameObject optionsGroup = FindGroup(rightSide, "Options");
                if (optionsGroup == null)
                {
                    Plugin.Logger.Warning("Ranked UI could not find Options group to clone for Private Race Room.");
                    return;
                }

                roomGroup = UnityEngine.Object.Instantiate(optionsGroup, optionsGroup.transform.parent, false);
                roomGroup.name = ModPrivateRaceRoomGroupName;
                roomGroup.transform.SetSiblingIndex(optionsGroup.transform.GetSiblingIndex() + 1);
                roomGroup.SetActive(false);
                ConvertRoomPanel(roomGroup);
                rightSide.groups.Add(roomGroup);

                CachePrivateRaceChoiceTemplates(roomGroup);
                ConfigurePrivateRaceRoomGroup(roomGroup);
            }
        }

        private static void SyncPrivateRaceRoomGroup(MainMenuRightSide rightSide)
        {
            GameObject roomGroup = FindGroup(rightSide, ModPrivateRaceRoomGroupName);
            if (roomGroup == null)
            {
                return;
            }

            ConfigurePrivateRaceRoomGroup(roomGroup);
        }

        private static void ConfigurePrivateRaceRoomGroup(GameObject roomGroup)
        {
            if (roomGroup == null)
            {
                return;
            }

            roomGroup.transform.SetAsLastSibling();

            uGUI_TabbedControlsPanel panel = roomGroup.GetComponent<uGUI_TabbedControlsPanel>();
            if (panel == null)
            {
                panel = roomGroup.GetComponentInChildren<uGUI_TabbedControlsPanel>(true);
            }

            if (panel == null || panel.tabsContainer == null || panel.panesContainer == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find options panel components in Private Race Room panel.");
                return;
            }

            ConfigurePrivateRaceLeaveButton(roomGroup);
            EnsureSinglePrivateRaceTab(panel);
            SetPanelTitle(roomGroup, ModPrivateRaceRoomRuntimeHost.IsError
                ? "Connection Failed"
                : (ModPrivateRaceRoomRuntimeHost.IsConnecting ? "Connecting" : "Private Room"));
            SetCustomPanelTitleFontSize(roomGroup, 24);

            Transform paneContent = GetPrivateRacePaneContent(panel);
            if (paneContent == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find the Private Race Room pane content root.");
                return;
            }

            GameObject root = EnsurePrivateRaceRoomRoot(paneContent);
            if (root == null)
            {
                Plugin.Logger.Warning("Ranked UI could not create the Private Race Room root.");
                return;
            }

            EnsurePrivateRacePlayerText(roomGroup, root.transform, PrivateRaceRoomHostTextName, 0, "Host: " + ModPrivateRaceRoomRuntimeHost.GetRoomHostDisplayText());
            EnsurePrivateRacePlayerText(roomGroup, root.transform, "ModPrivateRaceRoomCode", 1, "Room Code: " + ModPrivateRaceRoomRuntimeHost.GetRoomCodeText());
            EnsurePrivateRaceCopyButton(roomGroup, root.transform, 1);
            EnsurePrivateRacePlayerText(
                roomGroup,
                root.transform,
                PrivateRaceRoomJoinerTextName,
                2,
                ModPrivateRaceRoomRuntimeHost.IsLocalHost
                    ? ("Player: " + ModPrivateRaceRoomRuntimeHost.GetJoinerDisplayText())
                    : ("Player: " + ModPrivateRaceRoomRuntimeHost.GetLocalPlayerDisplayText()));
            EnsurePrivateRacePlayerText(roomGroup, root.transform, "ModPrivateRaceRoomStatus", 3, ModPrivateRaceRoomRuntimeHost.GetStatusMessage());

            if (!ModPrivateRaceRoomRuntimeHost.IsConnecting &&
                !ModPrivateRaceRoomRuntimeHost.IsError &&
                ModPrivateRaceRoomRuntimeHost.IsLocalHost)
            {
                GameObject modeRow = EnsurePrivateRaceModeSelectorRow(roomGroup, root.transform, PrivateRaceRoomModeRowName, 4);
                ConfigurePrivateRaceModeSelector(modeRow);

                GameObject startRow = EnsurePrivateRaceActionButtonRow(roomGroup, root.transform, PrivateRaceRoomStartButtonName, 5);
                bool canStartRace = ModPrivateRaceRoomRuntimeHost.CanStartRace();
                bool hasJoinedOpponent = ModPrivateRaceRoomRuntimeHost.HasJoinedOpponent;
                string startLabel = ModPrivateRaceRoomRuntimeHost.IsLaunching
                    ? "Starting Race..."
                    : (hasJoinedOpponent ? "Start Race" : "Waiting for Joiner");
                ConfigurePrivateRacePanelButton(
                    startRow,
                    startLabel,
                    !ModPrivateRaceRoomRuntimeHost.IsLaunching && canStartRace,
                    delegate
                    {
                        ModPrivateRaceRoomRuntimeHost.StartLocalRace();
                    });
            }
            else
            {
                HidePrivateRaceOptionalRow(root.transform, PrivateRaceRoomModeRowName);
                HidePrivateRaceOptionalRow(root.transform, PrivateRaceRoomStartButtonName);
            }

            _lastPrivateRaceRoomLayoutState = BuildPrivateRaceRoomLayoutState();
        }

        private static void PromptAndBeginPrivateRaceHost()
        {
            if (!CanConnect()) return;
            ModPrivateRaceRoomRuntimeHost.BeginHosting(RaceAccount.DisplayName);
        }

        private static void PromptAndBeginPrivateRaceJoin()
        {
            if (!CanConnect()) return;
            uGUI.main.userInput.RequestString("Room Code", "Join", string.Empty, 6, delegate(string value)
            {
                string code = ModPrivateRaceRoomRuntimeHost.SanitizeRoomCode(value);
                if (!string.IsNullOrEmpty(code))
                    ModPrivateRaceRoomRuntimeHost.BeginJoining(RaceAccount.DisplayName, code);
            });
        }

        private static void EnsureSinglePrivateRaceTab(uGUI_TabbedControlsPanel panel)
        {
            if (panel == null || panel.tabsContainer == null || panel.panesContainer == null)
            {
                return;
            }

            bool needsReset = panel.tabsContainer.childCount != 1 || panel.panesContainer.childCount != 1;
            if (!needsReset && panel.tabsContainer.childCount > 0)
            {
                Text tabText = panel.tabsContainer.GetChild(0).GetComponentInChildren<Text>(true);
                needsReset = tabText == null || !string.Equals((tabText.text ?? string.Empty).Trim(), "Race", StringComparison.OrdinalIgnoreCase);
            }

            if (needsReset)
            {
                panel.RemoveTabs();
                panel.AddTab("Race");
            }

            if (panel.tabsContainer.childCount > 0)
            {
                for (int i = 0; i < panel.tabsContainer.childCount; i++)
                {
                    Transform tabTransform = panel.tabsContainer.GetChild(i);
                    if (tabTransform == null)
                    {
                        continue;
                    }

                    bool isPrimary = i == 0;
                    tabTransform.gameObject.SetActive(isPrimary);
                    if (!isPrimary)
                    {
                        continue;
                    }

                    RemoveLocalizationComponents(tabTransform.gameObject);
                    SetButtonLabel(tabTransform.gameObject, "Race");
                    Toggle toggle = tabTransform.GetComponentInChildren<Toggle>(true);
                    if (toggle != null)
                    {
                        toggle.isOn = true;
                        toggle.interactable = false;
                    }
                }
            }

            for (int i = 0; i < panel.panesContainer.childCount; i++)
            {
                Transform paneTransform = panel.panesContainer.GetChild(i);
                if (paneTransform != null)
                {
                    paneTransform.gameObject.SetActive(i == 0);
                }
            }
        }

        private static Transform GetPrivateRacePaneContent(uGUI_TabbedControlsPanel panel)
        {
            if (panel == null || panel.panesContainer == null || panel.panesContainer.childCount <= 0)
            {
                return null;
            }

            Transform pane = panel.panesContainer.GetChild(0);
            Transform content = pane != null ? pane.Find("Content") : null;
            return content ?? pane;
        }

        private static void HidePrivateRaceOptionalRow(Transform rootTransform, string objectName)
        {
            if (rootTransform == null || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            Transform childTransform = rootTransform.Find(objectName);
            if (childTransform != null)
            {
                childTransform.gameObject.SetActive(false);
            }
        }

        private static void ConfigurePrivateRaceLeaveButton(GameObject roomGroup)
        {
            Button backButton = FindButtonByTranslationKey(roomGroup, "Back");
            if (backButton == null)
            {
                backButton = FindButtonByText(roomGroup, "Back");
            }

            if (backButton == null)
            {
                return;
            }

            RemoveLocalizationComponents(backButton.gameObject);
            SetButtonLabel(backButton.gameObject, "Leave Room");
            backButton.onClick = new Button.ButtonClickedEvent();
            backButton.onClick.AddListener(new UnityAction(delegate
            {
                if (uGUI.main == null || uGUI.main.confirmation == null)
                {
                    ModPrivateRaceRoomRuntimeHost.LeaveRoom();
                    if (uGUI_MainMenu.main != null)
                    {
                        uGUI_MainMenu.main.OnHome();
                    }

                    return;
                }

                uGUI.main.confirmation.Show("Are you sure you want to leave the private race room?", delegate(bool confirmed)
                {
                    if (!confirmed)
                    {
                        return;
                    }

                    ModPrivateRaceRoomRuntimeHost.LeaveRoom();
                    if (uGUI_MainMenu.main != null)
                    {
                        uGUI_MainMenu.main.OnHome();
                    }
                });
            }));
        }

        private static GameObject EnsurePrivateRaceRoomRoot(Transform contentTransform)
        {
            if (contentTransform == null)
            {
                return null;
            }

            for (int i = contentTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = contentTransform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (!string.Equals(child.name, PrivateRaceRoomRootName, StringComparison.Ordinal))
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }

            Transform existing = contentTransform.Find(PrivateRaceRoomRootName);
            GameObject rootObject = existing != null ? existing.gameObject : null;
            if (rootObject == null)
            {
                rootObject = new GameObject(PrivateRaceRoomRootName, typeof(RectTransform));
                rootObject.transform.SetParent(contentTransform, false);
            }

            RectTransform rectTransform = rootObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(12f, 12f);
            rectTransform.offsetMax = new Vector2(-12f, -12f);
            rectTransform.localScale = Vector3.one;
            return rootObject;
        }

        private static void EnsurePrivateRacePlayerText(GameObject panelGroup, Transform rootTransform, string objectName, int rowIndex, string value)
        {
            Transform existingTransform = rootTransform.Find(objectName);
            Text text = existingTransform != null ? existingTransform.GetComponent<Text>() : null;
            if (text == null)
            {
                Text template = panelGroup.GetComponentInChildren<Text>(true);
                if (template == null)
                {
                    return;
                }

                GameObject textObject = UnityEngine.Object.Instantiate(template.gameObject, rootTransform, false);
                textObject.name = objectName;
                RemoveLocalizationComponents(textObject);
                text = textObject.GetComponent<Text>();
            }

            RectTransform rectTransform = text.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(10f, -(26f + (54f * rowIndex)));
            rectTransform.sizeDelta = string.Equals(objectName, "ModPrivateRaceRoomCode", StringComparison.Ordinal)
                ? new Vector2(-160f, 42f)
                : new Vector2(-20f, 42f);
            rectTransform.localScale = Vector3.one;

            text.text = value;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = Color.white;
            text.raycastTarget = false;
            text.gameObject.SetActive(true);
        }

        private static void EnsurePrivateRaceCopyButton(GameObject panelGroup, Transform rootTransform, int rowIndex)
        {
            if (panelGroup == null || rootTransform == null)
            {
                return;
            }

            Transform existingTransform = rootTransform.Find(PrivateRaceRoomCopyButtonName);
            GameObject buttonObject = existingTransform != null ? existingTransform.gameObject : null;
            if (buttonObject == null)
            {
                Button templateButton = FindButtonByText(panelGroup, "Leave Room");
                if (templateButton == null)
                {
                    templateButton = FindButtonByText(panelGroup, "Back");
                }

                if (templateButton == null)
                {
                    return;
                }

                buttonObject = UnityEngine.Object.Instantiate(templateButton.gameObject, rootTransform, false);
                buttonObject.name = PrivateRaceRoomCopyButtonName;
            }

            RectTransform rectTransform = buttonObject.transform as RectTransform;
            if (rectTransform != null)
            {
                Text roomCodeText = null;
                Transform roomCodeTransform = rootTransform.Find("ModPrivateRaceRoomCode");
                if (roomCodeTransform != null)
                {
                    roomCodeText = roomCodeTransform.GetComponent<Text>();
                }

                float preferredWidth = roomCodeText != null ? roomCodeText.preferredWidth : 220f;
                float buttonLeft = Mathf.Clamp(18f + preferredWidth + 10f, 220f, 460f);
                rectTransform.anchorMin = new Vector2(0f, 1f);
                rectTransform.anchorMax = new Vector2(0f, 1f);
                rectTransform.pivot = new Vector2(0f, 1f);
                rectTransform.anchoredPosition = new Vector2(buttonLeft, -(27f + (54f * rowIndex)));
                rectTransform.sizeDelta = new Vector2(84f, 34f);
                rectTransform.localScale = Vector3.one;
            }

            Button button = buttonObject.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                buttonObject.SetActive(false);
                return;
            }

            string roomCode = ModPrivateRaceRoomRuntimeHost.GetRoomCodeText();
            bool canCopy = !string.IsNullOrEmpty(roomCode) &&
                !string.Equals(roomCode, "Unavailable", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(roomCode, "Generating...", StringComparison.OrdinalIgnoreCase);

            RemoveLocalizationComponents(button.gameObject);
            SetButtonLabel(button.gameObject, "Copy");
            SetButtonLabelFontSize(button.gameObject, 18);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(new UnityAction(delegate
            {
                GUIUtility.systemCopyBuffer = ModPrivateRaceRoomRuntimeHost.GetRoomCodeText();
            }));
            button.interactable = canCopy;
            buttonObject.SetActive(true);
        }

        private static GameObject EnsurePrivateRaceModeSelectorRow(GameObject panelGroup, Transform rootTransform, string objectName, int rowIndex)
        {
            if (panelGroup == null || rootTransform == null)
            {
                return null;
            }

            Transform existingTransform = rootTransform.Find(objectName);
            GameObject rowObject = existingTransform != null ? existingTransform.gameObject : null;
            if (rowObject == null)
            {
                rowObject = new GameObject(objectName, typeof(RectTransform));
                rowObject.transform.SetParent(rootTransform, false);
            }

            RectTransform rowRect = rowObject.transform as RectTransform;
            if (rowRect != null)
            {
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -(18f + (98f * rowIndex)));
                rowRect.sizeDelta = new Vector2(0f, 96f);
                rowRect.localScale = Vector3.one;
            }

            EnsurePrivateRaceModeSelectorChildren(panelGroup, rowObject);
            rowObject.SetActive(true);
            return rowObject;
        }

        private static void EnsurePrivateRaceModeSelectorChildren(GameObject panelGroup, GameObject rowObject)
        {
            EnsurePrivateRaceModeSelectorLabel(panelGroup, rowObject);
            EnsurePrivateRaceModeSelectorValue(panelGroup, rowObject);
            EnsurePrivateRaceModeSelectorArrow(panelGroup, rowObject, "Previous", "<");
            EnsurePrivateRaceModeSelectorArrow(panelGroup, rowObject, "Next", ">");
        }

        private static void EnsurePrivateRaceModeSelectorLabel(GameObject panelGroup, GameObject rowObject)
        {
            Transform existingTransform = rowObject.transform.Find("Label");
            Text labelText = existingTransform != null ? existingTransform.GetComponent<Text>() : null;
            if (labelText == null)
            {
                Text template = panelGroup.GetComponentInChildren<Text>(true);
                if (template == null)
                {
                    return;
                }

                labelText = UnityEngine.Object.Instantiate(template, rowObject.transform, false);
                labelText.name = "Label";
                RemoveLocalizationComponents(labelText.gameObject);
            }

            RectTransform rectTransform = labelText.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(10f, 0f);
            rectTransform.sizeDelta = new Vector2(300f, 48f);
            rectTransform.localScale = Vector3.one;

            labelText.text = "Race Gamemode";
            labelText.fontSize = 28;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.color = Color.white;
            labelText.raycastTarget = false;
            labelText.gameObject.SetActive(true);
        }

        private static void EnsurePrivateRaceModeSelectorValue(GameObject panelGroup, GameObject rowObject)
        {
            Transform existingTransform = rowObject.transform.Find("Value");
            Text valueText = existingTransform != null ? existingTransform.GetComponent<Text>() : null;
            if (valueText == null)
            {
                Text template = panelGroup.GetComponentInChildren<Text>(true);
                if (template == null)
                {
                    return;
                }

                valueText = UnityEngine.Object.Instantiate(template, rowObject.transform, false);
                valueText.name = "Value";
                RemoveLocalizationComponents(valueText.gameObject);
            }

            RectTransform rectTransform = valueText.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(562f, 0f);
            rectTransform.sizeDelta = new Vector2(170f, 48f);
            rectTransform.localScale = Vector3.one;

            valueText.text = ModPrivateRaceRoomRuntimeHost.SelectedMode;
            valueText.fontSize = 28;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            valueText.verticalOverflow = VerticalWrapMode.Truncate;
            valueText.color = Color.white;
            valueText.raycastTarget = false;
            valueText.gameObject.SetActive(true);
        }

        private static void EnsurePrivateRaceModeSelectorArrow(GameObject panelGroup, GameObject rowObject, string objectName, string label)
        {
            Transform existingTransform = rowObject.transform.Find(objectName);
            Button button = existingTransform != null ? existingTransform.GetComponent<Button>() : null;
            if (button == null)
            {
                bool isPrevious = string.Equals(objectName, "Previous", StringComparison.Ordinal);
                Button templateButton = isPrevious ? _privateRaceChoicePreviousArrowTemplate : _privateRaceChoiceNextArrowTemplate;
                if (templateButton == null)
                {
                    templateButton = FindButtonByText(panelGroup, "Leave Room");
                    if (templateButton == null)
                    {
                        templateButton = FindButtonByText(panelGroup, "Back");
                    }
                }

                if (templateButton == null)
                {
                    return;
                }

                GameObject clone = UnityEngine.Object.Instantiate(templateButton.gameObject, rowObject.transform, false);
                clone.name = objectName;
                button = clone.GetComponent<Button>();
            }

            RectTransform rectTransform = button.transform as RectTransform;
            if (rectTransform != null)
            {
                bool isPrevious = string.Equals(objectName, "Previous", StringComparison.Ordinal);
                rectTransform.anchorMin = new Vector2(0f, 0.5f);
                rectTransform.anchorMax = new Vector2(0f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = new Vector2(isPrevious ? 436f : 688f, 0f);
                rectTransform.sizeDelta = new Vector2(64f, 64f);
                rectTransform.localScale = Vector3.one;
            }

            RemoveLocalizationComponents(button.gameObject);
            if (_privateRaceChoicePreviousArrowTemplate == null || _privateRaceChoiceNextArrowTemplate == null)
            {
                SetButtonLabel(button.gameObject, label);
                SetButtonLabelFontSize(button.gameObject, 30);
            }
            button.interactable = true;
            button.gameObject.SetActive(true);
        }

        private static void ConfigurePrivateRaceModeSelector(GameObject rowObject)
        {
            if (rowObject == null)
            {
                return;
            }

            Text valueText = null;
            Transform valueTransform = rowObject.transform.Find("Value");
            if (valueTransform != null)
            {
                valueText = valueTransform.GetComponent<Text>();
            }

            if (valueText != null)
            {
                valueText.text = ModPrivateRaceRoomRuntimeHost.SelectedMode;
            }

            Button previousButton = null;
            Transform previousTransform = rowObject.transform.Find("Previous");
            if (previousTransform != null)
            {
                previousButton = previousTransform.GetComponent<Button>();
            }

            Button nextButton = null;
            Transform nextTransform = rowObject.transform.Find("Next");
            if (nextTransform != null)
            {
                nextButton = nextTransform.GetComponent<Button>();
            }

            ModPrivateRaceChoiceRowMarker marker = rowObject.GetComponent<ModPrivateRaceChoiceRowMarker>();
            if (marker == null)
            {
                marker = rowObject.AddComponent<ModPrivateRaceChoiceRowMarker>();
            }

            if (previousButton != null)
            {
                previousButton.onClick = new Button.ButtonClickedEvent();
                previousButton.onClick.AddListener(new UnityAction(delegate
                {
                    ModPrivateRaceRoomRuntimeHost.OffsetSelectedModeIndex(-1);
                    Plugin.Logger.Info("Private race mode changed to " + ModPrivateRaceRoomRuntimeHost.SelectedMode + ".");
                    SyncPrivateRaceRoomUiImmediately();
                }));
            }

            if (nextButton != null)
            {
                nextButton.onClick = new Button.ButtonClickedEvent();
                nextButton.onClick.AddListener(new UnityAction(delegate
                {
                    ModPrivateRaceRoomRuntimeHost.OffsetSelectedModeIndex(1);
                    Plugin.Logger.Info("Private race mode changed to " + ModPrivateRaceRoomRuntimeHost.SelectedMode + ".");
                    SyncPrivateRaceRoomUiImmediately();
                }));
            }

            if (previousButton != null)
            {
                previousButton.interactable = true;
            }

            if (nextButton != null)
            {
                nextButton.interactable = true;
            }
        }

        private static GameObject EnsurePrivateRaceActionButtonRow(GameObject panelGroup, Transform rootTransform, string objectName, int rowIndex)
        {
            if (panelGroup == null || rootTransform == null)
            {
                return null;
            }

            Transform existingTransform = rootTransform.Find(objectName);
            GameObject rowObject = existingTransform != null ? existingTransform.gameObject : null;
            if (rowObject == null)
            {
                Button templateButton = FindButtonByText(panelGroup, "Leave Room");
                if (templateButton == null)
                {
                    templateButton = FindButtonByText(panelGroup, "Back");
                }

                if (templateButton == null)
                {
                    return null;
                }

                rowObject = UnityEngine.Object.Instantiate(templateButton.gameObject, rootTransform, false);
                rowObject.name = objectName;
            }

            RectTransform rowRect = rowObject.transform as RectTransform;
            if (rowRect != null)
            {
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -(28f + (98f * rowIndex)));
                rowRect.sizeDelta = new Vector2(0f, 86f);
                rowRect.localScale = Vector3.one;
            }

            rowObject.SetActive(true);
            return rowObject;
        }

        private static void ConfigurePrivateRacePanelButton(GameObject rowObject, string label, bool isInteractable, UnityAction onClick)
        {
            if (rowObject == null)
            {
                return;
            }

            LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = rowObject.AddComponent<LayoutElement>();
            }

            layoutElement.minHeight = 72f;
            layoutElement.preferredHeight = 72f;

            Button button = rowObject.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                return;
            }

            RectTransform buttonRect = button.transform as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0f, 0.5f);
                buttonRect.anchorMax = new Vector2(1f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = Vector2.zero;
                buttonRect.sizeDelta = new Vector2(0f, 56f);
                buttonRect.offsetMin = new Vector2(0f, -28f);
                buttonRect.offsetMax = new Vector2(0f, 28f);
            }

            RemoveLocalizationComponents(button.gameObject);
            SetButtonLabel(button.gameObject, label);
            SetButtonLabelFontSize(button.gameObject, 26);
            ModPrivateRaceStartButtonMarker marker = rowObject.GetComponent<ModPrivateRaceStartButtonMarker>();
            if (marker == null)
            {
                marker = rowObject.AddComponent<ModPrivateRaceStartButtonMarker>();
            }

            button.onClick = new Button.ButtonClickedEvent();
            if (onClick != null)
            {
                button.onClick.AddListener(new UnityAction(delegate
                {
                    Plugin.Logger.Info("Private race Start Race button clicked.");
                    onClick();
                }));
            }

            button.interactable = isInteractable;
            ResetMainMenuButtonVisualState(rowObject);
            ApplyQueueRowPresentation(rowObject, new QueueRowDefinition(label, isInteractable, 26));
        }

        private static void SyncPrivateRaceRoomUiImmediately()
        {
            if (MainMenuRightSide.main == null)
            {
                return;
            }

            SyncPrivateRaceRoomGroup(MainMenuRightSide.main);
        }

        private static void CachePrivateRaceChoiceTemplates(GameObject roomGroup)
        {
            if (roomGroup == null)
            {
                return;
            }

            uGUI_Choice templateChoice = roomGroup.GetComponentInChildren<uGUI_Choice>(true);
            if (templateChoice == null)
            {
                return;
            }

            if (templateChoice.previousButton != null)
            {
                _privateRaceChoicePreviousArrowTemplate = templateChoice.previousButton;
            }

            if (templateChoice.nextButton != null)
            {
                _privateRaceChoiceNextArrowTemplate = templateChoice.nextButton;
            }
        }

        private static void UpdatePrivateRaceConnectingModal()
        {
            if (!ModPrivateRaceRoomRuntimeHost.IsConnecting && !ModPrivateRaceRoomRuntimeHost.IsError)
            {
                HidePrivateRaceConnectingModal();
                return;
            }

            GameObject modalRoot = EnsurePrivateRaceConnectingModal();
            if (modalRoot == null)
            {
                return;
            }

            if (_privateRaceConnectingModalTitleText != null)
            {
                _privateRaceConnectingModalTitleText.text = ModPrivateRaceRoomRuntimeHost.IsError ? "Connection Failed" : "Connecting";
            }

            if (_privateRaceConnectingModalBodyText != null)
            {
                _privateRaceConnectingModalBodyText.text = ModPrivateRaceRoomRuntimeHost.GetStatusMessage();
            }

            if (_privateRaceConnectingModalActionButton != null)
            {
                SetButtonLabel(_privateRaceConnectingModalActionButton.gameObject, ModPrivateRaceRoomRuntimeHost.IsError ? "Back" : "Cancel");
                _privateRaceConnectingModalActionButton.onClick = new Button.ButtonClickedEvent();
                _privateRaceConnectingModalActionButton.onClick.AddListener(new UnityAction(delegate
                {
                    ModPrivateRaceRoomRuntimeHost.LeaveRoom();
                    HidePrivateRaceConnectingModal();
                    OpenRankedModeSelect(uGUI_MainMenu.main, MainMenuRightSide.main, FindPrimaryButton("ButtonRanked"));
                }));
            }

            modalRoot.SetActive(true);
            modalRoot.transform.SetAsLastSibling();
        }

        private static void HidePrivateRaceConnectingModal()
        {
            if (_privateRaceConnectingModalRoot != null && _privateRaceConnectingModalRoot.activeSelf)
            {
                _privateRaceConnectingModalRoot.SetActive(false);
            }
        }

        private static GameObject EnsurePrivateRaceConnectingModal()
        {
            if (_privateRaceConnectingModalRoot != null)
            {
                return _privateRaceConnectingModalRoot;
            }

            uGUI_MainMenu menu = uGUI_MainMenu.main;
            if (menu == null)
            {
                return null;
            }

            Canvas parentCanvas = menu.GetComponentInParent<Canvas>();
            Text textTemplate = menu.GetComponentInChildren<Text>(true);
            if (parentCanvas == null || textTemplate == null)
            {
                return null;
            }

            _privateRaceConnectingModalRoot = new GameObject("ModPrivateRaceConnectingModal", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _privateRaceConnectingModalRoot.transform.SetParent(parentCanvas.transform, false);

            RectTransform rootRect = _privateRaceConnectingModalRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Image rootImage = _privateRaceConnectingModalRoot.GetComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0.55f);
            rootImage.raycastTarget = true;

            CanvasGroup rootCanvasGroup = _privateRaceConnectingModalRoot.GetComponent<CanvasGroup>();
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;

            GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(_privateRaceConnectingModalRoot.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 260f);
            panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.16f, 0.57f, 0.91f, 0.97f);
            panelImage.raycastTarget = true;

            _privateRaceConnectingModalTitleText = UnityEngine.Object.Instantiate(textTemplate, panelObject.transform, false);
            _privateRaceConnectingModalTitleText.name = "Title";
            RemoveLocalizationComponents(_privateRaceConnectingModalTitleText.gameObject);
            RectTransform titleRect = _privateRaceConnectingModalTitleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(28f, -80f);
            titleRect.offsetMax = new Vector2(-28f, -20f);
            _privateRaceConnectingModalTitleText.alignment = TextAnchor.MiddleLeft;
            _privateRaceConnectingModalTitleText.fontSize = 32;
            _privateRaceConnectingModalTitleText.color = Color.white;
            _privateRaceConnectingModalTitleText.text = "Connecting";
            _privateRaceConnectingModalTitleText.raycastTarget = false;
            _privateRaceConnectingModalTitleText.gameObject.SetActive(true);

            _privateRaceConnectingModalBodyText = UnityEngine.Object.Instantiate(textTemplate, panelObject.transform, false);
            _privateRaceConnectingModalBodyText.name = "Body";
            RemoveLocalizationComponents(_privateRaceConnectingModalBodyText.gameObject);
            RectTransform bodyRect = _privateRaceConnectingModalBodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0.5f);
            bodyRect.anchorMax = new Vector2(1f, 0.5f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.offsetMin = new Vector2(28f, -24f);
            bodyRect.offsetMax = new Vector2(-28f, 24f);
            _privateRaceConnectingModalBodyText.alignment = TextAnchor.MiddleLeft;
            _privateRaceConnectingModalBodyText.fontSize = 24;
            _privateRaceConnectingModalBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _privateRaceConnectingModalBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            _privateRaceConnectingModalBodyText.color = Color.white;
            _privateRaceConnectingModalBodyText.text = "Connecting to private room...";
            _privateRaceConnectingModalBodyText.raycastTarget = false;
            _privateRaceConnectingModalBodyText.gameObject.SetActive(true);

            GameObject buttonObject = new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panelObject.transform, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.sizeDelta = new Vector2(220f, 56f);
            buttonRect.anchoredPosition = new Vector2(0f, 26f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.11f, 0.44f, 0.86f, 1f);
            buttonImage.raycastTarget = true;

            _privateRaceConnectingModalActionButton = buttonObject.GetComponent<Button>();
            _privateRaceConnectingModalActionButton.targetGraphic = buttonImage;

            Text buttonText = UnityEngine.Object.Instantiate(textTemplate, buttonObject.transform, false);
            buttonText.name = "Text";
            RemoveLocalizationComponents(buttonText.gameObject);
            RectTransform buttonTextRect = buttonText.rectTransform;
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.offsetMin = Vector2.zero;
            buttonTextRect.offsetMax = Vector2.zero;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.fontSize = 26;
            buttonText.color = Color.white;
            buttonText.text = "Cancel";
            buttonText.raycastTarget = false;
            buttonText.gameObject.SetActive(true);

            _privateRaceConnectingModalRoot.SetActive(false);
            return _privateRaceConnectingModalRoot;
        }

        private static string BuildPrivateRaceRoomLayoutState()
        {
            return ModPrivateRaceRoomRuntimeHost.HostDisplayNameOrDefault + "|" +
                   ModPrivateRaceRoomRuntimeHost.GetRoomCodeText() + "|" +
                   ModPrivateRaceRoomRuntimeHost.GetJoinerDisplayText() + "|" +
                   ModPrivateRaceRoomRuntimeHost.GetStatusMessage() + "|" +
                   ModPrivateRaceRoomRuntimeHost.SelectedMode + "|" +
                   ModPrivateRaceRoomRuntimeHost.CanStartRace().ToString() + "|" +
                   ModPrivateRaceRoomRuntimeHost.IsLaunching.ToString() + "|" +
                   ModPrivateRaceRoomRuntimeHost.IsLocalHost.ToString();
        }
    }
}
