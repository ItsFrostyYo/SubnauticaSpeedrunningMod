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
    // Original menu geometry and native control styling, with practice saves,
    // custom language files, launcher updates and duplicate watermarks excluded.
    [HarmonyPatch]
    internal static partial class MainMenuUI
    {
        private const string ModModeSelectGroupName = "ModModeSelect";
        private const string ModQueueGroupName = "ModQueue";
        private const string ModMatchmakingGroupName = "ModMatchmaking";
        private const string ModPracticeNewGameGroupName = "ModPracticeNewGame";
        private const string BetterRngSavedGamesButtonLabel = "Start a new BetterRNG game";
        private const string RaceButtonLabel = "Race";
        private const string QueueRandomRaceComingSoonLabel = "Queue Random Race (Coming Soon)";
        private const string HostRaceLabel = "Host Race";
        private const string JoinRaceLabel = "Join Race";
        private const string SingleplayerRacePracticeLabel = "Start a new Singleplayer Race";
        private const string MatchmakingPlaceholderObjectName = "ModMatchmakingPlaceholder";
        private const string UpdatePanelTitleText = "Update " + Plugin.PluginVersion;
        private const int PanelTitleFontSize = 17;
        private const int DisabledQueueButtonFontSize = 19;
        private const float ComingSoonRowAlpha = 0.42f;
        private static float _nextMenuPatchAttemptAt;
        private static GameObject _privateRaceConnectingModalRoot;
        private static Text _privateRaceConnectingModalTitleText;
        private static Text _privateRaceConnectingModalBodyText;
        private static Button _privateRaceConnectingModalActionButton;
        private static Button _privateRaceChoicePreviousArrowTemplate;
        private static Button _privateRaceChoiceNextArrowTemplate;
        private static uGUI_MainMenu currentMenu;
        private static Button installedPlayButton, installedNewGameButton;
        private static readonly List<GameObject> ownedGroups = new List<GameObject>();

        [HarmonyPostfix, HarmonyPatch(typeof(uGUI_MainMenu), "Start")]
        private static void OnMenuStart(uGUI_MainMenu __instance)
        {
            Build(__instance);
        }

        private static void Build(uGUI_MainMenu menu)
        {
            Unload();
            currentMenu = menu;
            var right = menu.rightSide;
            var existing = new HashSet<GameObject>(right.groups);
            PatchPrimaryOptions(menu, right);
            EnsureRankedModeSelectGroup(right);
            EnsureRankedQueueGroup(right);
            EnsureRankedMatchmakingGroup(right);
            EnsureRankedPracticeNewGameGroup(right);
            EnsurePrivateRaceRoomGroup(right);
            EnsureLeaderboardHomeGroup(right);
            RestoreVanillaSingleplayerPanelPresentation(right);
            EnsureBetterRngSavedGamesButton(right);
            foreach (var group in right.groups) if (!existing.Contains(group)) ownedGroups.Add(group);
            var race = FindPrimaryButton("ButtonRanked");
            if (race != null) ownedGroups.Add(race.gameObject);
            var saved = FindGroup(right, "SavedGames");
            var content = saved.transform.Find("Scroll View/Viewport/SavedGameAreaContent");
            var better = FindBetterRngSavedGamesRow(content);
            if (better != null) ownedGroups.Add(better);
            var normal = content.Find("NewGame");
            installedNewGameButton = normal.GetComponentInChildren<Button>(true);
            installedNewGameButton.onClick.AddListener(SelectVanilla);
            _nextMenuPatchAttemptAt = 0f;
            Plugin.Logger.Info("Installed the original Race menu and Private Room presentation.");
        }

        internal static void Update()
        {
            if (uGUI_MainMenu.main == null)
            {
                HidePrivateRaceConnectingModal();
                return;
            }
            if (currentMenu != uGUI_MainMenu.main) Build(uGUI_MainMenu.main);
            if (currentMenu.rightSide.homeGroup != null) currentMenu.rightSide.homeGroup.SetActive(false);
            if (ModPrivateRaceRoomRuntimeHost.IsConnecting || ModPrivateRaceRoomRuntimeHost.IsError)
            {
                EnsurePrimaryMenuVisible(currentMenu, false);
                UpdatePrivateRaceConnectingModal();
                return;
            }
            HidePrivateRaceConnectingModal();
            if (ModPrivateRaceRoomRuntimeHost.IsRoomActive)
            {
                if (Time.unscaledTime >= _nextMenuPatchAttemptAt)
                {
                    KeepPrivateRaceRoomActive();
                    _nextMenuPatchAttemptAt = Time.unscaledTime + 0.2f;
                }
                return;
            }
            var options = FindGroup(currentMenu.rightSide, "Options");
            if (options != null && options.activeInHierarchy) return;
            EnsurePrimaryMenuVisible(currentMenu, true);
            EnsureLeaderboardHomeVisible(currentMenu.rightSide);
        }

        private static bool CanConnect()
        {
            if (!PluginSettings.EnableNetworking)
            {
                ErrorMessage.AddMessage("Enable the server connection in Options > Race.");
                return false;
            }
            if (ModPrivateRaceRoomRuntimeHost.IsRoomActive || ModRankedMatchmakingRuntimeHost.IsBusy) return false;
            return true;
        }

        internal static void SelectVanilla()
        {
            if (ModPrivateRaceRoomRuntimeHost.IsRoomActive) ModPrivateRaceRoomRuntimeHost.LeaveRoom();
            if (ModRankedMatchmakingRuntimeHost.IsBusy) ModRankedMatchmakingRuntimeHost.CancelQueue();
            Ruleset8RaceSession.Reset();
            ModClientSessionMode.SelectVanilla();
        }

        // A room uses the native tabbed-controls shell, without the options
        // component that would otherwise rebuild General/Graphics/settings tabs.
        private static void ConvertRoomPanel(GameObject root)
        {
            var options = root.GetComponentInChildren<uGUI_OptionsPanel>(true);
            if (options == null) return;
            options.enabled = false;
            var panel = options.gameObject.AddComponent<uGUI_TabbedControlsPanel>();
            foreach (var field in typeof(uGUI_TabbedControlsPanel).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                field.SetValue(panel, field.GetValue(options));
            UnityEngine.Object.DestroyImmediate(options);
            while (panel.tabsContainer.childCount != 0) UnityEngine.Object.DestroyImmediate(panel.tabsContainer.GetChild(0).gameObject);
            while (panel.panesContainer.childCount != 0) UnityEngine.Object.DestroyImmediate(panel.panesContainer.GetChild(0).gameObject);
        }

        internal static void Unload()
        {
            if (installedPlayButton != null) installedPlayButton.onClick.RemoveListener(SelectVanilla);
            if (installedNewGameButton != null) installedNewGameButton.onClick.RemoveListener(SelectVanilla);
            if (currentMenu != null)
            {
                EnsurePrimaryMenuVisible(currentMenu, true);
                if (currentMenu.rightSide.homeGroup != null) currentMenu.rightSide.homeGroup.SetActive(true);
            }
            foreach (var root in ownedGroups)
            {
                if (root == null) continue;
                if (currentMenu != null) currentMenu.rightSide.groups.Remove(root);
                UnityEngine.Object.DestroyImmediate(root);
            }
            ownedGroups.Clear();
            if (_privateRaceConnectingModalRoot != null) UnityEngine.Object.DestroyImmediate(_privateRaceConnectingModalRoot);
            _privateRaceConnectingModalRoot = null;
            currentMenu = null;
        }

        private static void EnsurePrimaryMenuVisible(uGUI_MainMenu menu, bool isVisible)
        {
            if (menu == null || menu.primaryOptions == null)
            {
                return;
            }

            if (isVisible)
            {
                menu.ShowPrimaryOptions(true);
            }

            GraphicRaycaster raycaster = menu.primaryOptions.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = isVisible;
            }

            CanvasGroup canvasGroup = menu.primaryOptions.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = menu.primaryOptions.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;
            canvasGroup.alpha = isVisible ? 1f : 0f;
        }

        private static void PatchPrimaryOptions(uGUI_MainMenu menu, MainMenuRightSide rightSide)
        {
            if (menu.primaryOptions == null)
            {
                return;
            }

            Transform playTransform = FindDescendantByName(menu.primaryOptions.transform, "ButtonPlay");
            Button playButton = playTransform != null ? playTransform.GetComponent<Button>() : null;
            if (playButton == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find ButtonPlay.");
                return;
            }

            Transform menuButtons = playButton.transform.parent;
            playButton.onClick.AddListener(SelectVanilla);
            installedPlayButton = playButton;

            Transform modTransform = menuButtons.Find("ButtonRanked");
            if (modTransform == null)
            {
                GameObject modObject = UnityEngine.Object.Instantiate(playButton.gameObject, menuButtons, false);
                modObject.name = "ButtonRanked";
                modObject.transform.SetSiblingIndex(playButton.transform.GetSiblingIndex());
                RemoveLocalizationComponents(modObject);
                SetButtonLabel(modObject, RaceButtonLabel);

                Button modButton = modObject.GetComponent<Button>();
                if (modButton != null)
                {
                    modButton.onClick = new Button.ButtonClickedEvent();
                    modButton.onClick.AddListener(new UnityAction(delegate
                    {
                        OpenRankedModeSelect(menu, rightSide, modButton);
                    }));
                }

                ResetMainMenuButtonVisualState(modObject);
                Plugin.Logger.Info("Inserted Race button above Play.");
            }
            else
            {
                SetButtonLabel(modTransform.gameObject, RaceButtonLabel);
            }
        }

        private static void EnsureRankedModeSelectGroup(MainMenuRightSide rightSide)
        {
            GameObject modeSelectGroup = FindGroup(rightSide, ModModeSelectGroupName);
            if (modeSelectGroup == null)
            {
                GameObject savedGamesGroup = FindGroup(rightSide, "SavedGames");
                if (savedGamesGroup == null)
                {
                    Plugin.Logger.Warning("Ranked UI could not find SavedGames group to clone for Choose Mode.");
                    return;
                }

                modeSelectGroup = UnityEngine.Object.Instantiate(savedGamesGroup, savedGamesGroup.transform.parent, false);
                modeSelectGroup.name = ModModeSelectGroupName;
                modeSelectGroup.transform.SetSiblingIndex(savedGamesGroup.transform.GetSiblingIndex() + 1);
                modeSelectGroup.SetActive(false);
                rightSide.groups.Add(modeSelectGroup);
            }

            DisableSavedGameBehaviors(modeSelectGroup);
            ConfigureRankedModeSelectGroup(modeSelectGroup);
        }

        private static void EnsureRankedQueueGroup(MainMenuRightSide rightSide)
        {
            GameObject modGroup = FindGroup(rightSide, ModQueueGroupName);
            if (modGroup == null)
            {
                GameObject savedGamesGroup = FindGroup(rightSide, "SavedGames");
                if (savedGamesGroup == null)
                {
                    Plugin.Logger.Warning("Ranked UI could not find SavedGames group to clone.");
                    return;
                }

                modGroup = UnityEngine.Object.Instantiate(savedGamesGroup, savedGamesGroup.transform.parent, false);
                modGroup.name = ModQueueGroupName;
                modGroup.transform.SetSiblingIndex(savedGamesGroup.transform.GetSiblingIndex() + 1);
                modGroup.SetActive(false);
                rightSide.groups.Add(modGroup);
            }

            DisableSavedGameBehaviors(modGroup);
            ConfigureRankedQueueGroup(modGroup);
        }

        private static void EnsureRankedMatchmakingGroup(MainMenuRightSide rightSide)
        {
            GameObject matchmakingGroup = FindGroup(rightSide, ModMatchmakingGroupName);
            if (matchmakingGroup == null)
            {
                GameObject savedGamesGroup = FindGroup(rightSide, "SavedGames");
                if (savedGamesGroup == null)
                {
                    Plugin.Logger.Warning("Ranked UI could not find SavedGames group to clone for Matchmaking.");
                    return;
                }

                matchmakingGroup = UnityEngine.Object.Instantiate(savedGamesGroup, savedGamesGroup.transform.parent, false);
                matchmakingGroup.name = ModMatchmakingGroupName;
                matchmakingGroup.transform.SetSiblingIndex(savedGamesGroup.transform.GetSiblingIndex() + 1);
                matchmakingGroup.SetActive(false);
                rightSide.groups.Add(matchmakingGroup);
            }

            DisableSavedGameBehaviors(matchmakingGroup);
            ConfigureRankedMatchmakingGroup(matchmakingGroup);
        }



        private static void ConfigureRankedModeSelectGroup(GameObject modeSelectGroup)
        {
            Transform content = modeSelectGroup.transform.Find("Scroll View/Viewport/SavedGameAreaContent");
            if (content == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find SavedGameAreaContent in Choose Mode panel.");
                return;
            }

            Transform template = content.Find("NewGame");
            if (template == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find NewGame template row in Choose Mode panel.");
                return;
            }

            int nativeFontSize = template.GetComponentInChildren<Text>(true).fontSize;
            DestroySiblingRows(content, template);

            template.gameObject.SetActive(true);
            template.name = "NewGame";
            template.SetSiblingIndex(0);
            ConfigureActionRow(
                template.gameObject,
                QueueRandomRaceComingSoonLabel,
                false,
                nativeFontSize,
                null);

            GameObject hostRow = UnityEngine.Object.Instantiate(template.gameObject, content, false);
            hostRow.name = "NewGame";
            hostRow.transform.SetSiblingIndex(1);
            ConfigureActionRow(
                hostRow,
                HostRaceLabel,
                true,
                nativeFontSize,
                delegate
                {
                    PromptAndBeginPrivateRaceHost();
                });

            GameObject joinRow = UnityEngine.Object.Instantiate(template.gameObject, content, false);
            joinRow.name = "NewGame";
            joinRow.transform.SetSiblingIndex(2);
            ConfigureActionRow(
                joinRow,
                JoinRaceLabel,
                true,
                nativeFontSize,
                delegate
                {
                    PromptAndBeginPrivateRaceJoin();
                });

            GameObject practiceRow = UnityEngine.Object.Instantiate(template.gameObject, content, false);
            practiceRow.name = "NewGame";
            practiceRow.transform.SetSiblingIndex(3);
            ConfigureActionRow(
                practiceRow,
                SingleplayerRacePracticeLabel,
                true,
                nativeFontSize,
                delegate
                {
                    ModClientSessionMode.SelectRankedSingleplayerPractice();
                    OpenRankedPracticeNewGame(uGUI_MainMenu.main, MainMenuRightSide.main, FindPrimaryButton("ButtonRanked"));
                });

            SetNativePanelTitle(modeSelectGroup, "Choose Mode");
        }

        private static void ConfigureRankedQueueGroup(GameObject modGroup)
        {
            Transform content = modGroup.transform.Find("Scroll View/Viewport/SavedGameAreaContent");
            if (content == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find SavedGameAreaContent in ModQueue.");
                return;
            }

            Transform template = content.Find("NewGame");
            if (template == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find NewGame template row in ModQueue.");
                return;
            }

            DestroySiblingRows(content, template);

            QueueRowDefinition[] rows = new QueueRowDefinition[]
            {
                new QueueRowDefinition("Queue Survival\nComing in Future Update", isInteractable: false, DisabledQueueButtonFontSize),
                new QueueRowDefinition("Queue Creative\nComing in Future Update", isInteractable: false, DisabledQueueButtonFontSize),
                new QueueRowDefinition("Hardcore\nComing in Future Update", isInteractable: false, DisabledQueueButtonFontSize)
            };

            template.gameObject.SetActive(true);
            template.name = "NewGame";
            template.SetSiblingIndex(0);
            ConfigureQueueRow(template.gameObject, rows[0]);

            for (int i = 1; i < rows.Length; i++)
            {
                GameObject row = UnityEngine.Object.Instantiate(template.gameObject, content, false);
                row.name = "NewGame";
                row.transform.SetSiblingIndex(i);
                ConfigureQueueRow(row, rows[i]);
            }

            SetPanelTitle(modGroup, "Queue Random Race");
            Plugin.Logger.Info("Configured ModQueue group with " + rows.Length + " queue buttons.");
        }

        private static void ConfigureRankedMatchmakingGroup(GameObject matchmakingGroup)
        {
            if (matchmakingGroup == null)
            {
                return;
            }

            Transform content = matchmakingGroup.transform.Find("Scroll View/Viewport/SavedGameAreaContent");
            if (content == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find SavedGameAreaContent in Matchmaking panel.");
                return;
            }

            Transform template = content.Find("NewGame");
            if (template == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find NewGame template row in Matchmaking panel.");
                return;
            }

            DestroySiblingRows(content, template);

            template.gameObject.SetActive(true);
            template.name = "NewGame";
            template.SetSiblingIndex(0);
            ConfigureActionRow(
                template.gameObject,
                "Cancel Matchmaking",
                true,
                26,
                delegate
                {
                    ModRankedMatchmakingRuntimeHost.CancelQueue();
                    OpenRankedQueueGroup(uGUI_MainMenu.main, MainMenuRightSide.main, FindPrimaryButton("ButtonRanked"));
                });

            EnsurePanelPlaceholder(
                matchmakingGroup,
                content,
                MatchmakingPlaceholderObjectName,
                ModRankedMatchmakingRuntimeHost.GetPanelMessage());
            SetPanelTitle(matchmakingGroup, ModRankedMatchmakingRuntimeHost.GetPanelTitle());
        }



        private static void ConfigureQueueRow(GameObject row, QueueRowDefinition definition)
        {
            if (row == null)
            {
                return;
            }

            Button button = row.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find queue button for label " + definition.Label + ".");
                return;
            }

            SetButtonLabel(button.gameObject, definition.Label);
            SetButtonLabelFontSize(button.gameObject, definition.FontSize);
            button.onClick = new Button.ButtonClickedEvent();
            if (definition.IsInteractable)
            {
                button.onClick.AddListener(new UnityAction(delegate
                {
                    if (definition.Label.IndexOf("Creative", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Plugin.Logger.Info("Queue Creative pressed.");
                        StartRankedMatchmaking("Creative");
                    }
                    else
                    {
                        ModClientSessionMode.SelectRankedMultiplayer();
                        Plugin.Logger.Info(definition.Label + " pressed.");
                    }
                }));
            }

            button.interactable = definition.IsInteractable;
            ResetMainMenuButtonVisualState(row);
            ApplyQueueRowPresentation(row, definition);
        }

        private static void EnsureBetterRngSavedGamesButton(MainMenuRightSide rightSide)
        {
            if (rightSide == null)
            {
                return;
            }

            GameObject savedGamesGroup = FindGroup(rightSide, "SavedGames");
            if (savedGamesGroup == null)
            {
                return;
            }

            Transform content = savedGamesGroup.transform.Find("Scroll View/Viewport/SavedGameAreaContent");
            if (content == null)
            {
                return;
            }

            Transform template = content.Find("NewGame");
            if (template == null)
            {
                return;
            }

            GameObject betterRngRow = FindBetterRngSavedGamesRow(content);
            GameObject legacyNamedRow = null;
            Transform legacyNamedTransform = content.Find("ModBetterRngNewGame");
            if (legacyNamedTransform != null)
            {
                legacyNamedRow = legacyNamedTransform.gameObject;
            }

            if (betterRngRow == null && legacyNamedRow != null)
            {
                betterRngRow = legacyNamedRow;
            }

            if (legacyNamedRow != null && betterRngRow != legacyNamedRow)
            {
                legacyNamedRow.SetActive(false);
                UnityEngine.Object.Destroy(legacyNamedRow);
            }

            if (betterRngRow == null)
            {
                betterRngRow = UnityEngine.Object.Instantiate(template.gameObject, content, false);
            }

            Button betterRngButton = betterRngRow.GetComponentInChildren<Button>(true);
            if (betterRngButton != null)
            {
                betterRngButton.onClick = new Button.ButtonClickedEvent();
                betterRngButton.onClick.AddListener(new UnityAction(delegate
                {
                    ModClientSessionMode.SelectBetterRngSingleplayer();

                    uGUI_MainMenu menu = uGUI_MainMenu.main;
                    if (menu != null)
                    {
                        menu.OnButtonNew();
                    }
                }));

                betterRngButton.interactable = true;
            }

            betterRngRow.SetActive(true);
            betterRngRow.name = "NewGame";
            betterRngRow.transform.SetSiblingIndex(Mathf.Min(1, content.childCount - 1));
            SetButtonLabel(betterRngRow, BetterRngSavedGamesButtonLabel);
            SetButtonLabelFontSize(betterRngRow, template.GetComponentInChildren<Text>(true).fontSize);
            SetAlpha(betterRngRow, 1f);
            ResetMainMenuButtonVisualState(betterRngRow);

            ModBetterRngSavedGamesRowMarker marker = betterRngRow.GetComponent<ModBetterRngSavedGamesRowMarker>();
            if (marker == null)
            {
                marker = betterRngRow.AddComponent<ModBetterRngSavedGamesRowMarker>();
            }

            RectTransform contentRect = content as RectTransform;
            if (contentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }
        }

        private static GameObject FindBetterRngSavedGamesRow(Transform content)
        {
            if (content == null)
            {
                return null;
            }

            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.GetComponent<ModBetterRngSavedGamesRowMarker>() != null)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static void DisableSavedGameBehaviors(GameObject modGroup)
        {
            if (modGroup == null)
            {
                return;
            }

            MainMenuLoadPanel[] loadPanels = modGroup.GetComponentsInChildren<MainMenuLoadPanel>(true);
            for (int i = 0; i < loadPanels.Length; i++)
            {
                MainMenuLoadPanel loadPanel = loadPanels[i];
                if (loadPanel != null)
                {
                    loadPanel.enabled = false;
                    UnityEngine.Object.Destroy(loadPanel);
                }
            }

            MainMenuLoadScroll[] loadScrolls = modGroup.GetComponentsInChildren<MainMenuLoadScroll>(true);
            for (int i = 0; i < loadScrolls.Length; i++)
            {
                MainMenuLoadScroll loadScroll = loadScrolls[i];
                if (loadScroll != null)
                {
                    loadScroll.enabled = false;
                    UnityEngine.Object.Destroy(loadScroll);
                }
            }
        }

        private static GameObject FindGroup(MainMenuRightSide rightSide, string name)
        {
            for (int i = 0; i < rightSide.groups.Count; i++)
            {
                GameObject group = rightSide.groups[i];
                if (group != null && string.Equals(group.name, name, StringComparison.Ordinal))
                {
                    return group;
                }
            }

            return null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, name, StringComparison.Ordinal))
            {
                return root;
            }

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform result = FindDescendantByName(root.GetChild(i), name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void OpenRankedModeSelect(uGUI_MainMenu menu, MainMenuRightSide rightSide, Button modButton)
        {
            if (rightSide == null)
            {
                return;
            }

            ModClientSessionMode.SelectVanilla();
            rightSide.OpenGroup(ModModeSelectGroupName);
            SelectPrimaryButton(menu, modButton);
        }

        private static void OpenRankedPracticeNewGame(uGUI_MainMenu menu, MainMenuRightSide rightSide, Button modButton)
        {
            if (rightSide == null)
            {
                return;
            }

            rightSide.OpenGroup(ModPracticeNewGameGroupName);
            SelectPrimaryButton(menu, modButton);
        }

        private static void OpenRankedQueueGroup(uGUI_MainMenu menu, MainMenuRightSide rightSide, Button modButton)
        {
            if (rightSide == null)
            {
                return;
            }

            rightSide.OpenGroup(ModQueueGroupName);
            SelectPrimaryButton(menu, modButton);
        }

        private static void StartRankedPracticeNewGame(GameMode gameMode)
        {
            if (Ruleset8RaceSession.IsStartingNewGame(uGUI_MainMenu.main)) return;
            Plugin.Logger.Info("Singleplayer Race new-game button clicked for mode '" + gameMode + "'.");
            ModClientSessionMode.SelectRankedSingleplayerPractice();

            uGUI_MainMenu menu = uGUI_MainMenu.main;
            if (menu == null)
            {
                Plugin.LogError("Singleplayer Race could not start because uGUI_MainMenu.main was null.");
                return;
            }

            if (!Ruleset8RaceSession.PrepareSingleplayerFromPool(gameMode))
            {
                string message = gameMode + " has no filtered race seeds available yet.";
                Plugin.LogError(message);
                if (uGUI.main != null && uGUI.main.confirmation != null)
                {
                    uGUI.main.confirmation.Show(message, delegate(bool confirmed) { });
                }
                return;
            }

            try
            {
                switch (gameMode)
                {
                    case GameMode.Survival:
                        Plugin.Logger.Info("Calling the game Survival new-save path for Singleplayer Race.");
                        menu.OnButtonSurvival();
                        break;
                    case GameMode.Creative:
                        menu.OnButtonCreative();
                        break;
                    case GameMode.Hardcore:
                        menu.OnButtonHardcore();
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Singleplayer Race failed while starting the game mode '" + gameMode + "'.", ex);
                const string message = "Singleplayer Race could not start. Check the runtime log for details.";
                if (uGUI.main != null && uGUI.main.confirmation != null)
                {
                    uGUI.main.confirmation.Show(message, delegate(bool confirmed) { });
                }
            }
        }

        private static void StartRankedMatchmaking(string mode)
        {
            ModClientSessionMode.SelectRankedMultiplayer(mode);
            ModRankedMatchmakingRuntimeHost.StartQueue(mode);

            MainMenuRightSide rightSide = MainMenuRightSide.main;
            if (rightSide != null)
            {
                rightSide.OpenGroup(ModMatchmakingGroupName);
            }

            Button rankedButton = FindPrimaryButton("ButtonRanked");
            SelectPrimaryButton(uGUI_MainMenu.main, rankedButton);
        }

        private static Button FindButtonByTranslationKey(GameObject root, string translationKey)
        {
            if (root == null || string.IsNullOrEmpty(translationKey))
            {
                return null;
            }

            TranslationLiveUpdate[] updates = root.GetComponentsInChildren<TranslationLiveUpdate>(true);
            for (int i = 0; i < updates.Length; i++)
            {
                TranslationLiveUpdate update = updates[i];
                if (update == null || !string.Equals(update.translationKey, translationKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return FindButtonAncestor(update.transform);
            }

            return null;
        }

        private static Button FindButtonByText(GameObject root, string textValue)
        {
            if (root == null || string.IsNullOrEmpty(textValue))
            {
                return null;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || !string.Equals(text.text, textValue, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return FindButtonAncestor(text.transform);
            }

            return null;
        }

        private static Button FindButtonAncestor(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                Button button = current.GetComponent<Button>();
                if (button != null)
                {
                    return button;
                }

                current = current.parent;
            }

            return null;
        }

        private static void SelectPrimaryButton(uGUI_MainMenu menu, Button button)
        {
            if (menu != null && menu.primaryOptions != null && button != null)
            {
                menu.primaryOptions.SelectItem(button);
            }

            if (EventSystem.current != null && button != null)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }
        }

        private static void SetButtonLabel(GameObject root, string textValue)
        {
            if (root == null)
            {
                return;
            }

            uGUI_Text[] localizers = root.GetComponentsInChildren<uGUI_Text>(true);
            for (int i = 0; i < localizers.Length; i++)
            {
                uGUI_Text localizer = localizers[i];
                if (localizer != null)
                {
                    localizer.SetText(textValue, false);
                    return;
                }
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text != null)
                {
                    text.text = textValue;
                    return;
                }
            }
        }

        private static void RemoveLocalizationComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            uGUI_Text[] localizers = root.GetComponentsInChildren<uGUI_Text>(true);
            for (int i = 0; i < localizers.Length; i++)
            {
                uGUI_Text localizer = localizers[i];
                if (localizer != null)
                {
                    UnityEngine.Object.Destroy(localizer);
                }
            }

            TranslationLiveUpdate[] translationUpdates = root.GetComponentsInChildren<TranslationLiveUpdate>(true);
            for (int i = 0; i < translationUpdates.Length; i++)
            {
                TranslationLiveUpdate translationUpdate = translationUpdates[i];
                if (translationUpdate != null)
                {
                    UnityEngine.Object.Destroy(translationUpdate);
                }
            }
        }

        private static void ConfigureActionRow(GameObject row, string label, bool isInteractable, int fontSize, UnityAction onClick)
        {
            if (row == null)
            {
                return;
            }

            Button button = row.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find action row button for label " + label + ".");
                return;
            }

            RemoveLocalizationComponents(button.gameObject);
            SetButtonLabel(button.gameObject, label);
            SetButtonLabelFontSize(button.gameObject, fontSize);
            button.onClick = new Button.ButtonClickedEvent();
            if (isInteractable && onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            button.interactable = isInteractable;
            ResetMainMenuButtonVisualState(row);
            ApplyQueueRowPresentation(row, new QueueRowDefinition(label, isInteractable, fontSize));
        }

        private static void DestroySiblingRows(Transform content, Transform template)
        {
            List<GameObject> rowsToDestroy = new List<GameObject>();
            int childCount = content.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (child != template)
                {
                    rowsToDestroy.Add(child.gameObject);
                }
            }

            for (int i = 0; i < rowsToDestroy.Count; i++)
            {
                rowsToDestroy[i].SetActive(false);
                UnityEngine.Object.Destroy(rowsToDestroy[i]);
            }
        }

        private static Button FindPrimaryButton(string objectName)
        {
            if (uGUI_MainMenu.main == null || uGUI_MainMenu.main.primaryOptions == null)
            {
                return null;
            }

            Transform transform = FindDescendantByName(uGUI_MainMenu.main.primaryOptions.transform, objectName);
            return transform != null ? transform.GetComponent<Button>() : null;
        }

        private static void RestoreVanillaSingleplayerPanelPresentation(MainMenuRightSide rightSide)
        {
            if (rightSide == null)
            {
                return;
            }

            GameObject savedGamesGroup = FindGroup(rightSide, "SavedGames");
            if (savedGamesGroup != null)
            {
                RemoveCustomPanelTitle(savedGamesGroup);
                RestoreOriginalPanelTitles(savedGamesGroup);
            }
        }

        private static void SetPanelTitle(GameObject panelGroup, string title)
        {
            if (panelGroup == null)
            {
                return;
            }

            EnsureCustomPanelTitle(panelGroup, title);
            HideOriginalPanelTitles(panelGroup);
        }

        private static void RemoveCustomPanelTitle(GameObject panelGroup)
        {
            if (panelGroup == null)
            {
                return;
            }

            Transform existingTransform = panelGroup.transform.Find("ModCustomPanelTitle");
            if (existingTransform == null)
            {
                return;
            }

            existingTransform.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(existingTransform.gameObject);
        }

        private static void SetButtonLabelFontSize(GameObject root, int fontSize)
        {
            if (root == null)
            {
                return;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                text.fontSize = fontSize;
            }
        }

        private static void ApplyQueueRowPresentation(GameObject row, QueueRowDefinition definition)
        {
            if (row == null)
            {
                return;
            }

            SetAlpha(row, definition.IsInteractable ? 1f : ComingSoonRowAlpha);

            Text[] texts = row.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                text.fontSize = definition.FontSize;
                text.color = definition.IsInteractable ? Color.white : new Color(0.9f, 0.9f, 0.9f, 0.95f);
                text.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static void EnsureCustomPanelTitle(GameObject panelGroup, string title)
        {
            Transform existingTransform = panelGroup.transform.Find("ModCustomPanelTitle");
            Text titleText = existingTransform != null ? existingTransform.GetComponent<Text>() : null;
            if (titleText == null)
            {
                Text template = panelGroup.GetComponentInChildren<Text>(true);
                if (template == null)
                {
                    return;
                }

                GameObject titleObject = UnityEngine.Object.Instantiate(template.gameObject, panelGroup.transform, false);
                titleObject.name = "ModCustomPanelTitle";

                uGUI_Text localizer = titleObject.GetComponent<uGUI_Text>();
                if (localizer != null)
                {
                    UnityEngine.Object.Destroy(localizer);
                }

                titleText = titleObject.GetComponent<Text>();
                if (titleText == null)
                {
                    return;
                }
            }

            RectTransform rectTransform = titleText.rectTransform;
            rectTransform.SetParent(panelGroup.transform, false);
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(24f, -22f);
            rectTransform.sizeDelta = new Vector2(-48f, 58f);
            rectTransform.localScale = Vector3.one;

            titleText.text = title;
            titleText.fontSize = PanelTitleFontSize;
            titleText.alignment = TextAnchor.UpperLeft;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.raycastTarget = false;
            titleText.color = Color.white;
        }

        private static void SetCustomPanelTitleFontSize(GameObject panelGroup, int fontSize)
        {
            if (panelGroup == null)
            {
                return;
            }

            Transform existingTransform = panelGroup.transform.Find("ModCustomPanelTitle");
            Text titleText = existingTransform != null ? existingTransform.GetComponent<Text>() : null;
            if (titleText == null)
            {
                return;
            }

            titleText.fontSize = fontSize;
        }

        private static void EnsurePanelPlaceholder(GameObject panelGroup, Transform content, string objectName, string textValue)
        {
            if (panelGroup == null || content == null)
            {
                return;
            }

            Transform existingTransform = content.Find(objectName);
            Text placeholderText = existingTransform != null ? existingTransform.GetComponent<Text>() : null;
            if (placeholderText == null)
            {
                Text template = panelGroup.GetComponentInChildren<Text>(true);
                if (template == null)
                {
                    return;
                }

                GameObject placeholderObject = UnityEngine.Object.Instantiate(template.gameObject, content, false);
                placeholderObject.name = objectName;

                uGUI_Text localizer = placeholderObject.GetComponent<uGUI_Text>();
                if (localizer != null)
                {
                    UnityEngine.Object.Destroy(localizer);
                }

                placeholderText = placeholderObject.GetComponent<Text>();
                if (placeholderText == null)
                {
                    return;
                }
            }

            RectTransform rectTransform = placeholderText.rectTransform;
            rectTransform.SetParent(content, false);
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0f, -58f);
            rectTransform.sizeDelta = new Vector2(-60f, 60f);
            rectTransform.localScale = Vector3.one;

            placeholderText.text = textValue;
            placeholderText.alignment = TextAnchor.UpperCenter;
            placeholderText.horizontalOverflow = HorizontalWrapMode.Wrap;
            placeholderText.verticalOverflow = VerticalWrapMode.Overflow;
            placeholderText.raycastTarget = false;
            placeholderText.color = Color.white;
            placeholderText.resizeTextForBestFit = false;
            placeholderText.fontSize = Math.Max(placeholderText.fontSize, 17);
        }

        private static void HideOriginalPanelTitles(GameObject panelGroup)
        {
            Text[] texts = panelGroup.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || string.Equals(text.gameObject.name, "ModCustomPanelTitle", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(text.text, "Play", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Play Singleplayer", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Options", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "PrivateRaceRoom", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Private Race Room", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Choose Mode", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "New Game", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Private Race Room", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Connecting", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Queue Ranked Matches", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Queue Random Race", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Leaderboard", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, UpdatePanelTitleText, StringComparison.OrdinalIgnoreCase))
                {
                    text.gameObject.SetActive(false);
                }
            }
        }

        private static void RestoreOriginalPanelTitles(GameObject panelGroup)
        {
            if (panelGroup == null)
            {
                return;
            }

            Text[] texts = panelGroup.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || string.Equals(text.gameObject.name, "ModCustomPanelTitle", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(text.text, "Play", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Play Singleplayer", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "New Game", StringComparison.OrdinalIgnoreCase))
                {
                    text.gameObject.SetActive(true);
                }
            }
        }

        private static void SetAlpha(GameObject root, float alpha)
        {
            if (root == null)
            {
                return;
            }

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                Color color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }
        }

        private static void ResetMainMenuButtonVisualState(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            MainMenuOffsetAnimator[] animators = root.GetComponentsInChildren<MainMenuOffsetAnimator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                MainMenuOffsetAnimator animator = animators[i];
                if (animator == null || animator.target == null)
                {
                    continue;
                }

                animator.OnPointerExit(null);
                animator.target.localPosition -= animator.offset;
            }

            uGUI_BasicColorSwap[] colorSwaps = root.GetComponentsInChildren<uGUI_BasicColorSwap>(true);
            for (int i = 0; i < colorSwaps.Length; i++)
            {
                uGUI_BasicColorSwap colorSwap = colorSwaps[i];
                if (colorSwap != null)
                {
                    colorSwap.makeTextWhite();
                }
            }
        }
        private struct QueueRowDefinition
        {
            public QueueRowDefinition(string label, bool isInteractable, int fontSize)
            {
                Label = label;
                IsInteractable = isInteractable;
                FontSize = fontSize;
            }

            public string Label { get; private set; }

            public bool IsInteractable { get; private set; }

            public int FontSize { get; private set; }
        }
        private sealed class ModBetterRngSavedGamesRowMarker : MonoBehaviour
        {
        }
        private sealed class ModPrivateRaceChoiceRowMarker : MonoBehaviour
        {
        }
        private sealed class ModPrivateRaceStartButtonMarker : MonoBehaviour
        {
        }
    }

    [HarmonyPatch(typeof(uGUI_MainMenu), "LoadGameAsync")]
    internal static class LoadVanillaSave
    {
        private static void Prefix() { MainMenuUI.SelectVanilla(); }
    }
}
