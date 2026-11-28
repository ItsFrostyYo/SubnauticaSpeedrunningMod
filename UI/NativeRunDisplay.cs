using UnityEngine;
using UnityEngine.UI;

namespace ItsFrosti.SpeedrunningMod.Ui
{
    internal static class ModOverlayRuntime
    {
        private const string OverlayRootName = "SubnauticaSpeedrunningMod.PersistentOverlay";
        private static OverlayBehaviour _behaviour;
        private static string _timerText = "0.000";
        private static bool _timerVisible;
        private static string _categoryText = string.Empty;
        private static GUIStyle _categoryStyle;
        private static string _runTitleText = string.Empty;
        private static string _runSubtitleText = string.Empty;
        private static string _runComparisonText = string.Empty;
        private static bool _runStatusVisible;
        private static Color _runTitleColor = new Color(1f, 0.76f, 0.36f, 1f);
        private static Color _runComparisonColor = Color.white;
        private static string _verificationTitleText = string.Empty;
        private static string _verificationDetailText = string.Empty;
        private static bool _verificationVisible;
        private static Color _verificationTitleColor = Color.white;
        private static Color _verificationDetailColor = Color.white;
        private static string _topCenterTitleText = string.Empty;
        private static string _topCenterDetailText = string.Empty;
        private static bool _topCenterVisible;
        private static Color _topCenterTitleColor = Color.white;
        private static Color _topCenterDetailColor = Color.white;
        private static string _centerMessageText = string.Empty;
        private static bool _centerMessageVisible;
        private static Color _centerMessageColor = Color.white;
        private static Font _font;
        private static FontStyle _fontStyle = FontStyle.Normal;
        private static Color _textColor = Color.white;
        private static float _nextTemplateRefreshAt;
        private static GUIStyle _topRightStyle;
        private static GUIStyle _timerOutlineStyle;
        private static GUIStyle _queueTitleStyle;
        private static GUIStyle _queueDetailStyle;
        private static GUIStyle _runTitleStyle;
        private static GUIStyle _runSubtitleStyle;
        private static GUIStyle _runComparisonStyle;
        private static GUIStyle _verificationTitleStyle;
        private static GUIStyle _verificationDetailStyle;
        private static GUIStyle _topCenterTitleStyle;
        private static GUIStyle _topCenterDetailStyle;
        private static GUIStyle _centerMessageStyle;

        public static void EnsureInstalled()
        {
            if (_behaviour != null)
            {
                return;
            }

            GameObject root = GameObject.Find(OverlayRootName);
            if (root == null)
            {
                root = new GameObject(OverlayRootName);
                Object.DontDestroyOnLoad(root);
                root.AddComponent<SceneCleanerPreserve>();
            }

            _behaviour = root.GetComponent<OverlayBehaviour>();
            if (_behaviour == null)
            {
                _behaviour = root.AddComponent<OverlayBehaviour>();
            }
        }

        public static void SetTimer(string text, bool visible)
        {
            EnsureInstalled();
            _timerText = text ?? "0.000";
            _timerVisible = visible;
        }

        public static void SetCategoryLabel(string text)
        {
            _categoryText = text ?? string.Empty;
        }

        public static void SetRunStatus(string title, string subtitle, string comparison, Color titleColor, Color comparisonColor, bool visible)
        {
            EnsureInstalled();
            _runTitleText = title ?? string.Empty;
            _runSubtitleText = subtitle ?? string.Empty;
            _runComparisonText = comparison ?? string.Empty;
            _runTitleColor = titleColor;
            _runComparisonColor = comparisonColor;
            _runStatusVisible = visible;
        }

        public static void SetVerification(string title, string detail, Color titleColor, Color detailColor, bool visible)
        {
            EnsureInstalled();
            _verificationTitleText = title ?? string.Empty;
            _verificationDetailText = detail ?? string.Empty;
            _verificationTitleColor = titleColor;
            _verificationDetailColor = detailColor;
            _verificationVisible = visible;
        }

        public static void SetTopCenterMessage(string title, string detail, Color titleColor, Color detailColor, bool visible)
        {
            EnsureInstalled();
            _topCenterTitleText = title ?? string.Empty;
            _topCenterDetailText = detail ?? string.Empty;
            _topCenterTitleColor = titleColor;
            _topCenterDetailColor = detailColor;
            _topCenterVisible = visible;
        }

        public static void SetCenterMessage(string text, Color color, bool visible)
        {
            EnsureInstalled();
            _centerMessageText = text ?? string.Empty;
            _centerMessageColor = color;
            _centerMessageVisible = visible;
        }

