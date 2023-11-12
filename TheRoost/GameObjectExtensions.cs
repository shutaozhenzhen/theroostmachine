using UnityEngine;
using System;

namespace Roost
{
    public static class GameObjectExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject gameObject)
            where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        public static GameObject FindGameObject(string name, bool includeInactive)
        {
            //NB - case sensitive
            UnityEngine.GameObject result = GameObject.Find(name);

            if (result == null)
            {
                GameObject[] allGO = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject go in allGO)
                    if (go.name == name)
                        return go;
            }

            return result;
        }

        public static GameObject FindInChildren(this GameObject go, string name, bool nested = false)
        {
            //NB - case insensitive
            Transform transform = go.transform;
            for (int n = 0; n < transform.childCount; n++)
                if (string.Equals(transform.GetChild(n).name, name, StringComparison.OrdinalIgnoreCase))
                    return transform.GetChild(n).gameObject;
                else if (nested)
                {
                    GameObject nestedFound = FindInChildren(transform.GetChild(n).gameObject, name, true);
                    if (nestedFound != null)
                        return nestedFound;
                }

            return null;
        }

    }
}
