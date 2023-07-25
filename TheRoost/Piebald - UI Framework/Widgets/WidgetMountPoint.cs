namespace Roost.Piebald
{
    using UnityEngine;

    /// <summary>
    /// Represents a mount point for a widget.
    /// Mount points determine where widgets are placed.
    /// They are analogous to transforms, but mount points may not always be on the root GameObject of a widget.
    /// <para/>
    /// For convienence, WidgetMountPoint is decorated with functions for creating and adding widgets through <see cref="WidgetMountPointExtensions"/>.
    /// </summary>
    public class WidgetMountPoint
    {
        public WidgetMountPoint(Transform transform)
        {
            this.Transform = transform;
        }

        public Transform Transform { get; private set; }

        public static implicit operator Transform(WidgetMountPoint mountPoint) => mountPoint.Transform;

        public static implicit operator GameObject(WidgetMountPoint mountPoint) => mountPoint.Transform.gameObject;

        public static void On(Transform transform, System.Action<WidgetMountPoint> action)
        {
            action(new WidgetMountPoint(transform));
        }

        public static void On(GameObject gameObject, System.Action<WidgetMountPoint> action)
        {
            action(new WidgetMountPoint(gameObject.transform));
        }

        public void Clear()
        {
            foreach (Transform child in this.Transform)
            {
                GameObject.Destroy(child.gameObject);
            }
        }

        public void AddWidget(UIGameObjectWidget widget)
        {
            widget.GameObject.transform.SetParent(this.Transform, false);
        }
    }
}
