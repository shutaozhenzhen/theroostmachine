using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

using HarmonyLib;

using SecretHistories.Fucine;
using SecretHistories.UI;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;

namespace TheRoostManchine
{
    internal class Vagabond : MonoBehaviour
    {
        private static void Invoke()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            TheRoostMachine.Patch(
                original: typeof(MenuScreenController).GetMethod("InitialiseServices", BindingFlags.NonPublic | BindingFlags.Instance),
                prefix: typeof(Vagabond).GetMethod("SetInterface", BindingFlags.NonPublic | BindingFlags.Static));

            //gotta do that litte favour for the Twins since they are static class and are inconvenient to initialise (aesthetically)
            TheRoostMachine.Patch(
                original: typeof(NotificationWindow).GetMethod("SetDetails", BindingFlags.Public | BindingFlags.Instance),
                prefix: typeof(Twins).GetMethod("ShowNotificationWithIntervention", BindingFlags.NonPublic | BindingFlags.Static));
        }

        static Dictionary<string, Action<string[]>> testMethods = new Dictionary<string, Action<string[]>>();
        public static void AddTest(string reference, Action<string[]> method)
        {
            testMethods.Add(reference, method);
        }
        static void InvokeTest(string[] command)
        {
            string[] arguments = new string[command.Length - 2];
            Array.Copy(command, 2, arguments, 0, command.Length - 2);
            testMethods[command[1]].Invoke(arguments);
        }


        GameObject console;
        void Update()
        {
            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
                if (GameObject.FindObjectOfType<DebugTools>() == null)
                    Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
        }



        private static void ExecuteCommand(string text)
        {
            string[] command = text.Split();

            switch (command[0])
            {
                case "/achievements": AchievementCommand(command); break;
                case "/goinfo": GameObjectCommand(command); break;
                case "/goset": GameObjectCommand(command); break;
                case "/compendium": GameObjectCommand(command); break;
                case "/test": InvokeTest(command); break;
                default: Twins.Sing("Unknown command"); break;
            }
        }

        static void AchievementCommand(string[] command)
        {
            try
            {
                string data = string.Empty;
                switch (command[1])
                {
                    case "reset": Elegiast.ClearAchievement(command[2]); return;
                    case "cloud": data = Elegiast.ReadableCloudData(); break;
                    case "local": Elegiast.ReadableCloudData(); break;
                    case "all": Elegiast.ReadableAll(); break;
                }

                if (command.Length == 2)
                    Twins.Sing(data);
                else
                    Twins.Sing("Checking achievement '{0}' presence: {1}", command[2], data.Contains("\"" + command[2] + "\""));
            }
            catch (Exception ex)
            {
                Twins.Sing(ex);
            }
        }

        static void GameObjectCommand(string[] command)
        {
            try
            {
                string[] entityPath = command[0].Split('.');
                object entity = GetEntity(entityPath);
                if (entity == null)
                {
                    if (entityPath.Length == 1)
                        Twins.Sing("No GameObject '{0}' found", entityPath[0]);
                    else if (entityPath.Length == 2)
                        Twins.Sing("No Component '{0}' found on {1}", entityPath[1], entityPath[0]);
                }

                if (command.Length == 1)
                {
                    if (entity.GetType() == typeof(GameObject))
                        Twins.Sing((entity as GameObject).name, ((GameObject)entity).GetComponents(typeof(Component)));
                    else
                        foreach (PropertyInfo property in entity.GetType().GetProperties())
                            Twins.Sing(property.Name, property.GetValue(entity));
                    return;
                }

                PropertyInfo targetProperty = entity.GetType().GetProperty(command[1]);
                if (command.Length == 2) //no value specified, just return property
                {
                    Twins.Sing(targetProperty.GetValue(entity));
                    return;
                }

                string string_value = command[2];
                for (var n = 3; n < command.Length; n++)
                    string_value += " " + command[n];

                object value = ConvertValue(string_value, targetProperty);
                targetProperty.SetValue(entity, value);
            }
            catch (Exception ex)
            {
                Twins.Sing(ex);
            }
        }

