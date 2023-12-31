namespace Roost.Piebald
{
    using System;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// A UI Widget implementing an icon button.
    /// </summary>
    public class IconButtonWidget : ImageWidget<IconButtonWidget>
    {
        public static readonly ColorBlock ColorBlock = new ColorBlock
        {
            // These colors have been extracted from the Group Cards button.
            normalColor = new Color(1, 1, 1, 1),
            highlightedColor = new Color(0.5765f, 0.8824f, 0.9373f, 1),
            pressedColor = new Color(0.1255f, 0.4471f, 0.4824f, 1),
            selectedColor = new Color(0.5765f, 0.8824f, 0.9373f, 1),
            disabledColor = new Color(0.502f, 0.502f, 0.502f, 1),
            colorMultiplier = 1,
            fadeDuration = 0.1f,
        };

        private GameObject background;
        private GameObject content;

        public IconButtonWidget(string key)
            : this(new GameObject(key))
        {
        }

        public IconButtonWidget(GameObject gameObject)
            : base(gameObject)
        {
            this.Button = this.GameObject.GetOrAddComponent<BetterButton>();
            this.Button.transition = Selectable.Transition.ColorTint;
            this.Button.colors = ColorBlock;

            this.SoundTrigger = this.GameObject.GetOrAddComponent<ButtonSoundTrigger>();

            this.content = new GameObject("Content");
            var contentRt = this.content.AddComponent<RectTransform>();
            this.content.transform.SetParent(this.GameObject.transform, false);
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            this.Button.image = this.Image;
        }

        public BetterButton Button { get; private set; }

        public ButtonSoundTrigger SoundTrigger { get; private set; }

        public override WidgetMountPoint MountPoint => new WidgetMountPoint(this.content.transform);

        public override Image Image => this.content.GetOrAddComponent<Image>();

        public bool Enabled
        {
            get => this.Button.interactable;
            set => this.Button.interactable = value;
        }

        public Sprite Background
        {
            get => this.background.GetComponent<Image>()?.sprite ?? null;
            set => this.SetBackground(value);
        }

        public string ClickSound
        {
            get => (string)this.SoundTrigger.GetType().GetFieldInvariant("soundFXName").GetValue(this.SoundTrigger);
            set => this.SetClickSound(value);
        }

        public new Color Color
        {
            get => this.Button.colors.normalColor;
            set => this.SetColor(value);
        }

        public Color HighlightedColor
        {
            get => this.Button.colors.highlightedColor;
            set => this.SetHighlightedColor(value);
        }

        public Color PressedColor
        {
            get => this.Button.colors.pressedColor;
            set => this.SetPressedColor(value);
        }

        public Color SelectedColor
        {
            get => this.Button.colors.selectedColor;
            set => this.SetSelectedColor(value);
        }

        public Color DisabledColor
        {
            get => this.Button.colors.disabledColor;
            set => this.SetDisabledColor(value);
        }

        public IconButtonWidget Enable()
        {
            return this.SetEnabled(true);
        }

        public IconButtonWidget Disable()
        {
            return this.SetEnabled(false);
        }

        public IconButtonWidget SetEnabled(bool enabled)
        {
            this.Button.interactable = enabled;
            return this;
        }

        public IconButtonWidget SetBackground(Sprite sprite)
        {
            // HACK: Our colors are not properly set if we are disabled before opting into backgrounds.
            var prevInteractable = this.Button.interactable;
            this.Button.interactable = true;
            try
            {
                this.background = new GameObject("Background");

                var backgroundRt = this.background.AddComponent<RectTransform>();
                backgroundRt.SetParent(this.GameObject.transform, false);
                backgroundRt.SetAsFirstSibling();
                backgroundRt.anchorMin = Vector2.zero;
                backgroundRt.anchorMax = Vector2.one;
                backgroundRt.offsetMin = Vector2.zero;
                backgroundRt.offsetMax = Vector2.zero;

                var backgroundImage = this.background.AddComponent<Image>();
                backgroundImage.sprite = sprite;
                backgroundImage.color = this.Image.color;

                var colorCopy = this.content.AddComponent<CopyGraphicCanvasColor>();
                colorCopy.CopyFrom = backgroundImage;
                colorCopy.CopyTo = this.Image;

                this.Button.image = backgroundImage;
            }
            finally
            {
                this.Button.interactable = prevInteractable;
            }

            return this;
        }

        public IconButtonWidget SetBackground()
        {
            return this.SetBackground(ResourcesManager.GetSpriteForUI("button_blank"));
        }

        public IconButtonWidget SetClickSound(string soundEffect)
        {
            this.SoundTrigger.GetType().GetFieldInvariant("soundFXName").SetValue(this.SoundTrigger, soundEffect);
            return this as IconButtonWidget;
        }

        public new IconButtonWidget SetColor(Color color)
        {
            var newValue = this.Button.colors.Clone();
            newValue.normalColor = color;
            this.Button.colors = newValue;
            return this as IconButtonWidget;
        }

        public IconButtonWidget SetHighlightedColor(Color color)
        {
            var newValue = this.Button.colors.Clone();
            newValue.highlightedColor = color;
            this.Button.colors = newValue;
            return this as IconButtonWidget;
        }

        public IconButtonWidget SetPressedColor(Color color)
        {
            var newValue = this.Button.colors.Clone();
            newValue.pressedColor = color;
            this.Button.colors = newValue;
            return this as IconButtonWidget;
        }

        public IconButtonWidget SetSelectedColor(Color color)
        {
            var newValue = this.Button.colors.Clone();
            newValue.selectedColor = color;
            this.Button.colors = newValue;
            return this as IconButtonWidget;
        }

        public IconButtonWidget SetDisabledColor(Color color)
        {
            var newValue = this.Button.colors.Clone();
            newValue.disabledColor = color;
            this.Button.colors = newValue;
            return this as IconButtonWidget;
        }

        public IconButtonWidget OnClick(UnityEngine.Events.UnityAction action)
        {
            this.Button.onClick.AddListener(action);
            return this as IconButtonWidget;
        }

        public IconButtonWidget OnRightClick(UnityEngine.Events.UnityAction action)
        {
            this.Button.onRightClick.AddListener(action);
            return this as IconButtonWidget;
        }
    }
}
