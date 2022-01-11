using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using SecretHistories.UI;
using SecretHistories.Services;
using SecretHistories.Fucine;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace TheRoost.Vagabond
{
    public class CommandLine : MonoBehaviour
    {
        internal static void Enter()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            AtTimeOfPower.MainMenuLoaded.Schedule(CreateCommandLine, PatchType.Postfix);

            TheRoostMachine.Patch(
                original: typeof(SecretHistories.UI.NotificationWindow).GetMethod("SetDetails", BindingFlags.Public | BindingFlags.Instance),
                prefix: typeof(CommandsCollection).GetMethod("ShowNotificationWithIntervention", BindingFlags.NonPublic | BindingFlags.Static));

            AddCommand("reimport", CommandsCollection.Reimport);
            AddCommand("compendium", CommandsCollection.CompendiumInfo);
            AddCommand("unity", CommandsCollection.GameObjectCommand);
        }

        static Dictionary<string, Action<string[]>> commandMethods = new Dictionary<string, Action<string[]>>();
        public static void AddCommand(string reference, Action<string[]> method)
        {
            commandMethods.Add(reference, method);
        }

        private static void ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command) || command[0] != '/')
            {
                Birdsong.Sing("Commands should always start with /");
                return;
            }

            command = command.Substring(1);
            string[] commandParts = command.Split();

            if (commandMethods.ContainsKey(commandParts[0]))
            {
                string[] arguments = new string[commandParts.Length - 1];
                Array.Copy(commandParts, 1, arguments, 0, commandParts.Length - 1);
                commandMethods[commandParts[0]].Invoke(arguments);
                return;
            }

            Birdsong.Sing("Unknown command '{0}'", command[0]);
        }

        GameObject console;
        void Update()
        {
            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
                if (GameObject.FindObjectOfType<DebugTools>() == null)
                    Watchman.Get<Concursum>().ToggleSecretHistory();
        }

        private static void CreateCommandLine()
        {
            bool consoleEnabled = GameObject.Find("SecretHistoryLogMessageEntry(Clone)") != null;
            if (consoleEnabled == false)
                Watchman.Get<Concursum>().ToggleSecretHistory();

            if (GameObject.FindObjectOfType<CommandLine>() != null)
            {
                Watchman.Get<Concursum>().ToggleSecretHistory();
                return;
            }

            var debugCanvas = GameObject.Find("SecretHistoryLogMessageEntry(Clone)").GetComponentInParent<Canvas>().transform;
            CommandLine vagabond = GameObject.FindObjectOfType<SecretHistory>().gameObject.AddComponent<CommandLine>();
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

    public static class CommandsCollection
    {
        public static void Reimport(string[] command)
        {
            Watchman.Get<SecretHistories.Constants.Modding.ModManager>().CatalogueMods();
            Compendium compendium = Watchman.Get<Compendium>();
            CompendiumLoader loader = new CompendiumLoader(Watchman.Get<Config>().GetConfigValue("contentdir"));
            DateTime now = DateTime.Now;
            foreach (SecretHistories.Fucine.ILogMessage logMessage in loader.PopulateCompendium(compendium, Watchman.Get<Config>().GetConfigValue("Culture")).GetMessages())
                Birdsong.Sing(VerbosityLevel.Trivia, logMessage.Description);
            Birdsong.Sing("Total time to import: {0}", (DateTime.Now - now));
        }

        public static void CompendiumInfo(string[] command)
        {
            Compendium compendium = Watchman.Get<Compendium>();
            try
            {
                Type entityType = compendium.GetEntityTypes().FirstOrDefault(type => type.Name.ToLower() == command[0].ToLower());

                if (entityType == null)
                    throw new Exception(String.Format("Entity type {0} not found", command[0]));

                if (command.Length == 1)
                {
                    object list = typeof(Compendium).GetMethod("GetEntitiesAsAlphabetisedList").MakeGenericMethod(new Type[] { entityType }).Invoke(compendium, new object[0]);

                    Birdsong.Sing("All entities of type {0}\n~~~", entityType.Name, command[1]);
                    foreach (IEntityWithId entityWithId in list as IList)
                        Birdsong.Sing(entityWithId.Id, entityWithId.Lever == null);
                    Birdsong.Sing("~~~~~~~");
                    return;
                }

                IEntityWithId entity = typeof(Compendium).GetMethod("GetEntityById").MakeGenericMethod(new Type[] { entityType }).Invoke(compendium, new object[] { command[1] }) as IEntityWithId;

                if (entity == null || entity.Lever == null)
                    throw new Exception(String.Format("{0} '{1}' not found", entityType.Name, command[1]));

                if (command.Length == 2)
                {
                    Birdsong.Sing("All properties of {0} id '{1}\n~~~", entityType.Name, command[1]);
                    foreach (PropertyInfo prop in entity.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                        Birdsong.Sing(prop.Name, prop.GetValue(entity));
                    Birdsong.Sing("~~~~~~~");
                    return;
                }

                string propertyName = command[2].ToLower().First().ToString().ToUpper() + command[2].Substring(1);
                PropertyInfo property = entity.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property != null)
                {
                    Birdsong.Sing("{0} of {1} '{2}': {3}", propertyName, entityType.Name, entity.Id, property.GetValue(entity));
                    return;
                }

                if (entity.HasCustomProperty(propertyName))
                {
                    Birdsong.Sing("Custom property '{0}' of {1} '{2}': {3}", propertyName, entityType.Name, entity.Id, entity.RetrieveProperty(propertyName));
                    return;
                }

                throw new Exception(String.Format("Property '{0}' of {1} id '{2}' not found", command[2], entityType.Name, entity.Id));
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex.Message);
            }
        }

        public static void GameObjectCommand(string[] command)
        {
            try
            {
                string[] entityPath = command[0].Split('.');
                UnityEngine.Object unityObject = GetUnityObject(entityPath);

                //if only object path is specified we return either all Components (for GameObject) or all properties (for Component)
                if (command.Length == 1)
                {
                    if (unityObject.GetType() == typeof(GameObject))
                    {
                        Birdsong.Sing("All Components of GameObject {0}\n~~~~~~~", unityObject.name);
                        foreach (Component component in (unityObject as GameObject).GetComponents(typeof(Component)))
                            Birdsong.Sing(component.GetType().Name);
                    }
                    else
                    {
                        Birdsong.Sing("All properties of {0}\n~~~~~~~", unityObject);
                        foreach (PropertyInfo property in unityObject.GetType().GetProperties())
                            Birdsong.Sing(property.Name, property.GetValue(unityObject));
                    }
                    return;
                }

                PropertyInfo targetProperty = unityObject.GetType().GetProperty(command[1]);

                if (targetProperty == null)
                    throw new Exception(String.Format("Property {0} is not found in {1}", command[1], unityObject));

                if (command.Length == 2) //no set value specified, just return current property value
                {
                    Birdsong.Sing("{0} of {1}: {2}", targetProperty.Name, unityObject.name, targetProperty.GetValue(unityObject));
                    return;
                }

                string stringValue = command[2];
                for (var n = 3; n < command.Length; n++)
                    stringValue += " " + command[n];

                object value = Birdsong.ConvertValue(stringValue, targetProperty.PropertyType);
                targetProperty.SetValue(unityObject, value);
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex);
            }
        }

        private static UnityEngine.Object GetUnityObject(string[] path)
        {
            GameObject go = Birdsong.FindGameObject(path[0], true);

            if (go == null)
                throw new Exception(String.Format("No GameObject '{0}' found", path[0]));

            if (path.Length == 1)
                return go;

            int i = 1;
            while (path[i] == "child" || path[i] == "parent")
            {
                if (path[i] == "child")
                {
                    go = go.transform.GetChild(int.Parse(path[i + 1])).gameObject;
                    i += 2;
                }
                else if (path[i] == "parent")
                {
                    go = go.transform.parent.gameObject;
                    i++;
                }

                if (go == null)
                    throw new Exception(String.Format("No sufficient GameObject relative to '{0}' was found", path[0]));
                else if (i >= path.Length)
                    return go;
            }

            if (i < path.Length - 1)
                throw new Exception("UnityObject definition string is too long, don't know what to do with that; trying to return a component");

            UnityEngine.Component component = go.GetComponent(path[i]);

            if (component == null)
                throw new Exception(String.Format("GameObject '{0}' doesn't have {1} as one of its Components", go.name, path[i]));

            return component;
        }
    }
}