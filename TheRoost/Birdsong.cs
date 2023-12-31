﻿using System;
using System.Reflection;
using System.Collections;
using UnityEngine;

namespace Roost
{
    public static class Birdsong
    {
        public static VerbosityLevel sensivity = VerbosityLevel.Essential;
        public static object secret;
        public static bool debug => secret == null ? false : (bool)secret ;
        public static void SetVerbosityFromConfig(int value)
        {
            switch (value)
            {
                default:
                case 0:
                    sensivity = VerbosityLevel.Essential; return;
                case 1:
                    sensivity = VerbosityLevel.Significants; return;
                case 2:
                    sensivity = VerbosityLevel.SystemChatter; return;
                case 3:
                    sensivity = VerbosityLevel.Trivia; return;
            }
        }

        //this one reserved for operative debug logs; separated in its own method so I can easily find all the calls and won't accidentally left one in the release version
        public static void Sing(object data, params object[] furtherData)
        {
            Birdsong.Tweet(VerbosityLevel.Essential, 1, data, furtherData);
        }

        public static void TweetLoud(object data, params object[] furtherData)
        {
            Birdsong.Tweet(VerbosityLevel.Essential, 1, data, furtherData);
        }

        public static void TweetQuiet(object data, params object[] furtherData)
        {
            Birdsong.Tweet(VerbosityLevel.SystemChatter, 0, data, furtherData);
        }

        public static void Tweet(VerbosityLevel verbosity, int messageLevel, object data, params object[] furtherData)
        {
            if (sensivity < verbosity)
                return;

            string message = FormatMessage(data, furtherData);
            NoonUtility.Log(message, messageLevel, verbosity);
        }

        //finally, an optimal name for error throwing
        public static Exception Cack(object data, params object[] furtherData)
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
                return String.Concat(wrapMessageMaybe.ToString(), " ", furtherData.UnpackCollection());
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

        public static int alarmCount = 0;
        public static int Incr()
        {
            return alarmCount++;
        }

        public static void ResetIncr()
        {
            alarmCount = 0;
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

        public static void Schedule(Action action, YieldInstruction delay, object actor = null, object[] parameters = null)
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
