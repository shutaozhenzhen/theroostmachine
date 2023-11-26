namespace Roost.Piebald
{
    using Antlr.Runtime;
    using SecretHistories.UI;
    using System;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;


    public class CollapsibleDrawerWidget : LayoutItemWidget<CollapsibleDrawerWidget>
    {
        private static readonly Color DefaultFontColor = new Color(0.5765f, 0.8824f, 0.9373f, 1);
        private static string EXPAND_BUTTON_SPRITE = "ui:expand_button";
        private static string COLLAPSE_BUTTON_SPRITE = "ui:collapse_button";

        // Header
        public HorizontalLayoutGroupWidget Header { get; set; }
        public TextMeshProUGUI TextMesh => this.HeaderText.TextMesh;
        public ImageWidget HeaderBackground { get; set; }
        public TextWidget HeaderText { get; set; }
        public IconButtonWidget ExpandButton { get; set; }
        public ButtonSoundTrigger SoundTrigger { get; }

        // Drawer
        public WidgetMountPoint Drawer { get; set; }
        public VerticalLayoutGroupWidget DrawerContent { get; set; }

        public event EventHandler Opened;
        public event EventHandler Closed;

        private bool IsOpen = true;

        public CollapsibleDrawerWidget(string key)
            : this(new GameObject(key))
        {
        }

        public CollapsibleDrawerWidget(GameObject gameObject, bool openDrawer = true)
            : base(gameObject)
        {
            SetFitContentHeight();
            SetExpandWidth();
            this.VerticalFit = ContentSizeFitter.FitMode.MinSize;
            AddContent(mountPoint =>
            {
                mountPoint.AddVerticalLayoutGroup($"Collapsible_row")
                .SetExpandWidth()
                .SetFitContentHeight()
                .AddContent(mountPoint =>
                {
                    // header row
                    Header = mountPoint.AddHorizontalLayoutGroup($"Collapsible_row_header")
                    .SetExpandWidth()
                    .SetMinHeight(40)
                    .WithPointerSounds()
                    .SetPadding(5)
                    .AddContent(mountPoint =>
                    {
                        HeaderBackground = mountPoint.AddImage("Background")
                            .SetIgnoreLayout()
                            .SetLeft(0, 0)
                            .SetRight(1, 0)
                            .SetTop(1, 0)
                            .SetBottom(0, 0)
                            .SetColor(new Color(0.2f, 0.8f, 0.8f, 0.1f))
                            .SliceImage()
                            .SetSprite("internal:window_bg");

                        HeaderText = mountPoint.AddText("Label")
                            .SetExpandWidth()
                            .SetExpandHeight()
                            .SetFontSize(20)
                            .SetColor(DefaultFontColor)
                            .SetOverflowMode(TextOverflowModes.Ellipsis)
                            .SetVerticalAlignment(VerticalAlignmentOptions.Middle);

                        ExpandButton = mountPoint.AddIconButton("ExpandButton")
                            .SetAnchorAndSize(new Vector2(1, 0), new Vector2(40, 40))
                            .SetPreferredWidth(40)
                            .SetPreferredHeight(40)
                            .SetSprite(IsOpen ? COLLAPSE_BUTTON_SPRITE : EXPAND_BUTTON_SPRITE)
                            .CenterImage()
                            .OnPointerClick(evt => ToggleDrawer());

                    });

                    // The drawer itself
                    Drawer = mountPoint;
                    DrawerContent = mountPoint.AddVerticalLayoutGroup()
                    .SetPadding(20, 0, 0, 0)
                    .SetExpandWidth()
                    .SetFitContentHeight();

                    if(!openDrawer)
                    {
                        Close();
                    }
                });
            });
            CalculateSize();
        }

        private void ToggleDrawer()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void CalculateSize()
        {
            float totalHeight = 40f + 0f;

            // Loop through all children of the VerticalLayoutGroup
            for (int i = 0; i < DrawerContent.GameObject.transform.childCount; i++)
            {
                RectTransform child = DrawerContent.GameObject.transform.GetChild(i) as RectTransform;
                if (child != null)
                {
                    totalHeight += LayoutUtility.GetPreferredHeight(child);
                    //totalHeight += layoutGroup.spacing; // Add spacing between children
                }
            }

            // Adjust the size of the VerticalLayoutGroup to fit all children
            SetMinHeight(totalHeight);
        }

        public void Close()
        {
            if (!IsOpen) return;
            DrawerContent.SetActive(false);
            ExpandButton.SetSprite(EXPAND_BUTTON_SPRITE);
            IsOpen = false;
            SetMinHeight(40);
            this.Closed?.Invoke(this, EventArgs.Empty);
        }

        public void Open()
        {
            if (IsOpen) return;
            DrawerContent.SetActive(true);
            ExpandButton.SetSprite(COLLAPSE_BUTTON_SPRITE);
            IsOpen = true;
            CalculateSize();
            this.Opened?.Invoke(this, EventArgs.Empty);
        }

        public void AddContentToList(Action<WidgetMountPoint> factory)
        {
            DrawerContent.AddContent(factory);
            CalculateSize();
        }
    }
}