        private static void RefreshTemplateStyle()
        {
            if (Time.unscaledTime < _nextTemplateRefreshAt)
            {
                return;
            }

            _nextTemplateRefreshAt = Time.unscaledTime + 1f;

            Text template = ModOverlayUiUtility.FindTemplateText();
            if (template == null)
            {
                return;
            }

            if (template.font == null) return;
            if (_font == template.font && _fontStyle == template.fontStyle) return;
            _font = template.font;
            _fontStyle = template.fontStyle;
            _textColor = Color.white;
            _categoryStyle = null;
            _topRightStyle = null;
            _timerOutlineStyle = null;
            _queueTitleStyle = null;
            _queueDetailStyle = null;
            _runTitleStyle = null;
            _runSubtitleStyle = null;
            _runComparisonStyle = null;
            _verificationTitleStyle = null;
            _verificationDetailStyle = null;
            _topCenterTitleStyle = null;
            _topCenterDetailStyle = null;
            _centerMessageStyle = null;
        }

        private static void Draw()
        {
            RefreshTemplateStyle();
            PrepareGlyphs();
            if (ModRankedMatchmakingRuntimeHost.IsBusy) DrawQueueStatus();

            if (_timerVisible && !string.IsNullOrEmpty(_categoryText))
                DrawTopRightLabel(_categoryText, 18, 12f, _textColor);

            if (_timerVisible && !string.IsNullOrEmpty(_timerText))
            {
                DrawTopRightLabel(_timerText, 42, 40f, _textColor);
            }

            if (_runStatusVisible && !string.IsNullOrEmpty(_runTitleText))
            {
                DrawRunStatus();
            }

            if (_verificationVisible && !string.IsNullOrEmpty(_verificationTitleText))
            {
                DrawVerification();
            }

            if (_topCenterVisible && !string.IsNullOrEmpty(_topCenterTitleText))
            {
                DrawTopCenterMessage();
            }

            if (_centerMessageVisible && !string.IsNullOrEmpty(_centerMessageText))
            {
                DrawCenterMessage();
            }
        }

        private static void DrawQueueStatus()
        {
            string title = ModRankedMatchmakingRuntimeHost.GetHudTitle();
            string detail = ModRankedMatchmakingRuntimeHost.GetHudDetail();
            float width = System.Math.Min(600f, Screen.width * 0.6f);
            float left = (Screen.width - width) * 0.5f;
            float top = 16f;
            if (_queueTitleStyle == null)
            {
                _queueTitleStyle = CreateBaseStyle(TextAnchor.UpperCenter);
                _queueTitleStyle.fontSize = 24;
                _queueTitleStyle.wordWrap = true;
                _queueDetailStyle = new GUIStyle(_queueTitleStyle);
                _queueDetailStyle.fontSize = 28;
            }
            var titleStyle = _queueTitleStyle;
            float height = titleStyle.CalcHeight(new GUIContent(title), width);
            GUI.Label(new Rect(left, top, width, height + 4f), title, titleStyle);
            var detailStyle = _queueDetailStyle;
            GUI.Label(new Rect(left, top + height + 4f, width, 70f), detail, detailStyle);
        }

        private static void DrawTopRightLabel(string text, int fontSize, float top, Color color)
        {
            GUIStyle style = GetTopRightStyle(fontSize, color);

            Vector2 size = style.CalcSize(new GUIContent(text));
            Rect rect = new Rect(Screen.width - size.x - 12f, top, size.x + 2f, size.y + 4f);
            if (fontSize != 42)
            {
                GUI.Label(rect, text, style);
                return;
            }
            if (_timerOutlineStyle == null)
            {
                _timerOutlineStyle = new GUIStyle(style);
                _timerOutlineStyle.normal.textColor = Color.black;
            }
            for (int x = -2; x <= 2; x++)
                for (int y = -2; y <= 2; y++)
                    if (x != 0 || y != 0)
                        GUI.Label(new Rect(rect.x + x, rect.y + y, rect.width, rect.height), text, _timerOutlineStyle);
            GUI.Label(rect, text, style);
        }

        private static void DrawRunStatus()
        {
            GUIStyle titleStyle = GetRunTitleStyle();
            GUIStyle subtitleStyle = GetRunSubtitleStyle();

            float left = 12f;
            float top = 12f;
            float width = 520f;
            float titleHeight = titleStyle.CalcHeight(new GUIContent(_runTitleText), width);
            GUI.Label(new Rect(left, top, width, titleHeight + 4f), _runTitleText, titleStyle);

            if (string.IsNullOrEmpty(_runSubtitleText))
            {
                return;
            }

            float subtitleTop = top + titleHeight + 2f;
            float subtitleHeight = subtitleStyle.CalcHeight(new GUIContent(_runSubtitleText), width);
            GUI.Label(new Rect(left, subtitleTop, width, subtitleHeight + 4f), _runSubtitleText, subtitleStyle);

            if (string.IsNullOrEmpty(_runComparisonText))
            {
                return;
            }

            GUIStyle comparisonStyle = GetRunComparisonStyle();
            float comparisonTop = subtitleTop + subtitleHeight + 2f;
            float comparisonHeight = comparisonStyle.CalcHeight(new GUIContent(_runComparisonText), width);
            GUI.Label(new Rect(left, comparisonTop, width, comparisonHeight + 4f), _runComparisonText, comparisonStyle);
        }

