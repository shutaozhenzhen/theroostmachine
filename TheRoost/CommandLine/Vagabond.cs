using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;

using SecretHistories.Fucine;
using SecretHistories.UI;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;

namespace TheRoost
{
    internal class Vagabond : MonoBehaviour
    {
        private static void Invoke()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            var harmony = new Harmony("theroost.vagabond");
            var original = typeof(MenuScreenController).GetMethod("InitialiseServices", BindingFlags.NonPublic | BindingFlags.Instance);
            var patched = typeof(Vagabond).GetMethod("SetInterface", BindingFlags.NonPublic | BindingFlags.Static);
            harmony.Patch(original, prefix: new HarmonyMethod(patched));

            //gotta do that litte favour for the Twins since they are static class and are inconvenient to initialise (aesthetically)
            original = typeof(NotificationWindow).GetMethod("SetDetails", BindingFlags.Public | BindingFlags.Instance);
            patched = typeof(Twins).GetMethod("ShowNotificationWithIntervention", BindingFlags.NonPublic | BindingFlags.Static);
            harmony.Patch(original, prefix: new HarmonyMethod(patched));
        }

        private static void SetInterface()
        {
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

        private static void ExecuteCommand(string text)
        {
            try
            {
                string[] command = text.Split();

                if (command[0] == "achievements.cloud" || command[0] == "achievements.local" || command[0] == "achievements.all")
                {
                    string data;

                    if (command[0] == "achievements.cloud")
                        data = Elegiast.ReadableCloudData();
                    else if (command[0] == "achievements.local")
                        data = Elegiast.ReadableLocalData();
                    else
                        data = Elegiast.ReadableAll();

                    if (command.Length == 1)
                        TheRoost.Sing(data);
                    else
                        TheRoost.Sing(data.Contains("\"" + command[1] + "\""));
                    return;
                }
                else if (command[0] == "achievements.reset")
                {
                    Elegiast.ClearAchievement(command[1]);
                    return;
                }

                string[] entityPath = command[0].Split('.');
                object entity = GetEntity(entityPath);
                if (entity == null)
                {
                    if (entityPath.Length == 1)
                        TheRoost.Sing("No GameObject '{0}' found", entityPath[0]);
                    else if (entityPath.Length == 2)
                        TheRoost.Sing("No Component '{0}' found on {1}", entityPath[1], entityPath[0]);
                }

                if (command.Length == 1)
                {
                    if (entity.GetType() == typeof(GameObject))
                        TheRoost.Sing((entity as GameObject).name, ((GameObject)entity).GetComponents(typeof(Component)));
                    else
                        foreach (PropertyInfo property in entity.GetType().GetProperties())
                            TheRoost.Sing(property.Name, property.GetValue(entity));
                    return;
                }

                PropertyInfo targetProperty = entity.GetType().GetProperty(command[1]);
                if (command.Length == 2) //no value specified, just return property
                {
                    TheRoost.Sing(targetProperty.GetValue(entity));
                    return;
                }

                string string_value = command[2];
                for (var n = 3; n < command.Length; n++)
                    string_value += " " + command[n];

                object value = ConvertValue(string_value, targetProperty);
                targetProperty.SetValue(entity, value);
            }
            catch
            {
                TheRoost.Sing("Error during executing command");
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

            TheRoost.Sing("Can't convert value {0} into {1}", value, property.PropertyType.Name);
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
                    TheRoost.Sing("No property '{0}' found", path[n]);
                    break;
                }
            }

            return result;
        }

        GameObject console;
        void Update()
        {
            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                //console.SetActive(!console.activeSelf);
                if (GameObject.FindObjectOfType<DebugTools>() == null)
                    Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
            }
        }
    }

    public class Delayer : MonoBehaviour
    {
        public static Delayer Schedule(System.Reflection.MethodInfo action, object actor = null, object[] parameters = null)
        {
            GameObject gameObject = new GameObject();
            DontDestroyOnLoad(gameObject);
            Delayer delayer = gameObject.AddComponent<Delayer>();
            delayer.StartCoroutine(delayer.ExecuteDelayed(action, actor, parameters));

            return delayer;
        }

        public IEnumerator ExecuteDelayed(System.Reflection.MethodInfo action, object actor, object[] parameters)
        {
            yield return new WaitForEndOfFrame();
            action.Invoke(actor, parameters);
            Destroy(this.gameObject);
        }
    }

}