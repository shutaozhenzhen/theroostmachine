namespace Roost.Piebald
{
    using System;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public class InputFieldWidget : LayoutItemWidget<InputFieldWidget>, ITextWidget<InputFieldWidget>
    {
        private static readonly Color baseColor = new Color(0.2408f, 0.357f, 0.4151f, 1);
        private static readonly Color selectionColor = new Color(0.6588f, 0.8078f, 1, 0.7529f);
        private static readonly char asteriskChar = '*';
        private static readonly float caretBlinkRate = 0.85f;
        private static readonly Color caretColor = new Color(0.7098f, 0.3059f, 0.9882f, 1);

        public InputFieldWidget(string key)
            : this(new GameObject(key))
        {
        }

        public InputFieldWidget(GameObject gameObject)
            : base(gameObject)
        {
            var image = gameObject.GetOrAddComponent<Image>();
            image.sprite = null;
            image.color = baseColor;

            this.InputField = gameObject.GetOrAddComponent<TMP_InputField>();
            this.InputField.asteriskChar = asteriskChar;
            this.InputField.caretBlinkRate = caretBlinkRate;
            this.InputField.caretColor = caretColor;
            this.InputField.contentType = TMP_InputField.ContentType.Standard;
            this.InputField.inputType = TMP_InputField.InputType.Standard;
            this.InputField.lineType = TMP_InputField.LineType.SingleLine;
            this.InputField.pointSize = 14;
            this.InputField.selectionColor = selectionColor;

            var textObject = new GameObject("Text");
            this.InputField.textComponent = this.TextMesh = textObject.AddComponent<TextMeshProUGUI>();
            this.InputField.textComponent.horizontalAlignment = HorizontalAlignmentOptions.Left;
            this.InputField.textComponent.verticalAlignment = VerticalAlignmentOptions.Middle;
            var textRt = textObject.GetComponent<RectTransform>();
            textRt.SetParent(this.InputField.transform, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            // Apparently the caret is created automatically on OnEnable, but we need to toggle it to re-trigger once
            // everything is ready.
            this.InputField.enabled = false;
            this.InputField.enabled = true;

            this.InputField.onSelect.AddListener((x) => this.Select?.Invoke(this, EventArgs.Empty));
            this.InputField.onDeselect.AddListener((x) => this.Deselect?.Invoke(this, EventArgs.Empty));

            this.InputField.onEndEdit.AddListener((x) => this.EndEdit?.Invoke(this, new InputFieldTextEventArgs(x)));
            this.InputField.onValueChanged.AddListener((x) => this.Change?.Invoke(this, new InputFieldTextEventArgs(x)));
            this.InputField.onSubmit.AddListener((x) => this.Submit?.Invoke(this, new InputFieldTextEventArgs(x)));
        }

        public event EventHandler Select;
        public event EventHandler Deselect;

        public event EventHandler<InputFieldTextEventArgs> EndEdit;
        public event EventHandler<InputFieldTextEventArgs> Change;
        public event EventHandler<InputFieldTextEventArgs> Submit;

        public TMP_InputField InputField { get; }

        public TextMeshProUGUI TextMesh { get; }

        public string Font
        {
            get => this.TextMesh.font?.name;
            set => this.SetFont(value);
        }

        public string FontMaterial
        {
            get => this.TextMesh.fontMaterial?.name;
            set => this.SetFontMaterial(value);
        }

        public float FontSize
        {
            get => this.TextMesh.fontSize;
            set => this.SetFontSize(value);
        }

        public FontStyles FontStyle
        {
            get => this.TextMesh.fontStyle;
            set => this.SetFontStyle(value);
        }

        public FontWeight FontWeight
        {
            get => this.TextMesh.fontWeight;
            set => this.SetFontWeight(value);
        }

        public HorizontalAlignmentOptions HorizontalAlignment
        {
            get => this.TextMesh.horizontalAlignment;
            set => this.SetHorizontalAlignment(value);
        }

        public VerticalAlignmentOptions VerticalAlignment
        {
            get => this.TextMesh.verticalAlignment;
            set => this.SetVerticalAlignment(value);
        }

        public TextAlignmentOptions TextAlignment
        {
            get => this.TextMesh.alignment;
            set => this.SetTextAlignment(value);
        }

        public bool WordWrapping
        {
            get => this.TextMesh.enableWordWrapping;
            set => this.SetWordWrapping(value);
        }

        public TextOverflowModes OverflowMode
        {
            get => this.TextMesh.overflowMode;
            set => this.SetOverflowMode(value);
        }

        public float MaxFontSize
        {
            get => this.TextMesh.fontSizeMax;
            set => this.SetMaxFontSize(value);
        }

        public float MinFontSize
        {
            get => this.TextMesh.fontSizeMin;
            set => this.SetMinFontSize(value);
        }

        public string Text
        {
            get => this.InputField.text;
            set => this.SetText(value);
        }

        public Color Color
        {
            get => this.TextMesh.color;
            set => this.SetColor(value);
        }

        public InputFieldWidget SetFontMaterial(string resourceName)
        {
            this.TextMesh.fontMaterial = ResourceHack.FindMaterial(resourceName);
            return this;
        }

        public InputFieldWidget SetFont(string resourceName)
        {
            this.TextMesh.font = ResourceHack.FindFont(resourceName);
            return this;
        }

        public InputFieldWidget SetFontStyle(FontStyles style)
        {
            this.TextMesh.fontStyle = style;
            return this;
        }

        public InputFieldWidget SetFontWeight(FontWeight weight)
        {
            this.TextMesh.fontWeight = weight;
            return this;
        }

        public InputFieldWidget SetTextAlignment(TextAlignmentOptions alignment)
        {
            this.TextMesh.alignment = alignment;
            return this;
        }

        public InputFieldWidget SetHorizontalAlignment(HorizontalAlignmentOptions alignment)
        {
            this.TextMesh.horizontalAlignment = alignment;
            return this;
        }

        public InputFieldWidget SetVerticalAlignment(VerticalAlignmentOptions alignment)
        {
            this.TextMesh.verticalAlignment = alignment;
            return this;
        }

        public InputFieldWidget SetWordWrapping(bool enabled)
        {
            this.TextMesh.enableWordWrapping = enabled;
            return this;
        }

        public InputFieldWidget SetMinFontSize(float size)
        {
            this.TextMesh.fontSizeMin = size;
            this.TextMesh.enableAutoSizing = this.TextMesh.fontSizeMin != this.TextMesh.fontSizeMax;
            return this;
        }

        public InputFieldWidget SetMaxFontSize(float size)
        {
            this.TextMesh.fontSizeMax = size;
            this.TextMesh.enableAutoSizing = this.TextMesh.fontSizeMin != this.TextMesh.fontSizeMax;
            return this;
        }

        public InputFieldWidget SetFontSize(float size)
        {
            this.TextMesh.fontSize = size;
            this.TextMesh.enableAutoSizing = false;
            return this;
        }

        public InputFieldWidget SetOverflowMode(TextOverflowModes mode)
        {
            this.TextMesh.overflowMode = mode;
            return this;
        }

        public InputFieldWidget SetText(string value)
        {
            this.TextMesh.text = value;
            return this;
        }

        public InputFieldWidget SetColor(Color color)
        {
            this.TextMesh.color = color;
            return this;
        }

        public InputFieldWidget OnSelect(Action action)
        {
            this.Select += (x, y) => action();
            return this;
        }

        public InputFieldWidget OnDeselect(Action action)
        {
            this.Deselect += (x, y) => action();
            return this;
        }

        public InputFieldWidget OnEndEdit(Action<string> action)
        {
            this.EndEdit += (x, y) => action(y.Text);
            return this;
        }

        public InputFieldWidget OnChange(Action<string> action)
        {
            this.Change += (x, y) => action(y.Text);
            return this;
        }

        public InputFieldWidget OnSubmit(Action<string> action)
        {
            this.Submit += (x, y) => action(y.Text);
            return this;
        }

        public class InputFieldTextEventArgs : EventArgs
        {
            public InputFieldTextEventArgs(string text)
            {
                this.Text = text;
            }

            public string Text { get; }
        }
    }
}