        private static GUIStyle GetTopRightStyle(int fontSize, Color color)
        {
            if (fontSize == 18)
            {
                if (_categoryStyle == null)
                {
                    _categoryStyle = CreateBaseStyle(TextAnchor.UpperRight);
                    _categoryStyle.fontSize = 18;
                }
                _categoryStyle.normal.textColor = color;
                return _categoryStyle;
            }
            if (_topRightStyle == null)
            {
                _topRightStyle = CreateBaseStyle(TextAnchor.UpperRight);
            }
            _topRightStyle.fontSize = fontSize;
            _topRightStyle.normal.textColor = color;
            return _topRightStyle;
        }

        private static void PrepareGlyphs()
        {
            if (_font == null || !_font.dynamic) return;
            // Populate the shared dynamic font atlas before drawing any labels.
            // Requesting glyphs midway through drawing can invalidate earlier text.
            if (_timerVisible) { Request(_categoryText, 18); Request(_timerText, 42); }
            if (ModRankedMatchmakingRuntimeHost.IsBusy) { Request(ModRankedMatchmakingRuntimeHost.GetHudTitle(), 24); Request(ModRankedMatchmakingRuntimeHost.GetHudDetail(), 28); }
            if (_runStatusVisible) { Request(_runTitleText, 28); Request(_runSubtitleText, 24); Request(_runComparisonText, 21); }
            if (_verificationVisible) { Request(_verificationTitleText, 28); Request(_verificationDetailText, 22); }
            if (_topCenterVisible) { Request(_topCenterTitleText, 30); Request(_topCenterDetailText, 22); }
            if (_centerMessageVisible) Request(_centerMessageText, 44);
        }
        private static void Request(string text, int size)
        {
            if (!string.IsNullOrEmpty(text)) _font.RequestCharactersInTexture(text, size, _fontStyle);
        }

        private static GUIStyle GetRunTitleStyle()
        {
            if (_runTitleStyle == null)
            {
                _runTitleStyle = CreateBaseStyle(TextAnchor.UpperLeft);
                _runTitleStyle.wordWrap = true;
                _runTitleStyle.fontSize = 28;
            }

            _runTitleStyle.normal.textColor = _runTitleColor;
            return _runTitleStyle;
        }

        private static GUIStyle GetRunSubtitleStyle()
        {
            if (_runSubtitleStyle == null)
            {
                _runSubtitleStyle = CreateBaseStyle(TextAnchor.UpperLeft);
                _runSubtitleStyle.wordWrap = true;
                _runSubtitleStyle.fontSize = 24;
            }

            _runSubtitleStyle.normal.textColor = _textColor;
            return _runSubtitleStyle;
        }

        private static GUIStyle GetRunComparisonStyle()
        {
            if (_runComparisonStyle == null)
            {
                _runComparisonStyle = CreateBaseStyle(TextAnchor.UpperLeft);
                _runComparisonStyle.wordWrap = true;
                _runComparisonStyle.fontSize = 21;
            }

            _runComparisonStyle.normal.textColor = _runComparisonColor;
            return _runComparisonStyle;
        }

        private static void DrawVerification()
        {
            GUIStyle titleStyle = GetVerificationTitleStyle();
            GUIStyle detailStyle = GetVerificationDetailStyle();

            float width = 920f;
            float left = (Screen.width - width) * 0.5f;
            float top = 18f;
            float titleHeight = titleStyle.CalcHeight(new GUIContent(_verificationTitleText), width);
            GUI.Label(new Rect(left, top, width, titleHeight + 4f), _verificationTitleText, titleStyle);

            if (string.IsNullOrEmpty(_verificationDetailText))
            {
                return;
            }

            float detailTop = top + titleHeight + 2f;
            float detailHeight = detailStyle.CalcHeight(new GUIContent(_verificationDetailText), width);
            GUI.Label(new Rect(left, detailTop, width, detailHeight + 4f), _verificationDetailText, detailStyle);
        }

        private static GUIStyle GetVerificationTitleStyle()
        {
            if (_verificationTitleStyle == null)
            {
                _verificationTitleStyle = CreateBaseStyle(TextAnchor.UpperCenter);
                _verificationTitleStyle.wordWrap = true;
                _verificationTitleStyle.fontSize = 28;
            }

            _verificationTitleStyle.normal.textColor = _verificationTitleColor;
            return _verificationTitleStyle;
        }

