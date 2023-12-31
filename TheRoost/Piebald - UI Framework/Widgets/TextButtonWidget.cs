namespace Roost.Piebald
{
    using SecretHistories.UI;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// A UI widget that displays a button with text.
    /// </summary>
    public class TextButtonWidget : LayoutItemWidget<TextButtonWidget>, ITextWidget<TextButtonWidget>
    {
        // Color taken from the Verb window start button font.
        private static readonly Color DefaultFontColor = new Color(0.2392f, 0.1961f, 0.0667f, 1);

        public TextButtonWidget(string key)
            : this(new GameObject(key))
        {
        }

        public TextButtonWidget(GameObject gameObject)
            : base(gameObject)
        {
            this.SetPivot(.5f, .5f);

            // Would be nice if the button dynamically sized, but the image
            // is not a splice.
            this.SetMinWidth(120);
            this.SetMinHeight(45);

            this.SetPreferredWidth(120);
            this.SetPreferredHeight(45);

            this.Button = this.GameObject.GetOrAddComponent<BetterButton>();
            this.Button.transition = Selectable.Transition.ColorTint;
            this.Button.colors = IconButtonWidget.ColorBlock;

            this.SoundTrigger = this.GameObject.gameObject.GetOrAddComponent<ButtonSoundTrigger>();

            TextWidget textWidget = null;
            this.AddContent(mountPoint =>
            {
                var background = mountPoint.AddImage("Background")
                    .SetPivot(.5f, .5f)
                    .SetLeft(0, 0)
                    .SetTop(1, 0)
                    .SetRight(1, 0)
                    .SetBottom(0, 0)
                    .SetSprite("internal:button")
                    .SetIgnoreLayout();
                this.Button.image = background.Image;

                textWidget = mountPoint.AddText("Text")
                   .SetPivot(.5f, .5f)
                   .SetLeft(0, 9)
                   .SetRight(1, -9)
                   .SetTop(1, -4)
                   .SetBottom(0, 12)
                   .SetColor(DefaultFontColor)
                   .SetHorizontalAlignment(HorizontalAlignmentOptions.Center)
                   .SetVerticalAlignment(VerticalAlignmentOptions.Middle)
                   .SetFontStyle(FontStyles.Bold)
                   .SetMinFontSize(12)
                   .SetMaxFontSize(20);
            });

            this.TextWidget = textWidget;
        }

        public TextMeshProUGUI TextMesh => this.TextWidget.TextMesh;

        public BetterButton Button { get; }

        public ButtonSoundTrigger SoundTrigger { get; }

        public TextWidget TextWidget { get; }

        public bool Enabled
        {
            get
            {
                return this.Button.interactable;
            }

            set
            {
                this.Button.interactable = value;
            }
        }

        public string Text
        {
            get
            {
                return this.TextWidget.Text;
            }

            set
            {
                this.TextWidget.Text = value;
            }
        }

        public Color Color
        {
            get
            {
                return this.TextWidget.Color;
            }

            set
            {
                this.TextWidget.Color = value;
            }
        }

        public string Font
        {
            get
            {
                return this.TextWidget.Font;
            }

            set
            {
                this.TextWidget.Font = value;
            }
        }

        public string FontMaterial
        {
            get
            {
                return this.TextWidget.FontMaterial;
            }

            set
            {
                this.TextWidget.FontMaterial = value;
            }
        }

        public float FontSize
        {
            get
            {
                return this.TextWidget.FontSize;
            }

            set
            {
                this.TextWidget.FontSize = value;
            }
        }

        public FontStyles FontStyle
        {
            get
            {
                return this.TextWidget.FontStyle;
            }

            set
            {
                this.TextWidget.FontStyle = value;
            }
        }

        public FontWeight FontWeight
        {
            get
            {
                return this.TextWidget.FontWeight;
            }

            set
            {
                this.TextWidget.FontWeight = value;
            }
        }

        public HorizontalAlignmentOptions HorizontalAlignment
        {
            get
            {
                return this.TextWidget.HorizontalAlignment;
            }

            set
            {
                this.TextWidget.HorizontalAlignment = value;
            }
        }

        public VerticalAlignmentOptions VerticalAlignment
        {
            get
            {
                return this.TextWidget.VerticalAlignment;
            }

            set
            {
                this.TextWidget.VerticalAlignment = value;
            }
        }

        public TextAlignmentOptions TextAlignment
        {
            get
            {
                return this.TextWidget.TextAlignment;
            }

            set
            {
                this.TextWidget.TextAlignment = value;
            }
        }

        public bool WordWrapping
        {
            get
            {
                return this.TextWidget.WordWrapping;
            }

            set
            {
                this.TextWidget.WordWrapping = value;
            }
        }

        public TextOverflowModes OverflowMode
        {
            get
            {
                return this.TextWidget.OverflowMode;
            }

            set
            {
                this.TextWidget.OverflowMode = value;
            }
        }

        public float MaxFontSize
        {
            get
            {
                return this.TextWidget.MaxFontSize;
            }

            set
            {
                this.TextWidget.MaxFontSize = value;
            }
        }

        public float MinFontSize
        {
            get
            {
                return this.TextWidget.MinFontSize;
            }

            set
            {
                this.TextWidget.MinFontSize = value;
            }
        }

        public TextButtonWidget SetEnabled(bool enabled)
        {
            this.Button.interactable = enabled;
            return this;
        }

        public TextButtonWidget Enable()
        {
            return this.SetEnabled(true);
        }

        public TextButtonWidget Disable()
        {
            return this.SetEnabled(false);
        }

        public TextButtonWidget SetText(string value)
        {
            this.TextWidget.SetText(value);
            return this as TextButtonWidget;
        }

        public TextButtonWidget SetUIText(string locKey)
        {
            this.TextWidget.SetUIText(locKey);
            return this as TextButtonWidget;
        }

        public TextButtonWidget SetColor(Color color)
        {
            this.TextWidget.SetColor(color);
            return this as TextButtonWidget;
        }

        public TextButtonWidget SetClickSound(string soundEffect)
        {
            this.SoundTrigger.GetType().GetFieldInvariant("soundFXName").SetValue(this.SoundTrigger, soundEffect);
            return this as TextButtonWidget;
        }

        public TextButtonWidget SetFont(string resourceName)
        {
            this.TextWidget.SetFont(resourceName);
            return this;
        }

        public TextButtonWidget SetFontMaterial(string resourceName)
        {
            this.TextWidget.SetFontMaterial(resourceName);
            return this;
        }

        public TextButtonWidget SetFontSize(float size)
        {
            this.TextWidget.SetFontSize(size);
            return this;
        }

        public TextButtonWidget SetFontStyle(FontStyles style)
        {
            this.TextWidget.SetFontStyle(style);
            return this;
        }

        public TextButtonWidget SetFontWeight(FontWeight weight)
        {
            this.TextWidget.SetFontWeight(weight);
            return this;
        }

        public TextButtonWidget SetHorizontalAlignment(HorizontalAlignmentOptions alignment)
        {
            this.TextWidget.SetHorizontalAlignment(alignment);
            return this;
        }

        public TextButtonWidget SetVerticalAlignment(VerticalAlignmentOptions alignment)
        {
            this.TextWidget.SetVerticalAlignment(alignment);
            return this;
        }

        public TextButtonWidget SetTextAlignment(TextAlignmentOptions alignment)
        {
            this.TextWidget.SetTextAlignment(alignment);
            return this;
        }

        public TextButtonWidget SetMaxFontSize(float size)
        {
            this.TextWidget.SetMaxFontSize(size);
            return this;
        }

        public TextButtonWidget SetMinFontSize(float size)
        {
            this.TextWidget.SetMinFontSize(size);
            return this;
        }

        public TextButtonWidget OnClick(UnityEngine.Events.UnityAction action)
        {
            this.Button.onClick.AddListener(action);
            return this as TextButtonWidget;
        }

        public TextButtonWidget OnRightClick(UnityEngine.Events.UnityAction action)
        {
            this.Button.onRightClick.AddListener(action);
            return this as TextButtonWidget;
        }

        public TextButtonWidget SetWordWrapping(bool enabled)
        {
            this.TextWidget.SetWordWrapping(enabled);
            return this;
        }

        public TextButtonWidget SetOverflowMode(TextOverflowModes mode)
        {
            this.TextWidget.SetOverflowMode(mode);
            return this;
        }
    }
}
