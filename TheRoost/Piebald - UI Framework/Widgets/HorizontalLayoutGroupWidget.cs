namespace Roost.Piebald
{
    using System;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// A UI widget that arranges its children horizontally.
    /// </summary>
    public class HorizontalLayoutGroupWidget : LayoutItemWidget<HorizontalLayoutGroupWidget>
    {
        public HorizontalLayoutGroupWidget(string key)
            : this(new GameObject(key))
        {
        }

        public HorizontalLayoutGroupWidget(GameObject gameObject)
            : base(gameObject)
        {
            this.LayoutGroup = this.GameObject.GetOrAddComponent<HorizontalLayoutGroup>();
            this.SetChildAlignment(TextAnchor.UpperLeft);
            this.SetSpacing(0);

            // These absolutely must be on, or all our children will be zero-sized.
            this.EnableLayoutSystemVertical(true);
            this.EnableLayoutSystemHorizontal(true);

            this.SetSpreadChildrenVertically(false);
            this.SetSpreadChildrenHorizontally(false);
        }

        public HorizontalLayoutGroup LayoutGroup { get; private set; }

        public TextAnchor ChildAlignment
        {
            get
            {
                return this.LayoutGroup.childAlignment;
            }

            set
            {
                this.LayoutGroup.childAlignment = value;
            }
        }

        public float Spacing
        {
            get
            {
                return this.LayoutGroup.spacing;
            }

            set
            {
                this.LayoutGroup.spacing = value;
            }
        }

        public RectOffset Padding
        {
            get
            {
                return this.LayoutGroup.padding;
            }

            set
            {
                this.LayoutGroup.padding = value;
            }
        }

        public HorizontalLayoutGroupWidget SetChildAlignment(TextAnchor value)
        {
            this.LayoutGroup.childAlignment = value;
            return this;
        }

        // Fucking stupid name, but this property does both.
        public HorizontalLayoutGroupWidget EnableLayoutSystemVertical(bool value)
        {
            this.LayoutGroup.childControlWidth = value;
            return this;
        }

        // Fucking stupid name, but this property does both.
        public HorizontalLayoutGroupWidget EnableLayoutSystemHorizontal(bool value)
        {
            this.LayoutGroup.childControlHeight = value;
            return this;
        }

        // what the fuck does this even do.  It centers things but leaves other things zero sized.
        public HorizontalLayoutGroupWidget SetSpreadChildrenHorizontally(bool value = true)
        {
            this.LayoutGroup.childForceExpandWidth = value;
            return this;
        }

        // what the fuck does this even do.  It centers things but leaves other things zero sized.
        public HorizontalLayoutGroupWidget SetSpreadChildrenVertically(bool value = true)
        {
            this.LayoutGroup.childForceExpandHeight = value;
            return this;
        }

        public HorizontalLayoutGroupWidget SetSpacing(float value)
        {
            this.LayoutGroup.spacing = value;
            return this;
        }

        public HorizontalLayoutGroupWidget SetPadding(int left, int top, int right, int bottom)
        {
            this.LayoutGroup.padding = new RectOffset(left, right, top, bottom);
            return this;
        }

        public HorizontalLayoutGroupWidget SetPadding(int x, int y)
        {
            this.LayoutGroup.padding = new RectOffset(x, x, y, y);
            return this;
        }

        public HorizontalLayoutGroupWidget SetPadding(int value)
        {
            this.LayoutGroup.padding = new RectOffset(value, value, value, value);
            return this;
        }
    }
}
