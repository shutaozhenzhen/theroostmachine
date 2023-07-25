namespace Roost.Piebald
{
    using System;
    using UnityEngine;
    using UnityEngine.EventSystems;

    // This is special.  We want UIGameObjectWidget to be assignable from UIGameObjectWidget<T>.
    // So we implement the bulk in here, and replace the props with the TCoreType variants in the offset.
    /// <summary>
    /// A UI widget that is backed by a GameObject.
    /// </summary>
    public class UIGameObjectWidget
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UIGameObjectWidget"/> class.
        /// The widget will be backed by a new game object.
        /// </summary>
        /// <param name="key">The name to give the new game object.</param>
        public UIGameObjectWidget(string key)
                    : this(new GameObject(key))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UIGameObjectWidget"/> class.
        /// The widget will be backed by the given game object.
        /// </summary>
        /// <param name="target">The game object to back the widget.</param>
        public UIGameObjectWidget(GameObject target)
        {
            this.GameObject = target;
            this.CanvasRenderer = this.GameObject.GetOrAddComponent<CanvasRenderer>();
        }

        /// <summary>
        /// Raised when the pointer enters this widget.
        /// </summary>
        public event EventHandler<PointerEventData> PointerEnter
        {
            add
            {
                this.GameObject.GetOrAddComponent<PointerEventMonitor>().PointerEnter += value;
            }

            remove
            {
                this.GameObject.GetOrAddComponent<PointerEventMonitor>().PointerEnter -= value;
            }
        }

        /// <summary>
        /// Raised when the pointer exits this widget.
        /// </summary>
        public event EventHandler<PointerEventData> PointerExit
        {
            add
            {
                this.GameObject.GetOrAddComponent<PointerEventMonitor>().PointerExit += value;
            }

            remove
            {
                this.GameObject.GetOrAddComponent<PointerEventMonitor>().PointerExit -= value;
            }
        }

        /// <summary>
        /// Raised when the pointer is clicked on this widget.
        /// </summary>
        public event EventHandler<PointerEventData> PointerClick
        {
            add
            {
                this.GameObject.GetOrAddComponent<PointerEventMonitor>().PointerClick += value;
            }

            remove
            {
                this.GameObject.GetOrAddComponent<PointerEventMonitor>().PointerClick -= value;
            }
        }

        /// <summary>
        /// Raised when the pointer begins dragging on this widget.
        /// </summary>
        public event EventHandler<PointerEventData> BeginDrag
        {
            add
            {
                this.GameObject.GetOrAddComponent<DragMonitor>().BeginDrag += value;
            }

            remove
            {
                this.GameObject.GetOrAddComponent<DragMonitor>().BeginDrag -= value;
            }
        }

        /// <summary>
        /// Raised when the pointer is dragged on this widget.
        /// </summary>
        public event EventHandler<PointerEventData> ContinueDrag
        {
            add
            {
                this.GameObject.GetOrAddComponent<DragMonitor>().ContinueDrag += value;
            }

            remove
            {
                this.GameObject.GetOrAddComponent<DragMonitor>().ContinueDrag -= value;
            }
        }

        /// <summary>
        /// Raised when the pointer ends dragging on this widget.
        /// </summary>
        public event EventHandler<PointerEventData> EndDrag
        {
            add
            {
                this.GameObject.GetOrAddComponent<DragMonitor>().EndDrag += value;
            }

            remove
            {
                this.GameObject.GetOrAddComponent<DragMonitor>().EndDrag -= value;
            }
        }

        /// <summary>
        /// Gets the game object that backs the widget.
        /// </summary>
        public GameObject GameObject { get; private set; }

        /// <summary>
        /// Gets the mount point for new children in this widget.
        /// Note that this may not be the same as <see cref="GameObject"/>, if the widget wraps its children.
        /// </summary>
        public virtual WidgetMountPoint MountPoint => new WidgetMountPoint(this.GameObject.transform);

        /// <summary>
        /// Gets the canvas renderer for the widget.
        /// </summary>
        public CanvasRenderer CanvasRenderer { get; private set; }

        public static implicit operator GameObject(UIGameObjectWidget widget)
        {
            return widget.GameObject;
        }

        public static implicit operator WidgetMountPoint(UIGameObjectWidget widget)
        {
            return widget.MountPoint;
        }

        /// <summary>
        /// Sets the active state of the widget.
        /// </summary>
        public UIGameObjectWidget SetActive(bool active)
        {
            this.GameObject.SetActive(active);
            return this;
        }

        /// <summary>
        /// Activates the widget.
        /// </summary>
        public UIGameObjectWidget Activate()
        {
            this.GameObject.SetActive(true);
            return this;
        }

        /// <summary>
        /// Deactivates the widget.
        /// </summary>
        public UIGameObjectWidget Deactivate()
        {
            this.GameObject.SetActive(false);
            return this;
        }

        /// <summary>
        /// Adds a behavior to the widget, if it is not already added.
        /// </summary>
        public UIGameObjectWidget WithBehavior<T>()
            where T : MonoBehaviour
        {
            this.GameObject.GetOrAddComponent<T>();
            return this;
        }

        /// <summary>
        /// Adds a behavior to the widget, if it is not already added, and then invokes an action on it.
        /// </summary>
        /// <param name="action">The action to invoke on the behavior.</param>
        public UIGameObjectWidget WithBehavior<T>(Action<T> action)
            where T : MonoBehaviour
        {
            action(this.GameObject.GetOrAddComponent<T>());
            return this;
        }

        /// <summary>
        /// Adds an event handler for when the pointer enters the widget.
        /// </summary>
        public UIGameObjectWidget OnPointerEnter(Action<PointerEventData> action)
        {
            this.GameObject.GetOrAddComponent<PointerEventMonitor>().PointerEnter += (sender, e) => action(e);
            return this;
        }

        /// <summary>
        /// Adds an event handler for when the pointer exits the widget.
        /// </summary>
        public UIGameObjectWidget OnPointerExit(Action<PointerEventData> action)
        {
            this.GameObject.GetOrAddComponent<PointerEventMonitor>().PointerExit += (sender, e) => action(e);
            return this;
        }

        /// <summary>
        /// Adds an event handler for when the pointer is clicked on the widget.
        /// </summary>
        public UIGameObjectWidget OnPointerClick(Action<PointerEventData> action)
        {
            this.GameObject.GetOrAddComponent<PointerEventMonitor>().PointerClick += (sender, e) => action(e);
            return this;
        }

        /// <summary>
        /// Adds an event handler for when the pointer begins dragging on the widget.
        /// </summary>
        public UIGameObjectWidget OnBeginDrag(Action<PointerEventData> action)
        {
            this.GameObject.GetOrAddComponent<DragMonitor>().BeginDrag += (sender, e) => action(e);
            return this;
        }

        /// <summary>
        /// Adds an event handler for when the pointer is dragged on the widget.
        /// </summary>
        public UIGameObjectWidget OnContinueDrag(Action<PointerEventData> action)
        {
            this.GameObject.GetOrAddComponent<DragMonitor>().ContinueDrag += (sender, e) => action(e);
            return this;
        }

        /// <summary>
        /// Adds an event handler for when the pointer ends dragging on the widget.
        /// </summary>
        public UIGameObjectWidget OnEndDrag(Action<PointerEventData> action)
        {
            this.GameObject.GetOrAddComponent<DragMonitor>().EndDrag += (sender, e) => action(e);
            return this;
        }

        /// <summary>
        /// Clears the widget of all children.
        /// </summary>
        public UIGameObjectWidget Clear()
        {
            this.MountPoint.Clear();
            return this;
        }

        /// <summary>
        /// Adds a game object as a child of the widget.
        /// </summary>
        public UIGameObjectWidget AddContent(GameObject gameObject)
        {
            gameObject.transform.SetParent(this.MountPoint, false);
            this.OnContentAdded();
            return this;
        }

        /// <summary>
        /// Runs a mount point factory on the widget.
        /// </summary>
        public UIGameObjectWidget AddContent(Action<WidgetMountPoint> factory)
        {
            factory(this.MountPoint);
            this.OnContentAdded();
            return this;
        }

        protected virtual void OnContentAdded() { }
    }

    /// <summary>
    /// A base class for a UI widget.
    /// </summary>
    public abstract class UIGameObjectWidget<TCoreType> : UIGameObjectWidget
        where TCoreType : UIGameObjectWidget<TCoreType>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UIGameObjectWidget"/> class.
        /// The widget will be backed by a new game object.
        /// </summary>
        /// <param name="key">The name to give the new game object.</param>
        public UIGameObjectWidget(string key)
            : this(new GameObject(key))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UIGameObjectWidget"/> class.
        /// The widget will be backed by the given game object.
        /// </summary>
        /// <param name="target">The game object to back the widget.</param>
        public UIGameObjectWidget(GameObject target)
            : base(target)
        {
        }

        /// <summary>
        /// Sets the active state of the widget.
        /// </summary>
        public new TCoreType SetActive(bool active)
        {
            base.SetActive(active);
            return this as TCoreType;
        }

        /// <summary>
        /// Activates the widget.
        /// </summary>
        public new TCoreType Activate()
        {
            base.Activate();
            return this as TCoreType;
        }

        /// <summary>
        /// Deactivates the widget.
        /// </summary>
        public new TCoreType Deactivate()
        {
            base.Deactivate();
            return this as TCoreType;
        }

        /// <summary>
        /// Adds a behavior to the widget, if it is not already added.
        /// </summary>
        public new TCoreType WithBehavior<T>()
            where T : MonoBehaviour
        {
            base.WithBehavior<T>();
            return this as TCoreType;
        }

        /// <summary>
        /// Adds a behavior to the widget, if it is not already added, and then invokes an action on it.
        /// </summary>
        /// <param name="action">The action to invoke on the behavior.</param>
        public new TCoreType WithBehavior<T>(Action<T> action)
            where T : MonoBehaviour
        {
            base.WithBehavior<T>(action);
            return this as TCoreType;
        }

        /// <summary>
        /// Adds an event handler for when the pointer enters the widget.
        /// </summary>
        public new TCoreType OnPointerEnter(Action<PointerEventData> action)
        {
            base.OnPointerEnter(action);
            return this as TCoreType;
        }

        /// <summary>
        /// Adds an event handler for when the pointer exits the widget.
        /// </summary>
        public new TCoreType OnPointerExit(Action<PointerEventData> action)
        {
            base.OnPointerExit(action);
            return this as TCoreType;
        }

        /// <summary>
        /// Adds an event handler for when the pointer is clicked on the widget.
        /// </summary>
        public new TCoreType OnPointerClick(Action<PointerEventData> action)
        {
            base.OnPointerClick(action);
            return this as TCoreType;
        }

        /// <summary>
        /// Adds an event handler for when the pointer begins dragging on the widget.
        /// </summary>
        public new TCoreType OnBeginDrag(Action<PointerEventData> action)
        {
            base.OnBeginDrag(action);
            return this as TCoreType;
        }

        /// <summary>
        /// Adds an event handler for when the pointer is dragged on the widget.
        /// </summary>
        public new TCoreType OnContinueDrag(Action<PointerEventData> action)
        {
            base.OnContinueDrag(action);
            return this as TCoreType;
        }

        /// <summary>
        /// Adds an event handler for when the pointer ends dragging on the widget.
        /// </summary>
        public new TCoreType OnEndDrag(Action<PointerEventData> action)
        {
            base.OnEndDrag(action);
            return this as TCoreType;
        }

        /// <summary>
        /// Clears the widget of all children.
        /// </summary>
        public new TCoreType Clear()
        {
            base.Clear();
            return this as TCoreType;
        }

        /// <summary>
        /// Adds a game object as a child of the widget.
        /// </summary>
        public new TCoreType AddContent(GameObject gameObject)
        {
            base.AddContent(gameObject);
            return this as TCoreType;
        }

        /// <summary>
        /// Runs a mount point factory on the widget.
        /// </summary>
        public new TCoreType AddContent(Action<WidgetMountPoint> factory)
        {
            base.AddContent(factory);
            return this as TCoreType;
        }
    }
}
