using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;

using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Fucine.DataImport;

public static class Beachcomber
{
    public static Dictionary<Type, Dictionary<string, Type>> knownUnknownProperties;
    public static Dictionary<IEntityWithId, Dictionary<string, object>> storage;

    public static void Initialise()
    {
        storage = new Dictionary<IEntityWithId, Dictionary<string, object>>();

        var harmony = new Harmony("beachcomber");
        var original = typeof(AbstractEntity<Element>).GetMethod("PopUnknownKeysToLog", BindingFlags.NonPublic | BindingFlags.Instance);
        var patched = typeof(Beachcomber).GetMethod("KnowUnknown", BindingFlags.NonPublic | BindingFlags.Static);
        harmony.Patch(original, prefix: new HarmonyMethod(patched));

        ///natively, verbs don't have "comments" property - let's add it just for test/show off
        MarkPropertyAsOwned(typeof(Verb), "comments", typeof(string));
    }

    private static void KnowUnknown(IEntityWithId __instance, Hashtable ___UnknownProperties, ContentImportLog log)
    {
        Hashtable propertiesToComb = new Hashtable(___UnknownProperties);
        Dictionary<string, Type> propertiesToClaim = knownUnknownProperties[__instance.GetType()];

        foreach (string propertyName in propertiesToComb.Keys)
            if (propertiesToClaim.ContainsKey(propertyName))
            {
                log.LogInfo(String.Format("Known-Unknown property '{0}' for '{1}' {2}", propertyName, __instance.Id, __instance.GetType().Name));
                FormatAndStoreProperty(__instance, propertyName, propertiesToComb[propertyName], propertiesToClaim[propertyName], log);
                ___UnknownProperties.Remove(propertyName);
            }
    }

    private static void FormatAndStoreProperty(IEntityWithId entity, string propertyName, object propertyValue, Type propertyType, ContentImportLog log)
    {
        if (storage.ContainsKey(entity) == false)
            storage[entity] = new Dictionary<string, object>();

        object value = LoadTools.QuickImporter.LoadValue(entity, propertyName, propertyValue, propertyType, log);
        if (value != null)
            storage[entity].Add(propertyName, value);
    }

    public static void MarkPropertyAsOwned(Type entityType, string propertyName, Type propertyType)
    {
        if (knownUnknownProperties == null)
        {
            knownUnknownProperties = new Dictionary<Type, Dictionary<string, Type>>();
            foreach (Type vanillaType in loadableEntities)
                knownUnknownProperties[vanillaType] = new Dictionary<string, Type>();
        }

        knownUnknownProperties[entityType].Add(propertyName, propertyType);
    }

    public static T GetProperty<T>(this IEntityWithId owner, string property)
    {
        if (storage.ContainsKey(owner) && storage[owner].ContainsKey(property))
            return (T)storage[owner][property];
        else
            return default(T);
    }

    private static readonly Type[] loadableEntities = { 
                                                        typeof(AngelSpecification), 
                                                        typeof(Culture), 
                                                        typeof(DeckSpec), 
                                                        typeof(Dictum), 
                                                        typeof(Element), 
                                                        typeof(Ending), 
                                                        typeof(Expulsion), 
                                                        typeof(Legacy),
                                                        typeof(LinkedRecipeDetails), 
                                                        typeof(MorphDetails), 
                                                        typeof(MutationEffect), 
                                                        typeof(Portal), 
                                                        typeof(Recipe), 
                                                        typeof(Setting), 
                                                        typeof(SphereSpec), 
                                                        typeof(Verb),
                                                      };
}


namespace LoadTools
{
    public static class QuickImporter
    {
        public static object LoadValue(IEntityWithId baseEntity, string propertyName, object valueData, Type propertyType, ContentImportLog log)
        {
            object propertyValue = null;

            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
                propertyValue = LoadTools.QuickImporter.LoadList(baseEntity, propertyName, valueData, propertyType, log);
            else if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                propertyValue = LoadTools.QuickImporter.LoadDictionary(baseEntity, propertyName, valueData, propertyType, log);
            else if (propertyType.Namespace == "System")
                propertyValue = Convert.ChangeType(valueData, propertyType);
            else
                propertyValue = FactoryInstantiator.CreateEntity(propertyType, valueData as EntityData, log);

