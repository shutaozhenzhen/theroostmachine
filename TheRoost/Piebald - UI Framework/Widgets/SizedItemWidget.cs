namespace Roost.Piebald
{
    using UnityEngine;

    /// <summary>
    /// A UI Widget that sizes itself based on its parent through use of a <see cref="RectTransform"/>.
    /// </summary>
    public class SizedItemWidget : SizedItemWidget<SizedItemWidget>
    {
        public SizedItemWidget(string key) : base(key) { }
        public SizedItemWidget(GameObject gameObject) : base(gameObject) { }
    }

    /// <summary>
    /// A base class for UI widgets that siz themselves based on its parent through use of a <see cref="RectTransform"/>.
    /// </summary>
    public abstract class SizedItemWidget<TCoreType> : UIGameObjectWidget<TCoreType>
        where TCoreType : SizedItemWidget<TCoreType>
    {
        public SizedItemWidget(string key)
            : this(new GameObject(key))
        {
        }

        public SizedItemWidget(GameObject gameObject)
            : base(gameObject)
        {
            this.RectTransform = this.GameObject.GetOrAddComponent<RectTransform>();
            this.RectTransform.anchorMin = Vector2.zero;
            this.RectTransform.anchorMax = Vector2.one;
            this.RectTransform.offsetMin = Vector2.zero;
            this.RectTransform.offsetMax = Vector2.zero;
        }

        public RectTransform RectTransform { get; private set; }

        public float AnchorTop
        {
            get
            {
                return this.RectTransform.anchorMax.y;
            }

            set
            {
                this.RectTransform.anchorMax = new Vector2(this.RectTransform.anchorMax.x, value);
            }
        }

        public float AnchorBottom
        {
            get
            {
                return this.RectTransform.anchorMin.y;
            }

            set
            {
                this.RectTransform.anchorMin = new Vector2(this.RectTransform.anchorMin.x, value);
            }
        }

        public float AnchorLeft
        {
            get
            {
                return this.RectTransform.anchorMin.x;
            }

            set
            {
                this.RectTransform.anchorMin = new Vector2(value, this.RectTransform.anchorMin.y);
            }
        }

        public float AnchorRight
        {
            get
            {
                return this.RectTransform.anchorMax.x;
            }

            set
            {
                this.RectTransform.anchorMax = new Vector2(value, this.RectTransform.anchorMax.y);
            }
        }

        public float OffsetTop
        {
            get
            {
                return this.RectTransform.offsetMax.y;
            }

            set
            {
                this.RectTransform.offsetMax = new Vector2(this.RectTransform.offsetMax.x, value);
            }
        }

        public float OffsetBottom
        {
            get
            {
                return this.RectTransform.offsetMin.y;
            }

            set
            {
                this.RectTransform.offsetMin = new Vector2(this.RectTransform.offsetMin.x, value);
            }
        }

        public float OffsetLeft
        {
            get
            {
                return this.RectTransform.offsetMin.x;
            }

            set
            {
                this.RectTransform.offsetMin = new Vector2(value, this.RectTransform.offsetMin.y);
            }
        }

        public float OffsetRight
        {
            get
            {
                return this.RectTransform.offsetMax.x;
            }

            set
            {
                this.RectTransform.offsetMax = new Vector2(value, this.RectTransform.offsetMax.y);
            }
        }

        public Vector2 SizeDelta
        {
            get
            {
                return this.RectTransform.sizeDelta;
            }

            set
            {
                this.RectTransform.sizeDelta = value;
            }
        }

        // Anchors alternatively do nothing and break stuff, so don't provide access until we can figure out what they do.
        // public TCoreType SetAnchor(Vector3 anchor)
        // {
        //     this.RectTransform.anchoredPosition3D = anchor;
        //     return this as TCoreType;
        // }

        // public TCoreType SetAnchor(Vector2 anchor)
        // {
        //     this.RectTransform.anchoredPosition = anchor;
        //     return this as TCoreType;
        // }

        // public TCoreType SetAnchor(float x, float y)
        // {
        //     this.RectTransform.anchoredPosition = new Vector2(x, y);
        //     return this as TCoreType;
        // }

        public TCoreType SetZOffset(float value)
        {
            // I don't trust I understand what anchoredPositon3D does enough to expose it to the world,
            // but it does seem to control our z offset directly.
            // As far as I can tell, anchoredPosition3D is an algimation of the local position and an unholy fusion of offsetMin/offsetMax
            // For now, just expose its z value for cases where it is useful.
            this.RectTransform.anchoredPosition3D = new Vector3(this.RectTransform.anchoredPosition3D.x, this.RectTransform.anchoredPosition3D.y, value);
            return this as TCoreType;
        }

        public TCoreType SetPositionAndSize(Vector2 center, Vector2 size)
        {
            this.SetPositionAndSize((Vector3)center, size);
            return this as TCoreType;
        }

        public TCoreType SetPositionAndSize(Vector3 center, Vector2 size)
        {
            // Does this even do anything?  Testing shows inconsistent results, and mostly it does nothing.
            // this.RectTransform.anchoredPosition3D = center;

            // Some things want to set z axes. What does that even do?  Who knows...
            // Unity documents this in reference to a "anchor pivot", which clearly isn't the same as SetPivot as instead of controlling rotation,
            // unity docs hint that this affects the rendered position.
            // Let's just set the z and totally ignore it, as unity seems to totally ignore it too.
            this.RectTransform.anchoredPosition3D = new Vector3(0, 0, center.z);

            var halfSize = size / 2f;
            this.RectTransform.anchorMin = Vector2.zero;
            this.RectTransform.anchorMax = Vector2.zero;
            this.RectTransform.offsetMin = (Vector2)center - halfSize;
            this.RectTransform.offsetMax = (Vector2)center + halfSize;

            // This sometimes sets the size when the anchors are the same, sometimes it offsets the size set by the anchors, and sometimes it does nothing.
            // Unity is baffling.
            // this.RectTransform.sizeDelta = size;

            return this as TCoreType;
        }

        public TCoreType SetAnchorAndSize(Vector2 anchor, Vector2 size)
        {
            this.SetAnchorAndSize(anchor, Vector2.zero, size);
            return this as TCoreType;
        }

        public TCoreType SetAnchorAndSize(Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var halfSize = size / 2f;
            this.RectTransform.anchorMin = anchor;
            this.RectTransform.anchorMax = anchor;
            this.RectTransform.offsetMin = offset - halfSize;
            this.RectTransform.offsetMax = offset + halfSize;

            // Again, lets ignore sizeDelta and the madness it introduces.

            return this as TCoreType;
        }

        public TCoreType SetOffset(float offset)
        {
            this.SetOffset(offset, offset, offset, offset);
            return this as TCoreType;
        }

        public TCoreType SetOffset(float horizontal, float vertical)
        {
            this.SetOffset(horizontal, vertical, horizontal, vertical);
            return this as TCoreType;
        }

        public TCoreType SetOffset(Vector2 offset)
        {
            this.SetOffset(offset.x, offset.y, offset.x, offset.y);
            return this as TCoreType;
        }

        public TCoreType SetOffset(float left, float top, float right, float bottom)
        {
            this.RectTransform.offsetMin = new Vector2(left, bottom);
            this.RectTransform.offsetMax = new Vector2(right, top);
            return this as TCoreType;
        }

        public TCoreType SetOffset(Vector2 min, Vector2 max)
        {
            this.RectTransform.offsetMin = min;
            this.RectTransform.offsetMax = max;
            return this as TCoreType;
        }

        public TCoreType SetSizeOffset(Vector2 size)
        {
            this.RectTransform.sizeDelta = size;
            return this as TCoreType;
        }

        public TCoreType SetSizeOffset(float width, float height)
        {
            this.SetSizeOffset(new Vector2(width, height));
            return this as TCoreType;
        }

        public TCoreType SetPivot(Vector2 pivot)
        {
            this.RectTransform.pivot = pivot;
            return this as TCoreType;
        }

        public TCoreType SetPivot(float x, float y)
        {
            this.RectTransform.pivot = new Vector2(x, y);
            return this as TCoreType;
        }

        public TCoreType SetLeft(float anchor, float offset)
        {
            this.RectTransform.anchorMin = new Vector2(anchor, this.RectTransform.anchorMin.y);
            this.RectTransform.offsetMin = new Vector2(offset, this.RectTransform.offsetMin.y);
            return this as TCoreType;
        }

        public TCoreType SetRight(float anchor, float offset)
        {
            this.RectTransform.anchorMax = new Vector2(anchor, this.RectTransform.anchorMax.y);
            this.RectTransform.offsetMax = new Vector2(offset, this.RectTransform.offsetMax.y);
            return this as TCoreType;
        }

        public TCoreType SetTop(float anchor, float offset)
        {
            this.RectTransform.anchorMax = new Vector2(this.RectTransform.anchorMax.x, anchor);
            this.RectTransform.offsetMax = new Vector2(this.RectTransform.offsetMax.x, offset);
            return this as TCoreType;
        }

        public TCoreType SetBottom(float anchor, float offset)
        {
            this.RectTransform.anchorMin = new Vector2(this.RectTransform.anchorMin.x, anchor);
            this.RectTransform.offsetMin = new Vector2(this.RectTransform.offsetMin.x, offset);
            return this as TCoreType;
        }
    }
}
