using System;
using System.Reflection;
using System.Collections;
using UnityEngine;

namespace Roost
{
    public static class Birdsong
    {
        public static VerbosityLevel currentVerbosity;
        public static void Sing(VerbosityLevel verbosity, int messageLevel, object data, params object[] furtherData)
        {
            if (currentVerbosity < verbosity)
                return;

            string message = FormatMessage(data, furtherData);
            NoonUtility.Log(message, messageLevel, verbosity);
        }

        public static void Sing(object data, params object[] furtherData)
        {
            Birdsong.Sing(0, 1, data, furtherData);
        }

        //finally, an optimal name for error throwing
        public static System.Exception Cack(object data, params object[] furtherData)
        {
            string message = FormatMessage(data, furtherData);
            return new ApplicationException(message);
        }

        private static string FormatMessage(object wrapMessageMaybe, params object[] furtherData)
        {
            if (wrapMessageMaybe == null)
                wrapMessageMaybe = "null";
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
                return String.Concat(wrapMessageMaybe.ToString(), " ", furtherData.UnpackAsString());
        }

        public static string UnpackAsString(this IEnumerable collection)
        {
            string result = string.Empty;
            foreach (object obj in collection)
                result += (obj == null ? "null" : obj.ToString()) + ' ';
            return result;
        }

        public static string FormatException(this Exception ex)
        {
            string errorMessage = ex.Message;
            while (ex.InnerException != null)
            {
                errorMessage += " - " + ex.InnerException.Message;
                ex = ex.InnerException;
            }

            return errorMessage;
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
