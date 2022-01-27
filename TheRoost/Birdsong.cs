﻿using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace TheRoost
{
    //extension class (spread around the files, with functions defined where they are needed)
    public static partial class Birdsong
    {
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

        static List<BindingFlags> bindingFlagsPriority = new List<BindingFlags> { 
            (BindingFlags.Instance | BindingFlags.Public), 
            (BindingFlags.Instance | BindingFlags.NonPublic),
            (BindingFlags.Static | BindingFlags.Public),
            (BindingFlags.Static | BindingFlags.NonPublic),
        };
        public static MethodInfo GetMethodInvariant(this Type definingClass, string methodName)
        {
            MethodInfo method;
            foreach (BindingFlags flag in bindingFlagsPriority)
            {
                method = definingClass.GetMethod(methodName, flag);
                if (method != null)
                    return method;
            }

            Birdsong.Sing("Method {0} not found in class {1}", methodName, definingClass.Name);
            return null;
        }

        public static FieldInfo GetFieldInvariant(this Type definingClass, string fieldName)
        {
            FieldInfo field;
            foreach (BindingFlags flag in bindingFlagsPriority)
            {
                field = definingClass.GetField(fieldName, flag);
                if (field != null)
                    return field;
            }

            Birdsong.Sing("Field {0} not found in class {1}", fieldName, definingClass.Name);
            return null;
        }

        public static PropertyInfo GetPropertyInvariant(this Type definingClass, string propertyName)
        {
            PropertyInfo property;
            foreach (BindingFlags flag in bindingFlagsPriority)
            {
                property = definingClass.GetProperty(propertyName, flag);
                if (property != null)
                    return property;
            }

            Birdsong.Sing("Property {0} not found in class {1}", propertyName, definingClass.Name);
            return null;
        }

        public static T GetConfigValue<T>(string configId, T valueIfNotDefined = default(T))
        {
            return TheRoost.Vagabond.RoostConfig.GetConfigValueSafe<T>(configId, valueIfNotDefined);
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
