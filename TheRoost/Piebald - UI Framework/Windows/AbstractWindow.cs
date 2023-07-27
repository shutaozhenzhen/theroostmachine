namespace Roost.Piebald
{
    using System;
    using SecretHistories.UI;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public abstract class AbstractWindow : MonoBehaviour
    {
        private static readonly Color BgColorHeader = new Color(0.0314f, 0.0706f, 0.1059f, 0.902f);
        private static readonly Color BgColorBody = new Color(0.0627f, 0.1333f, 0.1922f, 0.902f);
        private static readonly Color BgColorFooter = new Color(0.0029f, 0.0246f, 0.0441f, 0.902f);

        private CanvasGroupFader canvasGroupFader;
        private WindowPositioner positioner;
        private TextWidget title;

        public AbstractWindow(bool includeShadow)
        {
            this.BuildWindowFrame(includeShadow);
        }

        public event EventHandler Opened;

        public event EventHandler Closed;

        public Vector3 Position
        {
            get => this.positioner.GetPosition();
            set => this.positioner.SetPosition(value);
        }

        public string Title
        {
            get => this.title.Text;
            set => this.title.Text = value;
        }

        public bool IsOpen => this.canvasGroupFader.IsFullyVisible();

        public bool IsVisible => this.canvasGroupFader.IsFullyVisible() || this.canvasGroupFader.IsAppearing();

        public bool IsClosed => this.canvasGroupFader.IsInvisible();

        protected virtual int DefaultWidth => 600;

        protected virtual int DefaultHeight => 400;

        protected WidgetMountPoint Icon { get; private set; }

        protected WidgetMountPoint Content { get; private set; }

        protected WidgetMountPoint Footer { get; private set; }

        public void Awake()
        {
            this.canvasGroupFader.HideImmediately();

            this.OnAwake();
        }

        public void Update()
        {
            this.OnUpdate();
        }

        public void OpenAt(Vector3 position)
        {
            if (this.IsVisible)
            {
                return;
            }

            SoundManager.PlaySfx("SituationWindowShow");
            this.canvasGroupFader.Show();
            this.positioner.Show(this.canvasGroupFader.durationTurnOn, position);
            this.OnOpen();

            this.Opened?.Invoke(this, EventArgs.Empty);
        }

        public void Close(bool immediately = false)
        {
            if (immediately)
            {
                this.canvasGroupFader.HideImmediately();
                this.OnClose();
                this.Closed?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (!this.IsVisible)
            {
                return;
            }

            SoundManager.PlaySfx("SituationWindowHide");
            this.canvasGroupFader.Hide();

            this.OnClose();
            this.Closed?.Invoke(this, EventArgs.Empty);
        }

        public void Retire()
        {
            Destroy(this.gameObject);
        }

        protected virtual void OnAwake()
        {
        }

        protected virtual void OnUpdate()
        {
        }

        protected virtual void OnOpen()
        {
        }

        protected virtual void OnClose()
        {
        }

        protected void Clear()
        {
            this.Content.Clear();
            this.Footer.Clear();
        }

        private void BuildWindowFrame(bool includeShadow)
        {
            var root = new LayoutItemWidget(this.gameObject)
                .SetPivot(0.5f, 0.5f)
                .SetLeft(.5f, 0)
                .SetTop(.5f, 0)
                .SetRight(.5f, 0)
                .SetBottom(.5f, 0)
                .SetMinWidth(this.DefaultWidth)
                .SetMinHeight(this.DefaultHeight);

            var canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
            this.positioner = this.gameObject.AddComponent<WindowPositioner>();
            typeof(WindowPositioner).GetFieldInvariant("canvasGroup").SetValue(this.positioner, canvasGroup);
            typeof(WindowPositioner).GetFieldInvariant("rectTrans").SetValue(this.positioner, root.RectTransform);

            // FIXME: Auto size window to content.
            // Should get ConstrainedLayoutElement working too, so we can specify a max size.
            // Note: Even though we have a fixed min size, and auto size does not work, this is still somehow load bearing for some reason.
            var fitter = this.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            this.canvasGroupFader = this.gameObject.AddComponent<CanvasGroupFader>();
            this.canvasGroupFader.durationTurnOn = 0.3f;
            this.canvasGroupFader.durationTurnOff = 0.3f;
            this.canvasGroupFader.Hide();

            WidgetMountPoint.On(this.gameObject, mountPoint =>
            {
                if (includeShadow)
                {
                    mountPoint.AddImage("Shadow")
                        .SetZOffset(100)
                        .SetOffset(-20)
                        .SetColor(new Color(0, 0, 0, 0.359f))
                        .SliceImage()
                        .SetSprite("internal:window_bg");
                }

                mountPoint.AddImage("BG_Top")
                    .SetLeft(0, 0)
                    .SetTop(1, 0)
                    .SetRight(1, 0)
                    .SetBottom(1, -50)
                    .SetSprite("internal:window_bg_top")
                    .SliceImage()
                    .SetColor(BgColorHeader);

                mountPoint.AddSizedItem("TextContainer")
                    .SetLeft(0, 57.5f)
                    .SetTop(1, 0)
                    .SetRight(1, -57.5f)
                    .SetBottom(1, -47)
                    .AddContent(mountPoint =>
                    {
                        this.title = mountPoint.AddText("TitleText")
                            .SetPreferredHeight(25)
                            .SetMinFontSize(12)
                            .SetMaxFontSize(30)
                            .SetBottom(0, 7)
                            .SetOverflowMode(TextOverflowModes.Ellipsis)
                            .SetTextAlignment(TextAlignmentOptions.BottomLeft)
                            .SetVerticalAlignment(VerticalAlignmentOptions.Bottom)
                            .SetFontStyle(FontStyles.Bold);

                        mountPoint.AddImage("TitleUnderline")
                            .SetLeft(0, 0)
                            .SetRight(1, 0)
                            .SetTop(0, 6)
                            .SetBottom(0, 4)
                            .SetSprite((Sprite)null)
                            .SetColor(new Color(0.5804f, 0.8863f, 0.9373f, 1));
                    });

                mountPoint.AddImage("BG_Body")
                    .SetLeft(0, 0)
                    .SetTop(1, -50)
                    .SetRight(1, 0)
                    .SetBottom(0, 50)
                    .SetSprite("internal:window_bg_middle")
                    .SliceImage()
                    .SetColor(BgColorBody);

                this.Content = mountPoint.AddLayoutItem("Content")
                    .SetLeft(0, 0)
                    .SetTop(1, -50)
                    .SetRight(1, 0)
                    .SetBottom(0, 50)
                    .SetPivot(0.5f, 0.5f);

                var iconSize = 65;
                var iconOffsetX = -10;
                var iconOffsetY = 10;
                this.Icon = mountPoint.AddLayoutItem("IconContainer")
                    .SetAnchorAndSize(
                        new Vector2(0, 1),
                        new Vector2(iconOffsetX + iconSize / 2, iconOffsetY - iconSize / 2),
                        new Vector2(iconSize, iconSize));

                mountPoint.AddLayoutItem("Footer")
                    .SetLeft(0, 0)
                    .SetTop(0, 50)
                    .SetRight(1, 0)
                    .SetBottom(0, 0)
                    .AddContent(mountPoint =>
                    {
                        mountPoint.AddImage("BG_Footer")
                            .SetSprite("internal:window_bg_bottom")
                            .SliceImage()
                            .SetColor(BgColorFooter);

                        this.Footer = mountPoint.AddLayoutItem("FooterContent");
                    });

                mountPoint.AddIconButton("CloseButton")
                    .SetAnchorAndSize(
                        Vector2.one,
                        new Vector2(-25, -25),
                        new Vector2(24, 24))
                    .SetSprite("internal:icon_close")
                    .CenterImage()
                    .SetColor(new Color(0.3804f, 0.7294f, 0.7922f, 1))
                    .SetHighlightedColor(Color.white)
                    .SetPressedColor(new Color(0.2671f, 0.6328f, 0.6985f, 1))
                    .SetSelectedColor(Color.white)
                    .SetDisabledColor(new Color(0.5368f, 0.5368f, 0.5368f, 0.502f))
                    .SetClickSound("UIButtonClose")
                    .OnClick(() => this.Close());
            });
        }
    }
}
