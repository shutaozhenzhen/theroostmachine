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
                original: typeof(SecretHistories.UI.NotificationWindow).GetMethodInvariant("SetDetails"),
                prefix: typeof(Birdsong).GetMethodInvariant("ShowNotificationWithIntervention"));
        }

        public static void Sing(VerbosityLevel verbosity, int messageLevel, object data, params object[] furtherData)
        {
            string message = FormatMessage(data, furtherData);
            NoonUtility.Log(message, messageLevel, verbosity);
        }

        public static void Sing(object data, params object[] furtherData)
        {
            Birdsong.Sing(0, 1, data, furtherData);
        }

        private static string FormatMessage(object wrapMessageMaybe, params object[] furtherData)
        {
            if (furtherData == null)
                return wrapMessageMaybe.ToString();

            if (wrapMessageMaybe.ToString().Contains("{0}"))
            {
                for (var n = 0; n < furtherData.Length; n++)
                    if (furtherData[n] == null)
                        furtherData[n] = "null";

                return String.Format(wrapMessageMaybe.ToString(), furtherData);
            }
            else
            {
                if (wrapMessageMaybe == null)
                    wrapMessageMaybe = "null";

                return String.Concat(wrapMessageMaybe.ToString(), " ", furtherData.UnpackAsString());
            }
        }

        public static string UnpackAsString(this IEnumerable collection)
        {
            string result = string.Empty;
            foreach (object obj in collection)
                result += (obj == null ? "null" : obj.ToString()) + ' ';
            return result;
        }

        public static MethodInfo GetMethodInvariant(this Type definingClass, string methodName)
        {
            //just GetMethod() that tries all possible BindingFlags combinations
            MethodInfo method = definingClass.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
                return method;

            method = definingClass.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
                return method;

            method = definingClass.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            if (method != null)
                return method;

            method = definingClass.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            if (method != null)
                return method;

            Birdsong.Sing("Method {0} not found in class {1}", methodName, definingClass.Name);
            return null;
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
