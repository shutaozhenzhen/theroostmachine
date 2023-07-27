namespace Roost.Piebald
{
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// A UI widget that dynamically determines its size.
    /// </summary>
    public class LayoutItemWidget : LayoutItemWidget<LayoutItemWidget>
    {
        public LayoutItemWidget(string key) : base(key) { }
        public LayoutItemWidget(GameObject gameObject) : base(gameObject) { }
    }

    /// <summary>
    /// A base class for UI widgets that dynamically determine their size.
    /// </summary>
    public abstract class LayoutItemWidget<TCoreType> : SizedItemWidget<TCoreType>
        where TCoreType : LayoutItemWidget<TCoreType>
    {
        private LayoutElement layoutElement;
        private ContentSizeFitter contentSizeFitter;

        public LayoutItemWidget(string key)
            : this(new GameObject(key))
        {
        }

        public LayoutItemWidget(GameObject gameObject)
            : base(gameObject)
        {
            // Always do this, in case we find ourselves in a dreaded childControlsWidth/Height group.
            this.layoutElement = this.GameObject.GetOrAddComponent<LayoutElement>();
            this.layoutElement.flexibleWidth = 0;
            this.layoutElement.flexibleHeight = 0;
            this.layoutElement.minHeight = -1;
            this.layoutElement.minWidth = -1;
            this.layoutElement.preferredHeight = -1;
            this.layoutElement.preferredWidth = -1;
        }

        public LayoutElement LayoutElement
        {
            get
            {
                return this.layoutElement;
            }
        }

        public ContentSizeFitter ContentSizeFitter
        {
            get
            {
                if (this.contentSizeFitter == null)
                {
                    this.contentSizeFitter = this.GameObject.GetOrAddComponent<ContentSizeFitter>();
                    this.contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                    this.contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                }

                return this.contentSizeFitter;
            }
        }

        public bool IgnoreLayout
        {
            get
            {
                return this.LayoutElement.ignoreLayout;
            }

            set
            {
                this.LayoutElement.ignoreLayout = value;
            }
        }

        public float MinWidth
        {
            get
            {
                return this.LayoutElement.minWidth;
            }

            set
            {
                this.LayoutElement.minWidth = value;
            }
        }

        public float MinHeight
        {
            get
            {
                return this.LayoutElement.minHeight;
            }

            set
            {
                this.LayoutElement.minHeight = value;
            }
        }

        public float PreferredWidth
        {
            get
            {
                return this.LayoutElement.preferredWidth;
            }

            set
            {
                this.LayoutElement.preferredWidth = value;
            }
        }

        public float PreferredHeight
        {
            get
            {
                return this.LayoutElement.preferredHeight;
            }

            set
            {
                this.LayoutElement.preferredHeight = value;
            }
        }

        public ContentSizeFitter.FitMode VerticalFit
        {
            get
            {
                return this.ContentSizeFitter.verticalFit;
            }

            set
            {
                this.ContentSizeFitter.verticalFit = value;
            }
        }

        public ContentSizeFitter.FitMode HorizontalFit
        {
            get
            {
                return this.ContentSizeFitter.horizontalFit;
            }

            set
            {
                this.ContentSizeFitter.horizontalFit = value;
            }
        }

        public static implicit operator LayoutItemWidget(LayoutItemWidget<TCoreType> widget)
        {
            return new LayoutItemWidget(widget.GameObject);
        }

        public TCoreType SetIgnoreLayout()
        {
            this.LayoutElement.ignoreLayout = true;
            return this as TCoreType;
        }

        public TCoreType SetMinWidth(float width)
        {
            this.LayoutElement.minWidth = width;
            return this as TCoreType;
        }

        public TCoreType SetMinHeight(float height)
        {
            this.LayoutElement.minHeight = height;
            return this as TCoreType;
        }

        public TCoreType SetPreferredWidth(float width)
        {
            this.LayoutElement.preferredWidth = width;
            return this as TCoreType;
        }

        public TCoreType SetPreferredHeight(float height)
        {
            this.LayoutElement.preferredHeight = height;
            return this as TCoreType;
        }

        /*
        public TCoreType MaxWidth(float width)
        {
            this.ConstrainedLayoutElement.MaxWidth = width;
            return this as TCoreType;
        }

        public TCoreType MaxHeight(float height)
        {
            this.ConstrainedLayoutElement.MaxHeight = height;
            return this as TCoreType;
        }
        */

        public TCoreType SetExpandWidth()
        {
            this.LayoutElement.flexibleWidth = 1;
            return this as TCoreType;
        }

        public TCoreType SetExpandHeight()
        {
            this.LayoutElement.flexibleHeight = 1;
            return this as TCoreType;
        }

        public TCoreType SetFitContentWidth()
        {
            this.ContentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return this as TCoreType;
        }

        public TCoreType SetFitContentHeight()
        {
            this.ContentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return this as TCoreType;
        }
    }
}
