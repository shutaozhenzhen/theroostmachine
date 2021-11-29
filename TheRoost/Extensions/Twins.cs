using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Fucine;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheRoostManchine
{
    public static class Twins
    {
        public delegate void ServicesInitialised();
        public static ServicesInitialised onServicesInitialized;

        public static void Sing(string wrapMessage, params object[] data)
        {
            if (data.Length > 0)
                NoonUtility.LogWarning(String.Format(wrapMessage, data));
            else
                NoonUtility.LogWarning(wrapMessage);
        }

        public static void Sing(params object[] data)
        {
            var str = string.Empty;
            foreach (object obj in data)
                NoonUtility.LogWarning((obj == null ? "null" : obj.ToString()));
        }

        public static GameObject FindInChildren(this GameObject go, string targetName, bool nested = false)
        {
            Transform transform = go.transform;
            for (int n = 0; n < transform.childCount; n++)
                if (String.Equals(transform.GetChild(n).name, targetName, StringComparison.OrdinalIgnoreCase))
                    return transform.GetChild(n).gameObject;
                else if (nested)
                {
                    GameObject nestedFound = FindInChildren(transform.GetChild(n).gameObject, targetName, true);
                    if (nestedFound != null)
                        return nestedFound;
                }

            return null;
        }

        public static void SetBabelLabel(this Babelfish babelfish, string locLabel)
        {
            if (babelfish == null)
            {
                Twins.Sing("No Babelfish component on the GameObject '{0}'", babelfish.gameObject.name);
                return;
            }

            typeof(Babelfish).GetField("locLabel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(babelfish, locLabel);
            babelfish.SetValuesForCurrentCulture();
        }

        public static bool intervention = false;
        public static Sprite interventionSprite;
        public static float interventionDuration;
        public static void ShowNotificationWindow(this Notifier notifier, string title, string description, Sprite image, float duration, bool duplicatesAllowed = true)
        {
            intervention = true;
            interventionSprite = image;
            interventionDuration = duration;
            notifier.ShowNotificationWindow(title, description, duplicatesAllowed);
            intervention = false;
            interventionSprite = null;
            interventionDuration = -1;
        }

        private static void ShowNotificationWithIntervention(NotificationWindow __instance, ref Image ___artwork)
        {
            if (intervention)
            {
                if (interventionDuration > 0)
                {
                    __instance.CancelInvoke("Hide");
                    __instance.SetDuration(interventionDuration);
                }
                if (interventionSprite != null)
                    ___artwork.sprite = interventionSprite;
            }
        }

        public static Delayer Schedule(MethodInfo action, object actor = null, object[] parameters = null)
        {
            GameObject gameObject = new GameObject();
            GameObject.DontDestroyOnLoad(gameObject);
            Delayer delayer = gameObject.AddComponent<Delayer>();
            delayer.StartCoroutine(delayer.ExecuteDelayed(action, actor, parameters));

            return delayer;
        }
    }

    public class Delayer : MonoBehaviour
    {
        public IEnumerator ExecuteDelayed(System.Reflection.MethodInfo action, object actor, object[] parameters)
        {
            yield return new WaitForEndOfFrame();
            action.Invoke(actor, parameters);
            Destroy(this.gameObject);
        }
    }
}
