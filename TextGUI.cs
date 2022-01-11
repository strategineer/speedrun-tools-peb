using UnityEngine;

namespace com.strategineer.PEBSpeedrunTools
{
    class TextGUI
    {
        const int SCREEN_OFFSET = 10;
        const int SHADOW_OFFSET = 2;

        private string _text;
        private GUIStyle _style = new GUIStyle();
        private GUIStyle _shadowStyle = new GUIStyle();
        private Rect _rect;
        private Rect _shadowRect;

        private bool _shouldShow;

        private TextAnchor _anchor;
        public TextGUI() : this(TextAnchor.UpperLeft, Color.yellow, "") { }
        public TextGUI(TextAnchor anchor, Color color, string text)
        {
            _shouldShow = true;
            _text = text;
            _anchor = anchor;

            int w = Screen.width, h = Screen.height;
            _rect = _shadowRect = new Rect(SCREEN_OFFSET, SCREEN_OFFSET, w - SCREEN_OFFSET * 2, h - SCREEN_OFFSET * 2);
            _shadowRect.x += SHADOW_OFFSET;
            _shadowRect.y += SHADOW_OFFSET;

            _style.fontSize = _shadowStyle.fontSize = h / 40;
            SetAnchor(anchor);
            SetColor(color);
            SetShadowColor(Color.black);
        }

        public void SetColor(Color color)
        {
            _style.normal.textColor = color;
        }
        public void SetShadowColor(Color color)
        {
            _shadowStyle.normal.textColor = color;
        }

        public void SetAnchor(TextAnchor anchor)
        {
            _style.alignment = _shadowStyle.alignment = anchor;
        }

        public void SetText(string text)
        {
            this._text = text;
        }

        public void SetActive(bool shouldShow)
        {
            _shouldShow = shouldShow;
        }

        public void Draw()
        {
            if (_shouldShow)
            {
                GUI.Label(_shadowRect, _text, _shadowStyle);
                GUI.Label(_rect, _text, _style);
            }
        }
    }
}
