using System;
using System.Collections.Generic;
using System.Reflection;

using SecretHistories.UI;
using SecretHistories.Services;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace TheRoost
{
    public class Vagabond : MonoBehaviour
    {
        internal static void Enter()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            AtTimeOfPower.MainMenuLoaded.Schedule(CreateCommandLine, PatchType.Postfix);

            AddCommand("/goinfo", GameObjectCommand);
            AddCommand("/goset", GameObjectCommand);
            AddCommand("/reimport", Reimport);
        }

        static void Reimport(string[] command)
        {
            try
            {
                Watchman.Get<SecretHistories.Constants.Modding.ModManager>().CatalogueMods();
                Compendium compendiumToPopulate = Watchman.Get<Compendium>();
                CompendiumLoader compendiumLoader = new CompendiumLoader(Watchman.Get<Config>().GetConfigValue("contentdir"));
                DateTime now = DateTime.Now;
                foreach (SecretHistories.Fucine.ILogMessage logMessage in compendiumLoader.PopulateCompendium(compendiumToPopulate, Watchman.Get<Config>().GetConfigValue("Culture")).GetMessages())
                {
                    Birdsong.Sing(logMessage.Description);
                }
                Birdsong.Sing("Total time to import: {0}", (DateTime.Now - now));
            }
            catch(Exception ex)
            {
                Birdsong.Sing(ex);
            }
        }


        static Dictionary<string, Action<string[]>> commandMethods = new Dictionary<string, Action<string[]>>();
        public static void AddCommand(string reference, Action<string[]> method)
        {
            commandMethods.Add(reference, method);
        }

        GameObject console;
        void Update()
        {
            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
                if (GameObject.FindObjectOfType<DebugTools>() == null)
                    Watchman.Get<Concursum>().ToggleSecretHistory();
        }

        private static void ExecuteCommand(string text)
        {
            string[] command = text.Split();

            if (commandMethods.ContainsKey(command[0]))
            {
                string[] arguments = new string[command.Length - 1];
                Array.Copy(command, 1, arguments, 0, command.Length - 1);
                commandMethods[command[0]].Invoke(arguments);
                return;
            }

            Birdsong.Sing("Unknown command");
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
                        Birdsong.Sing("No GameObject '{0}' found", entityPath[0]);
                    else if (entityPath.Length == 2)
                        Birdsong.Sing("No Component '{0}' found on {1}", entityPath[1], entityPath[0]);
                }

                if (command.Length == 1)
                {
                    if (entity.GetType() == typeof(GameObject))
                        Birdsong.Sing((entity as GameObject).name, ((GameObject)entity).GetComponents(typeof(Component)));
                    else
                        foreach (PropertyInfo property in entity.GetType().GetProperties())
                            Birdsong.Sing(property.Name, property.GetValue(entity));
                    return;
                }

                PropertyInfo targetProperty = entity.GetType().GetProperty(command[1]);
                if (command.Length == 2) //no value specified, just return property
                {
                    Birdsong.Sing(targetProperty.GetValue(entity));
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
                Birdsong.Sing(ex);
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

            Birdsong.Sing("Can't convert value {0} into {1}", value, property.PropertyType.Name);
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
                    Birdsong.Sing("No property '{0}' found", path[n]);
                    break;
                }
            }

            return result;
        }

        private static void CreateCommandLine()
        {
            bool consoleEnabled = GameObject.Find("SecretHistoryLogMessageEntry(Clone)") != null;
            if (consoleEnabled == false)
                Watchman.Get<Concursum>().ToggleSecretHistory();

            if (GameObject.FindObjectOfType<Vagabond>() != null)
            {
                Watchman.Get<Concursum>().ToggleSecretHistory();
                return;
            }

            var debugCanvas = GameObject.Find("SecretHistoryLogMessageEntry(Clone)").GetComponentInParent<Canvas>().transform;
            Vagabond vagabond = GameObject.FindObjectOfType<SecretHistory>().gameObject.AddComponent<Vagabond>();
            vagabond.console = new GameObject("AuxConsole", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));

            vagabond.console.GetComponent<Image>().color = debugCanvas.GetComponentsInChildren<Image>()[1].color;

            var consoleT = vagabond.console.GetComponent<RectTransform>();
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

            TMP_InputField consoleI = vagabond.console.GetComponent<TMP_InputField>();
            consoleI.targetGraphic = vagabond.console.GetComponent<Image>();
            consoleI.textComponent = text;
            consoleI.text = "...";

            consoleI.onSubmit = new TMP_InputField.SubmitEvent();
            consoleI.onSubmit.AddListener(new UnityEngine.Events.UnityAction<string>(ExecuteCommand));

            Watchman.Get<Concursum>().ToggleSecretHistory();
        }
    }
}