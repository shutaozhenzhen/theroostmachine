using System;
using System.Reflection;
using System.Collections;

using SecretHistories.UI;

using UnityEngine;
using UnityEngine.UI;

namespace TheRoost
{
    //extension class (spread around the files, with functions defined where they are needed)
    public static partial class Birdsong
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
            if (wrapMessageMaybe == null)
                wrapMessageMaybe = "null";

            if (furtherData == null)
                return wrapMessageMaybe.ToString();

            for (var n = 0; n < furtherData.Length; n++)
                if (furtherData[n] == null)
                    furtherData[n] = "null";

            if (wrapMessageMaybe.ToString().Contains("{0}"))
                return String.Format(wrapMessageMaybe.ToString(), furtherData);
            else
                return String.Concat(wrapMessageMaybe.ToString(), " ", furtherData.UnpackAsString());
        }

        public static string UnpackAsString(this IEnumerable collection)
        {
            string result = string.Empty;
            foreach (object obj in collection)
                result += obj.ToString() + ' ';
            return result;
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

        public static void Schedule(Delegate action, YieldInstruction delay, object actor = null, object[] parameters = null)
        {
            Schedule(action.Method, delay, actor, parameters);
        }

        internal IEnumerator ExecuteDelayed(MethodInfo action, object actor, object[] parameters, YieldInstruction delay)
        {
            yield return delay;
            action.Invoke(actor, parameters);
            Destroy(this.gameObject);
        }
    }
}