        private static object ConvertValue(string value, PropertyInfo property)
        {
            if (value == "null")
                return null;
            else if (property.PropertyType == typeof(string))
                return value;
            else if (property.PropertyType.IsEnum)
                return Enum.Parse(property.PropertyType, value);
            else if (property.PropertyType.IsValueType)
                return Convert.ChangeType(value, property.PropertyType);

            Twins.Sing("Can't convert value {0} into {1}", value, property.PropertyType.Name);
            return null;
        }

        private static object GetEntity(string[] path)
        {
            object result = GameObject.Find(path[0]);
            if (result == null || path.Length == 1)
                return result;

            int i = 1;
            while (path[i].Contains("child"))
            {
                result = (result as GameObject).transform.GetChild(int.Parse(path[i + 1])).gameObject;
                i += 2;
                if (result == null || i >= path.Length)
                    return result;
            }
            while (path[i].Contains("parent"))
            {
                result = (result as GameObject).transform.parent.gameObject;
                i++;
                if (result == null || i >= path.Length)
                    return result;
            }

            result = (result as GameObject).GetComponent(path[i]);
            if (result == null || i == path.Length)
                return result;

            for (var n = i; n < path.Length - 1; n++)
            {
                result = result.GetType().GetProperty(path[n]).GetValue(result);
                if (result == null)
                {
                    Twins.Sing("No property '{0}' found", path[n]);
                    break;
                }
            }

            return result;
        }

        private static void SetInterface()
        {
            //another little favour
            if (Twins.onServicesInitialized != null)
                Twins.onServicesInitialized.Invoke();

            Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
            var debugCanvas = GameObject.Find("SecretHistoryLogMessageEntry(Clone)").GetComponentInParent<Canvas>().transform;

            var console = new GameObject("AuxConsole", typeof(Vagabond), typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            GameObject.FindObjectOfType<SecretHistories.Services.SecretHistory>().gameObject.AddComponent<Vagabond>().console = console;

            console.GetComponent<Image>().color = debugCanvas.GetComponentsInChildren<Image>()[1].color;

            var consoleT = console.GetComponent<RectTransform>();
            consoleT.SetParent(debugCanvas.GetChild(0));
            consoleT.pivot = new Vector2(0.5f, 1f);
            consoleT.anchorMin = new Vector2(0.13f, 0f);
            consoleT.anchorMax = new Vector2(0.85f, 0f);
            consoleT.sizeDelta = new Vector2(0, 30);
            consoleT.anchoredPosition = new Vector2(0, 2);
            consoleT.localScale = Vector2.one;

            GameObject button = GameObject.Instantiate(GameObject.Find("SecretHistoryLogMessageEntry(Clone)").gameObject, consoleT);
            RectTransform textT = button.GetComponent<RectTransform>();
            textT.localScale = Vector3.one;
            textT.pivot = Vector2.one / 2;
            textT.anchorMin = Vector2.zero;
            textT.anchorMax = Vector2.one;
            textT.anchoredPosition = new Vector2(10, 0);
            textT.sizeDelta = Vector2.zero;

            TextMeshProUGUI text = textT.gameObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.fontSizeMin = 1;
            text.fontSizeMax = text.fontSize;
            text.enableAutoSizing = true;

            TMP_InputField consoleI = console.GetComponent<TMP_InputField>();
            consoleI.targetGraphic = console.GetComponent<Image>();
            consoleI.textComponent = text;
            consoleI.text = "...";

            consoleI.onSubmit = new TMP_InputField.SubmitEvent();
            consoleI.onSubmit.AddListener(new UnityEngine.Events.UnityAction<string>(ExecuteCommand));

            Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
        }
    }
}