            if (propertyValue == null)
                log.LogWarning(String.Format("Failed to load custom property '{0}' for '{1}' {2}", propertyName, baseEntity.Id, baseEntity.GetType().Name));

            return propertyValue;
        }

        public static IList LoadList(IEntityWithId baseEntity, string propertyName, object data, Type listType, ContentImportLog log)
        {
            ArrayList dataList = data as ArrayList;
            if (dataList == null)
            {
                log.LogWarning(String.Format("'{0}' list in '{1}' {2} is wrong format, skip loading", propertyName, baseEntity.Id, baseEntity.GetType().Name));
                return null;
            }

            IList list = FactoryInstantiator.CreateObjectWithDefaultConstructor(listType) as IList;
            Type expectedEntryType = listType.GetGenericArguments()[0];

            if (expectedEntryType.IsGenericType && expectedEntryType.GetGenericTypeDefinition() == typeof(List<>))
            {
                foreach (ArrayList entry in dataList)
                    list.Add(LoadList(baseEntity, propertyName, entry, expectedEntryType, log));
            }
            else if (expectedEntryType.IsGenericType && expectedEntryType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                foreach (EntityData entry in dataList)
                    list.Add(LoadDictionary(baseEntity, propertyName, entry, expectedEntryType, log));
            else if (expectedEntryType.Namespace == "System")
                foreach (object entry in dataList)
                    list.Add(Convert.ChangeType(entry.ToString(), expectedEntryType));
            else
                foreach (EntityData entry in dataList)
                    list.Add(FactoryInstantiator.CreateEntity(expectedEntryType, entry, log));

            return list;
        }

        public static IDictionary LoadDictionary(IEntityWithId baseEntity, string propertyName, object data, Type dictionaryType, ContentImportLog log)
        {
            EntityData entityData = data as EntityData;
            if (entityData == null)
            {
                log.LogWarning(String.Format("'{0}' dictionary in '{1}' {2} is wrong format, skip loading", propertyName, baseEntity.Id, baseEntity.GetType().Name));
                return null;
            }

            IDictionary dictionary = FactoryInstantiator.CreateObjectWithDefaultConstructor(dictionaryType) as IDictionary;
            Type dictionaryKeyType = dictionaryType.GetGenericArguments()[0];
            Type dictionaryValueType = dictionaryType.GetGenericArguments()[1];

            if (dictionaryValueType.IsGenericType && dictionaryValueType.GetGenericTypeDefinition() == typeof(List<>))
                foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                {
                    IList list = LoadList(baseEntity, dictionaryEntry.Key.ToString(), dictionaryEntry.Value as ArrayList, dictionaryValueType, log);
                    dictionary.Add(Convert.ChangeType(dictionaryEntry.Key.ToString(), dictionaryKeyType), list);
                }
            else if (dictionaryValueType.IsGenericType && dictionaryValueType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                {
                    IDictionary nestedDictionary = LoadDictionary(baseEntity, dictionaryEntry.Key.ToString(), dictionaryEntry.Value as EntityData, dictionaryValueType, log);
                    dictionary.Add(Convert.ChangeType(dictionaryEntry.Key.ToString(), dictionaryKeyType), nestedDictionary);
                }
            else if (dictionaryValueType.Namespace == "System")
                foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                    dictionary.Add(Convert.ChangeType(dictionaryEntry.Key.ToString(), dictionaryKeyType), Convert.ChangeType(dictionaryEntry.Value.ToString(), dictionaryValueType));
            else
                foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                {
                    IEntityWithId entity = FactoryInstantiator.CreateEntity(dictionaryValueType, dictionaryEntry.Value as EntityData, log);
                    dictionary.Add(Convert.ChangeType(dictionaryEntry.Key.ToString(), dictionaryKeyType), entity);
                }


            return dictionary;
        }
    }
}