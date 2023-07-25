namespace Roost.Piebald
{
    public static class WidgetMountPointExtensions
    {
        /// <summary>
        /// Adds a new rect transform widget to the mount point.
        /// </summary>
        /// <param name="mountPoint">The mount point to add the widget to.</param>
        /// <param name="key">The key to use for the new widget's game object.</param>
        public static RectTransformWidget AddRectTransform(this WidgetMountPoint mountPoint, string key = "RectTransform")
        {
            var widget = new RectTransformWidget(key);
            mountPoint.AddWidget(widget);
            return widget;
        }

        /// <summary>
        /// Adds a new sizing layout widget to the mount point.
        /// </summary>
        /// <param name="mountPoint">The mount point to add the widget to.</param>
        /// <param name="key">The key to use for the new widget's game object.</param>
        public static SizingLayoutWidget AddSizingLayout(this WidgetMountPoint mountPoint, string key = "SizingLayout")
        {
            var widget = new SizingLayoutWidget(key);
            mountPoint.AddWidget(widget);
            return widget;
        }

        /// <summary>
        /// Adds a new image widget to the mount point.
        /// </summary>
        /// <param name="mountPoint">The mount point to add the widget to.</param>
        /// <param name="key">The key to use for the new widget's game object.</param>
        public static ImageWidget AddImage(this WidgetMountPoint mountPoint, string key = "Image")
        {
            var widget = new ImageWidget(key);
            mountPoint.AddWidget(widget);
            return widget;
        }

        /// <summary>
        /// Adds a new text widget to the mount point.
        /// </summary>
        /// <param name="mountPoint">The mount point to add the widget to.</param>
        /// <param name="key">The key to use for the new widget's game object.</param>
        public static TextWidget AddText(this WidgetMountPoint mountPoint, string key = "Text")
        {
            var widget = new TextWidget(key);
            mountPoint.AddWidget(widget);
            return widget;
        }

        /// <summary>
        /// Adds a new button widget to the mount point.
        /// </summary>
        /// <param name="mountPoint">The mount point to add the widget to.</param>
        /// <param name="key">The key to use for the new widget's game object.</param>
        public static IconButtonWidget AddIconButton(this WidgetMountPoint mountPoint, string key = "IconButton")
        {
            var widget = new IconButtonWidget(key);
            mountPoint.AddWidget(widget);
            return widget;
        }

        /// <summary>
        /// Adds a new text button widget to the mount point.
        /// </summary>
        /// <param name="mountPoint">The mount point to add the widget to.</param>
        /// <param name="key">The key to use for the new widget's game object.</param>
        public static TextButtonWidget AddTextButton(this WidgetMountPoint mountPoint, string key = "TextButton")
        {
            var widget = new TextButtonWidget(key);
            mountPoint.AddWidget(widget);
            return widget;
        }

        /// <summary>
        /// Adds a new vertical layout group widget to the mount point.
        /// </summary>
        /// <param name="mountPoint">The mount point to add the widget to.</param>
        /// <param name="key">The key to use for the new widget's game object.</param>
        public static VerticalLayoutGroupWidget AddVerticalLayoutGroup(this WidgetMountPoint mountPoint, string key = "VerticalLayoutGroup")
        {
            var widget = new VerticalLayoutGroupWidget(key);
            mountPoint.AddWidget(widget);
            return widget;
        }

        /// <summary>
        /// Adds a new horizontal layout group widget to the mount point.
        /// </summary>
        /// <param name="mountPoint">The mount point to add the widget to.</param>
        /// <param name="key">The key to use for the new widget's game object.</param>
        public static HorizontalLayoutGroupWidget AddHorizontalLayoutGroup(this WidgetMountPoint mountPoint, string key = "HorizontalLayoutGroup")
        {
            var widget = new HorizontalLayoutGroupWidget(key);
            mountPoint.AddWidget(widget);
            return widget;
        }

        /// <summary>
        /// Adds a new scroll region widget to the mount point.
        /// </summary>
        /// <param name="mountPoint">The mount point to add the widget to.</param>
        /// <param name="key">The key to use for the new widget's game object.</param>
        public static ScrollRegionWidget AddScrollRegion(this WidgetMountPoint mountPoint, string key = "ScrollRegion")
        {
            var widget = new ScrollRegionWidget(key);
            mountPoint.AddWidget(widget);
            return widget;
        }
    }
}