        private static GUIStyle GetVerificationDetailStyle()
        {
            if (_verificationDetailStyle == null)
            {
                _verificationDetailStyle = CreateBaseStyle(TextAnchor.UpperCenter);
                _verificationDetailStyle.wordWrap = true;
                _verificationDetailStyle.fontSize = 22;
            }

            _verificationDetailStyle.normal.textColor = _verificationDetailColor;
            return _verificationDetailStyle;
        }

        private static void DrawCenterMessage()
        {
            GUIStyle style = GetCenterMessageStyle();
            float width = 840f;
            float height = style.CalcHeight(new GUIContent(_centerMessageText), width);
            float left = (Screen.width - width) * 0.5f;
            float top = (Screen.height - height) * 0.5f - 40f;
            GUI.Label(new Rect(left, top, width, height + 6f), _centerMessageText, style);
        }

        private static void DrawTopCenterMessage()
        {
            GUIStyle titleStyle = GetTopCenterTitleStyle();
            GUIStyle detailStyle = GetTopCenterDetailStyle();

            float width = 920f;
            float left = (Screen.width - width) * 0.5f;
            float top = 156f;
            float titleHeight = titleStyle.CalcHeight(new GUIContent(_topCenterTitleText), width);
            GUI.Label(new Rect(left, top, width, titleHeight + 4f), _topCenterTitleText, titleStyle);

            if (string.IsNullOrEmpty(_topCenterDetailText))
            {
                return;
            }

            float detailTop = top + titleHeight + 2f;
            float detailHeight = detailStyle.CalcHeight(new GUIContent(_topCenterDetailText), width);
            GUI.Label(new Rect(left, detailTop, width, detailHeight + 4f), _topCenterDetailText, detailStyle);
        }

        private static GUIStyle GetCenterMessageStyle()
        {
            if (_centerMessageStyle == null)
            {
                _centerMessageStyle = CreateBaseStyle(TextAnchor.MiddleCenter);
                _centerMessageStyle.wordWrap = true;
                _centerMessageStyle.fontSize = 44;
            }

            _centerMessageStyle.normal.textColor = _centerMessageColor;
            return _centerMessageStyle;
        }

        private static GUIStyle GetTopCenterTitleStyle()
        {
            if (_topCenterTitleStyle == null)
            {
                _topCenterTitleStyle = CreateBaseStyle(TextAnchor.UpperCenter);
                _topCenterTitleStyle.wordWrap = true;
                _topCenterTitleStyle.fontSize = 30;
            }

            _topCenterTitleStyle.normal.textColor = _topCenterTitleColor;
            return _topCenterTitleStyle;
        }

        private static GUIStyle GetTopCenterDetailStyle()
        {
            if (_topCenterDetailStyle == null)
            {
                _topCenterDetailStyle = CreateBaseStyle(TextAnchor.UpperCenter);
                _topCenterDetailStyle.wordWrap = true;
                _topCenterDetailStyle.fontSize = 22;
            }

            _topCenterDetailStyle.normal.textColor = _topCenterDetailColor;
            return _topCenterDetailStyle;
        }

        private static GUIStyle CreateBaseStyle(TextAnchor alignment)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = alignment;
            style.fontStyle = _fontStyle;
            style.richText = false;
            style.clipping = TextClipping.Overflow;
            style.wordWrap = false;

            if (_font != null)
            {
                style.font = _font;
            }

            return style;
        }

        internal static void Clear()
        {
            _categoryText = string.Empty;
            _timerVisible = _runStatusVisible = _verificationVisible = _topCenterVisible = _centerMessageVisible = false;
        }

        internal static void Unload()
        {
            Clear();
            if (_behaviour != null) Object.DestroyImmediate(_behaviour.gameObject);
            _behaviour = null;
            _font = null;
            _nextTemplateRefreshAt = 0f;
        }

        private sealed class OverlayBehaviour : MonoBehaviour
        {
            private void Awake()
            {
                Object.DontDestroyOnLoad(base.gameObject);
                if (GetComponent<SceneCleanerPreserve>() == null)
                    base.gameObject.AddComponent<SceneCleanerPreserve>();
            }

            private void OnGUI()
            {
                if (Event.current.type != EventType.Repaint) return;
                int previousDepth = GUI.depth;
                bool previousEnabled = GUI.enabled;
                GUI.depth = -10000;
                Color previous = GUI.color;
                Color previousContent = GUI.contentColor;
                Matrix4x4 previousMatrix = GUI.matrix;
                try
                {
                    GUI.enabled = true;
                    GUI.color = Color.white;
                    GUI.contentColor = Color.white;
                    GUI.matrix = Matrix4x4.identity;
                    Draw();
                }
                finally
                {
                    GUI.depth = previousDepth;
                    GUI.enabled = previousEnabled;
                    GUI.color = previous;
                    GUI.contentColor = previousContent;
                    GUI.matrix = previousMatrix;
                }
            }
        }
    }
}
