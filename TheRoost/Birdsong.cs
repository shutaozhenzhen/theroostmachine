﻿using System;
using System.Reflection;
using System.Collections;

using SecretHistories.UI;
using SecretHistories.Fucine;

using UnityEngine;
using UnityEngine.UI;

namespace TheRoost
{
    //extension class
    public static class Birdsong
    {
        internal static void Enact()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            TheRoostMachine.Patch(
                original: typeof(SecretHistories.UI.NotificationWindow).GetMethod("SetDetails", BindingFlags.Public | BindingFlags.Instance),
                prefix: typeof(Birdsong).GetMethod("ShowNotificationWithIntervention", BindingFlags.NonPublic | BindingFlags.Static));
        }

        public static void Sing(object data, params object[] furtherData)
        {
            string message = FormatMessage(data, furtherData);
            NoonUtility.LogWarning(message);
        }

        public static void Sing(VerbosityLevel verbosity, int messageLevel, object data, params object[] furtherData)
        {
            string message = FormatMessage(data, furtherData);
            NoonUtility.Log(message, messageLevel, verbosity);
        }

        private static string FormatMessage(object wrapMessageMaybe, params object[] furtherData)
        {
            if (wrapMessageMaybe.ToString().Contains("{0}"))
                return String.Format(wrapMessageMaybe.ToString(), furtherData);
            else
                return wrapMessageMaybe.ToString() + ' ' + furtherData.UnpackAsString();
        }

        public static string UnpackAsString(this IEnumerable collection)
        {
            string result = string.Empty;
            if (collection != null)
                foreach (object obj in collection)
                    result += (obj == null ? "null" : obj.ToString()) + ' ';

            return result;
        }

        public static void ClaimProperty<TEntity, TProperty>(string propertyName, bool localize = false) where TEntity : AbstractEntity<TEntity>
        {
            TheRoost.Beachcomber.CustomLoader.ClaimProperty<TEntity, TProperty>(propertyName, localize);
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
                Birdsong.Sing("No Babelfish component on the GameObject '{0}'", babelfish.gameObject.name);
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
    }

    public class Rooster : MonoBehaviour
    {
        private static Rooster NewRooster()
        {
            GameObject gameObject = new GameObject();
            Rooster delayer = gameObject.AddComponent<Rooster>();
            GameObject.DontDestroyOnLoad(gameObject);
            return delayer;
        }

        public static void Schedule(MethodInfo action, YieldInstruction delay, object actor = null, object[] parameters = null)
        {
            Rooster delayer = NewRooster();
            delayer.StartCoroutine(delayer.ExecuteDelayed(action, actor, parameters, delay));
        }

        internal IEnumerator ExecuteDelayed(MethodInfo action, object actor, object[] parameters, YieldInstruction delay)
        {
            yield return delay;
            action.Invoke(actor, parameters);
            Destroy(this.gameObject);
        }

        public static void Schedule(Delegate action, YieldInstruction delay, object actor = null, object[] parameters = null)
        {
            Schedule(action.Method, delay, actor, parameters);
        }
    }
}
