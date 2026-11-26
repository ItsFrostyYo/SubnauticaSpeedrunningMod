using UnityEngine;
using UnityEngine.UI;

namespace ItsFrosti.SpeedrunningMod.Ui
{
    internal static class ModOverlayUiUtility
    {
        public static Text FindTemplateText()
        {
            // The original menu disables both native version labels before
            // choosing its HUD font. Follow that same fallback without changing
            // the loader-owned watermark. Never choose fonts from loaded prefabs.
            if (uGUI.main != null && uGUI.main.screenCanvas != null)
            {
                Text[] screenTexts = uGUI.main.screenCanvas.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < screenTexts.Length; i++)
                {
                    Text screenText = screenTexts[i];
                    if (screenText != null)
                    {
                        return screenText;
                    }
                }
            }

            return Object.FindObjectOfType<Text>();
        }
    }
}
