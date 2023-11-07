using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using SecretHistories.UI;
using SecretHistories.Services;
using SecretHistories.Fucine;
using SecretHistories.Infrastructure.Modding;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Roost.Vagabond
{
    public class CommandLine : MonoBehaviour
    {
        internal static void Enact()
        {
            AtTimeOfPower.MenuSceneInit.Schedule(CreateCommandLine, PatchType.Postfix);

            AddCommand("reimport", CommandsCollection.Reimport);
            AddCommand("compendium", CommandsCollection.CompendiumInfo);
            AddCommand("root", CommandsCollection.RootInfo);
            AddCommand("unity", CommandsCollection.UnityObjectCommand);
        }

        private static Dictionary<string, Action<string[]>> commandMethods = new Dictionary<string, Action<string[]>>();
        public static void AddCommand(string reference, Action<string[]> method)
        {
            if (commandMethods.ContainsKey(reference))
            {
                Birdsong.TweetLoud($"Trying to register command '{reference}', but it's already registered");
                return;
            }
            commandMethods.Add(reference, method);
        }

        private static void ExecuteCommand(string command)
        {
            command = command.Trim();
            if (string.IsNullOrWhiteSpace(command) || command[0] != '/')
            {
                CommandLine.Log("Commands should always start with /");
                return;
            }

            CommandLine.Log("executing command: '{0}'", command);

            command = command.Trim().Substring(1);
            string[] commandParts = command.Split();

            if (commandMethods.ContainsKey(commandParts[0]))
            {
                string[] arguments = new string[commandParts.Length - 1];
                Array.Copy(commandParts, 1, arguments, 0, commandParts.Length - 1);
                commandMethods[commandParts[0]].Invoke(arguments);
                return;
            }
            else
                foreach (Type type in Watchman.Get<Compendium>().GetEntityTypes())
                    if (type.Name.ToLower() == commandParts[0].ToLower())
                    {
                        CommandsCollection.CompendiumInfo(commandParts);
                        return;
                    }

            CommandLine.Log("Unknown command '{0}'", commandParts[0]);
        }

        private GameObject console;

        private static void CreateCommandLine()
        {
            if (GameObject.FindObjectOfType<CommandLine>() != null)
                return;

            GameObject OGLog = GameObject.Find("SecretHistory").transform.GetChild(0).gameObject;

            CommandLine vagabond = GameObject.FindObjectOfType<SecretHistory>().gameObject.AddComponent<CommandLine>();
            vagabond.console = new GameObject("AuxConsole", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));

            vagabond.console.GetComponent<Image>().color = OGLog.GetComponentsInChildren<Image>()[1].color;

            var consoleT = vagabond.console.GetComponent<RectTransform>();
            consoleT.SetParent(OGLog.transform.GetChild(0));
            consoleT.pivot = new Vector2(0.5f, 1f);
            consoleT.anchorMin = new Vector2(0.13f, 0f);
            consoleT.anchorMax = new Vector2(0.85f, 0f);
            consoleT.sizeDelta = new Vector2(0, 30);
            consoleT.anchoredPosition = new Vector2(0, 2);
            consoleT.localScale = Vector2.one;

            GameObject button = GameObject.Instantiate(OGLog.FindInChildren("Messages", true), consoleT);
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
        }

        public static void Log(object data, params object[] furtherData)
        {
            Birdsong.TweetLoud(data, furtherData);
        }
    }

    public static class CommandsCollection
    {
        public static void Reimport(string[] command)
        {
            Watchman.Get<ModManager>().CatalogueMods();
            Compendium compendium = Watchman.Get<Compendium>();
            CompendiumLoader loader = new CompendiumLoader(Watchman.Get<Config>().GetConfigValue("contentdir"));
            DateTime now = DateTime.Now;
            foreach (ILogMessage logMessage in loader.PopulateCompendium(compendium, Watchman.Get<Config>().GetConfigValue("Culture")).GetMessages())
                Birdsong.TweetLoud(logMessage.VerbosityNeeded, logMessage.MessageLevel, logMessage.Description);
            CommandLine.Log("Total time to import: {0}", (DateTime.Now - now));
        }

        public static void RootInfo(string[] command)
        {
            if (command.Length == 0)
            {
                CommandLine.Log(SecretHistories.Entities.FucineRoot.Get().Mutations.UnpackCollection());
                return;
            }

            if (command.Length == 1)
            {
                SecretHistories.Entities.FucineRoot.Get().Mutations.TryGetValue(command[0], out int result);
                CommandLine.Log($"root/{command[0]} = {result}");
                return;
            }

            CommandLine.Log($"Too many arguments");
        }

        public static void CompendiumInfo(string[] command)
        {
            Compendium compendium = Watchman.Get<Compendium>();
            try
            {
                if (command.Length == 0)
                {
                    foreach (Type type in compendium.GetEntityTypes())
                        CommandLine.Log(type.Name);
                    return;
                }

                Type entityType = compendium.GetEntityTypes().FirstOrDefault(type => type.Name.ToLower() == command[0].ToLower());

                if (entityType == null)
                {
                    CommandLine.Log("Entity type {0} not found", command[0]);
                    return;
                }

                if (command.Length == 1)
                {
                    object list = typeof(Compendium).GetMethod("GetEntitiesAsAlphabetisedList").MakeGenericMethod(new Type[] { entityType }).Invoke(compendium, new object[0]);

                    CommandLine.Log("All entities of type {0}\n~~~", entityType.Name, command[1]);
                    foreach (IEntityWithId entityWithId in list as IList)
                        CommandLine.Log(entityWithId.Id, entityWithId.Lever == null);
                    CommandLine.Log("~~~~~~~");
                    return;
                }

                IEntityWithId entity = typeof(Compendium).GetMethod("GetEntityById").MakeGenericMethod(new Type[] { entityType }).Invoke(compendium, new object[] { command[1] }) as IEntityWithId;

                if (entity == null || entity.Lever == null)
                {
                    CommandLine.Log("{0} '{1}' not found", entityType.Name, command[1]);
                    return;
                }

                if (command.Length == 2)
                {
                    CommandLine.Log("All properties of {0} id '{1}\n~~~", entityType.Name, command[1]);
                    foreach (PropertyInfo prop in entity.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                        CommandLine.Log(prop.Name, prop.GetValue(entity));
                    CommandLine.Log("~~~~~~~");
                    return;
                }

                string propertyName = command[2].ToLower().First().ToString().ToUpper() + command[2].Substring(1);
                PropertyInfo property = entity.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                object value;
                if (property != null)
                    value = property.GetValue(entity);
                else if (entity.HasCustomProperty(propertyName))
                    value = entity.RetrieveProperty(propertyName);
                else
                {
                    CommandLine.Log("Property '{0}' of {1} id '{2}' not found", command[2], entityType.Name, entity.Id);
                    return;
                }

                if (value is ICollection collection)
                    value = collection.UnpackCollection();

                CommandLine.Log("{0} of {1} '{2}': {3}", propertyName, entityType.Name, entity.Id, value);
            }
            catch (Exception ex)
            {
                CommandLine.Log(ex.Message);
            }
        }

        public static void UnityObjectCommand(string[] command)
        {
            try
            {
                if (command == null || command.Length == 0)
                {
                    foreach (GameObject go in GameObject.FindObjectsOfType(typeof(GameObject)))
                        CommandLine.Log(go.name);

                    return;
                }

                string[] entityPath = command[0].Split('.');
                UnityEngine.Object unityObject = GetUnityObject(entityPath);
                if (unityObject == null)
                {
                    CommandLine.Log("No GameObject defined as '{0}' found", entityPath);
                    return;
                }

                //if only object path is specified we return either all Components (for GameObject) or all properties (for Component)
                if (command.Length == 1)
                {
                    if (unityObject.GetType() == typeof(GameObject))
                        LogAllComponents(unityObject as GameObject);
                    else
                        LogAllProperties(unityObject as Component);

                    return;
                }

                PropertyInfo targetProperty = unityObject.GetType().GetProperty(command[1]);

                if (targetProperty == null)
                {
                    CommandLine.Log("Property {0} is not found in {1}", command[1], unityObject);
                    return;
                }

                if (command.Length == 2) //no set value specified, just return current property value
                {
                    CommandLine.Log("{0} of {1}: {2}", targetProperty.Name, unityObject.name, targetProperty.GetValue(unityObject));
                    return;
                }

                string stringValue = command[2];
                for (var n = 3; n < command.Length; n++)
                    stringValue += " " + command[n];

                object value = ImportMethods.ConvertValue(stringValue, targetProperty.PropertyType);
                targetProperty.SetValue(unityObject, value);
            }
            catch (Exception ex)
            {
                CommandLine.Log(ex);
            }
        }

        private static UnityEngine.Object GetUnityObject(string[] path)
        {
            GameObject go = Machine.FindGameObject(path[0], true);

            if (go == null)
                return null;

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
                {
                    CommandLine.Log("No sufficient GameObject relative to '{0}' was found", path[0]);
                    return null;
                }
                else if (i >= path.Length)
                    return go;
            }

            if (i < path.Length - 1)
                CommandLine.Log("UnityObject definition string is too long, don't know what to do with that; trying to return a component");

            UnityEngine.Component component = go.GetComponent(path[i]);

            if (component == null)
                CommandLine.Log("GameObject '{0}' doesn't have {1} as one of its Components", go.name, path[i]);

            return component;
        }

        public static void LogAllComponents(this GameObject unityObject)
        {
            CommandLine.Log("All Components of GameObject {0}\n~~~~~~~", unityObject.name);
            foreach (Component component in unityObject.GetComponents(typeof(Component)))
                CommandLine.Log(component.GetType().Name);
        }

        public static void LogAllProperties(this Component unityObject)
        {
            CommandLine.Log("All properties of {0}\n~~~~~~~", unityObject);
            foreach (PropertyInfo property in unityObject.GetType().GetProperties())
                CommandLine.Log(property.Name, property.GetValue(unityObject));
        }

        public static void LogAllChildren(this Transform transform)
        {
            CommandLine.Log("All children of {0}\n~~~~~~~", transform.gameObject);
            for (int n = 0; n < transform.childCount; n++)
                CommandLine.Log(transform.GetChild(n).gameObject.name);
        }
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static GameObject FindGameObject(string name, bool includeInactive)
        {
            //NB - case sensitive
            UnityEngine.GameObject result = GameObject.Find(name);

            if (result == null)
            {
                GameObject[] allGO = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject go in allGO)
                    if (go.name == name)
                        return go;
            }

            return result;
        }

        public static GameObject FindInChildren(this GameObject go, string name, bool nested = false)
        {
            //NB - case insensitive
            Transform transform = go.transform;
            for (int n = 0; n < transform.childCount; n++)
                if (String.Equals(transform.GetChild(n).name, name, StringComparison.OrdinalIgnoreCase))
                    return transform.GetChild(n).gameObject;
                else if (nested)
                {
                    GameObject nestedFound = FindInChildren(transform.GetChild(n).gameObject, name, true);
                    if (nestedFound != null)
                        return nestedFound;
                }

            return null;
        }
    }
}