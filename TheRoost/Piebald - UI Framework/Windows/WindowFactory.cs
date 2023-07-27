namespace Roost.Piebald
{
    using System;
    using UnityEngine;

    public static class WindowFactory
    {
        public static T CreateWindow<T>(string key)
            where T : AbstractWindow
        {
            if (typeof(ITableWindow).IsAssignableFrom(typeof(T)))
            {
                return CreateTabletopWindow<T>(key);
            }

            if (typeof(IMetaWindow).IsAssignableFrom(typeof(T)))
            {
                return CreateMetaWindow<T>(key);
            }

            throw new Exception("Cannot create window of type " + typeof(T).Name + ".  Window must implement either ITableWindow or IMetaWindow.");
        }

        private static T CreateTabletopWindow<T>(string key)
            where T : AbstractWindow
        {
            var mountPoint = MountPoints.TabletopWindowLayer;
            if (mountPoint == null)
            {
                throw new Exception("Cannot find Tabletop window mount point.");
            }

            var gameObject = new GameObject(key);
            gameObject.transform.SetParent(mountPoint, false);
            return gameObject.AddComponent<T>();
        }

        private static T CreateMetaWindow<T>(string key)
            where T : AbstractWindow
        {
            var mountPoint = MountPoints.MetaWindowLayer;
            if (mountPoint == null)
            {
                throw new Exception("Cannot find CanvasMeta.");
            }

            var gameObject = new GameObject(key);
            gameObject.transform.SetParent(mountPoint, false);
            return gameObject.AddComponent<T>();
        }
    }
}
