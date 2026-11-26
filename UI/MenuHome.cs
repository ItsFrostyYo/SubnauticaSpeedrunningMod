using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ItsFrosti.SpeedrunningMod.Ui
{
    // Preserve the original informational home-panel layout. Actual downloads
    // and update prompts continue to belong to SNSRModLoader.
    internal static partial class MainMenuUI
    {
        private const string LeaderboardHomeGroupName = "ModLeaderboardHome";
        private const string UpdatePanelBodyObjectName = "ModUpdatePanelBody";
        private const string UpdatePanelBodyText = "Ported the mod to SNSRModLoader, removed Practice Save Files, and added Settings for choosing Display Name and Toggling Server.";
        private const int UpdatePanelBodyFontSize = 14;

        private static void EnsureLeaderboardHomeGroup(MainMenuRightSide rightSide)
        {
            GameObject leaderboardGroup = FindGroup(rightSide, LeaderboardHomeGroupName);
            if (leaderboardGroup == null)
            {
                GameObject savedGamesGroup = FindGroup(rightSide, "SavedGames");
                if (savedGamesGroup == null)
                {
                    Plugin.Logger.Warning("Ranked UI could not find SavedGames group to clone for Leaderboard.");
                    return;
                }

                leaderboardGroup = UnityEngine.Object.Instantiate(savedGamesGroup, savedGamesGroup.transform.parent, false);
                leaderboardGroup.name = LeaderboardHomeGroupName;
                leaderboardGroup.transform.SetSiblingIndex(savedGamesGroup.transform.GetSiblingIndex());
                leaderboardGroup.SetActive(false);
                rightSide.groups.Add(leaderboardGroup);
            }

            DisableSavedGameBehaviors(leaderboardGroup);
            ConfigureLeaderboardHomeGroup(leaderboardGroup);
        }

        private static void ConfigureLeaderboardHomeGroup(GameObject leaderboardGroup)
        {
            if (leaderboardGroup == null)
            {
                return;
            }

            Transform content = leaderboardGroup.transform.Find("Scroll View/Viewport/SavedGameAreaContent");
            if (content == null)
            {
                Plugin.Logger.Warning("Ranked UI could not find SavedGameAreaContent in Leaderboard panel.");
                return;
            }

            List<GameObject> childrenToDestroy = new List<GameObject>();
            for (int i = 0; i < content.childCount; i++)
            {
                childrenToDestroy.Add(content.GetChild(i).gameObject);
            }

            for (int i = 0; i < childrenToDestroy.Count; i++)
            {
                childrenToDestroy[i].SetActive(false);
                UnityEngine.Object.Destroy(childrenToDestroy[i]);
            }

            ConfigureUpdatePanelLayout(leaderboardGroup);
            EnsureUpdatePanelBodyText(leaderboardGroup, UpdatePanelBodyText);
            SetPanelTitle(leaderboardGroup, UpdatePanelTitleText);
        }

        private static void EnsureLeaderboardHomeVisible(MainMenuRightSide rightSide)
        {
            if (rightSide == null)
            {
                return;
            }

            GameObject leaderboardGroup = FindGroup(rightSide, LeaderboardHomeGroupName);
            if (leaderboardGroup == null)
            {
                return;
            }

            bool shouldBeVisible = ShouldShowUpdateHomePanel(rightSide, leaderboardGroup);
            if (shouldBeVisible)
            {
                leaderboardGroup.transform.SetAsLastSibling();
                if (!leaderboardGroup.activeSelf)
                {
                    leaderboardGroup.SetActive(true);
                }
            }
            else if (leaderboardGroup.activeSelf)
            {
                leaderboardGroup.SetActive(false);
            }
        }

        private static bool ShouldShowUpdateHomePanel(MainMenuRightSide rightSide, GameObject leaderboardGroup)
        {
            if (rightSide == null || leaderboardGroup == null)
            {
                return false;
            }

            for (int i = 0; i < rightSide.groups.Count; i++)
            {
                GameObject group = rightSide.groups[i];
                if (group == null ||
                    group == leaderboardGroup ||
                    group == rightSide.homeGroup)
                {
                    continue;
                }

                if (group.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ConfigureUpdatePanelLayout(GameObject panelGroup)
        {
            if (panelGroup == null)
            {
                return;
            }

            panelGroup.transform.SetAsLastSibling();

            RectTransform panelRect = panelGroup.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(1f, 0.5f);
                panelRect.anchorMax = new Vector2(1f, 0.5f);
                panelRect.pivot = new Vector2(1f, 0.5f);
                panelRect.anchoredPosition = new Vector2(-18f, -34f);
                panelRect.sizeDelta = new Vector2(420f, 300f);
                panelRect.localScale = Vector3.one;
            }

            Transform scrollViewTransform = panelGroup.transform.Find("Scroll View");
            RectTransform scrollViewRect = scrollViewTransform != null ? scrollViewTransform.GetComponent<RectTransform>() : null;
            if (scrollViewRect != null)
            {
                scrollViewRect.anchorMin = new Vector2(0f, 0f);
                scrollViewRect.anchorMax = new Vector2(1f, 1f);
                scrollViewRect.offsetMin = new Vector2(14f, 14f);
                scrollViewRect.offsetMax = new Vector2(-14f, -46f);
                scrollViewRect.localScale = Vector3.one;
            }

            Transform scrollbarTransform = panelGroup.transform.Find("Scroll View/Scrollbar Vertical");
            if (scrollbarTransform != null)
            {
                scrollbarTransform.gameObject.SetActive(false);
            }
        }

        private static void EnsureUpdatePanelBodyText(GameObject panelGroup, string textValue)
        {
            if (panelGroup == null)
            {
                return;
            }

            Transform existingTransform = panelGroup.transform.Find(UpdatePanelBodyObjectName);
            Text bodyText = existingTransform != null ? existingTransform.GetComponent<Text>() : null;
            if (bodyText == null)
            {
                Text template = panelGroup.GetComponentInChildren<Text>(true);
                if (template == null)
                {
                    return;
                }

                GameObject bodyObject = UnityEngine.Object.Instantiate(template.gameObject, panelGroup.transform, false);
                bodyObject.name = UpdatePanelBodyObjectName;

                uGUI_Text localizer = bodyObject.GetComponent<uGUI_Text>();
                if (localizer != null)
                {
                    UnityEngine.Object.Destroy(localizer);
                }

                bodyText = bodyObject.GetComponent<Text>();
                if (bodyText == null)
                {
                    return;
                }
            }

            RectTransform rectTransform = bodyText.rectTransform;
            rectTransform.SetParent(panelGroup.transform, false);
            rectTransform.SetAsLastSibling();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(20f, -54f);
            rectTransform.sizeDelta = new Vector2(-40f, 196f);
            rectTransform.localScale = Vector3.one;

            bodyText.text = textValue;
            bodyText.fontSize = UpdatePanelBodyFontSize;
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.raycastTarget = false;
            bodyText.resizeTextForBestFit = false;
            bodyText.color = Color.white;
            bodyText.supportRichText = true;
            bodyText.gameObject.SetActive(true);
        }
    }
}
