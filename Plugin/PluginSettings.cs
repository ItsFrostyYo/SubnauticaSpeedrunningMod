using SNSRModLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ItsFrosti.SpeedrunningMod
{
    [HarmonyPatch(typeof(uGUI_OptionsPanel), "AddTabs")]
    internal static class PluginSettings
    {
        private static SettingsFile settings;
        internal static bool EnableNetworking { get; private set; }
        internal static string ServerUrl { get; private set; }
        internal static string DisplayName { get; private set; }
        internal static string PlayerId { get; private set; }

        internal static void Initialize(SettingsFile file)
        {
            settings = file;
            EnableNetworking = settings.Get("EnableNetworking", true);
            ServerUrl = settings.Get("ServerUrl", "https://subnautica-speedrunning-server.onrender.com/");
            DisplayName = ModPrivateRaceRoomRuntimeHost.SanitizeDisplayName(settings.Get("DisplayName", "Player"));
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = "Player";
            PlayerId = settings.Get("PlayerId", "");
            if (string.IsNullOrEmpty(PlayerId)) PlayerId = System.Guid.NewGuid().ToString("N");
            Save();
        }

        private static void Save()
        {
            settings.Set("EnableNetworking", EnableNetworking);
            settings.Set("ServerUrl", ServerUrl);
            settings.Set("DisplayName", DisplayName);
            settings.Set("PlayerId", PlayerId);
            settings.Save();
        }

        private static void Postfix(uGUI_OptionsPanel __instance)
        {
            int tab = __instance.AddTab("Race");
            __instance.AddToggleOption(tab, "Server connection", EnableNetworking, value => { EnableNetworking = value; Save(); });
            AddDisplayNameInput(__instance, tab);
        }

        private static void AddDisplayNameInput(uGUI_OptionsPanel panel, int tab)
        {
            // The options builder has no text-entry row. Combine its native
            // labelled row with the native user-input field before AddItem so
            // the game's own builder also wires keyboard/controller navigation.
            var template = Object.Instantiate(panel.choiceOptionPrefab);
            var choice = template.GetComponentInChildren<uGUI_Choice>(true);
            var valueText = choice.currentText;
            var label = template.GetComponentInChildren<Text>(true);
            label.text = "Display Name";
            var translation = label.GetComponent<TranslationLiveUpdate>();
            if (translation != null) translation.translationKey = "Display Name";

            var inputObject = Object.Instantiate(uGUI.main.userInput.inputField.gameObject, valueText.transform, false);
            inputObject.name = "RaceDisplayName";
            var rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            var field = inputObject.GetComponent<InputField>();
            field.onValueChanged = new InputField.OnChangeEvent();
            field.onEndEdit = new InputField.SubmitEvent();
            field.contentType = InputField.ContentType.Standard;
            field.lineType = InputField.LineType.SingleLine;
            field.readOnly = false;
            field.interactable = true;
            field.enabled = true;
            field.characterLimit = 24;
            field.onValidateInput = null;
            field.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            field.text = DisplayName;
            inputObject.SetActive(true);
            valueText.enabled = false;
            Object.DestroyImmediate(choice.previousButton.gameObject);
            Object.DestroyImmediate(choice.nextButton.gameObject);
            Object.DestroyImmediate(choice);
            template.SetActive(true);
            GameObject row;
            try { row = panel.AddItem(tab, template); }
            finally { Object.DestroyImmediate(template); }

            var input = row.GetComponentInChildren<InputField>(true);
            input.onValueChanged.AddListener(SetDisplayName);
            input.onEndEdit.AddListener(value => {
                SetDisplayName(value);
                if (input.text != DisplayName) input.text = DisplayName;
            });
        }

        internal static void SetDisplayName(string value)
        {
            DisplayName = ModPrivateRaceRoomRuntimeHost.SanitizeDisplayName(value);
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = "Player";
            var identity = ModClientIdentityStore.GetIdentity();
            if (identity != null) identity.DisplayName = DisplayName;
            Save();
        }
    }
}
