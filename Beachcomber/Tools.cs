using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

using HarmonyLib;

using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;

using UnityEngine;


namespace Hoard.Tools
{
    public static class Importer
    {
        public static object LoadValue(IEntityWithId baseEntity, object valueData, Type propertyType, ContentImportLog log)
        {
            object propertyValue = null;

            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
                propertyValue = LoadList(baseEntity, valueData, propertyType, log);
            else if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                propertyValue = LoadDictionary(baseEntity, valueData, propertyType, log);
            else if (propertyType.Namespace == "System")
                propertyValue = Convert.ChangeType(valueData, propertyType);
            else
                propertyValue = FactoryInstantiator.CreateEntity(propertyType, valueData as EntityData, log);

            if (propertyValue == null)
                log.LogWarning(String.Format("Failed to load custom property for '{0}' {1}", baseEntity.Id, baseEntity.GetType().Name));

            return propertyValue;
        }

        public static IList LoadList(IEntityWithId baseEntity, object data, Type listType, ContentImportLog log)
        {
            ArrayList dataList = data as ArrayList;
            if (dataList == null)
            {
                log.LogWarning(String.Format("Custom property list in '{0}' {1} is wrong format, skip loading", baseEntity.Id, baseEntity.GetType().Name));
                return null;
            }

            IList list = FactoryInstantiator.CreateObjectWithDefaultConstructor(listType) as IList;
            Type expectedEntryType = listType.GetGenericArguments()[0];

            if (expectedEntryType.IsGenericType && expectedEntryType.GetGenericTypeDefinition() == typeof(List<>))
            {
                foreach (ArrayList entry in dataList)
                    list.Add(LoadList(baseEntity, entry, expectedEntryType, log));
            }
            else if (expectedEntryType.IsGenericType && expectedEntryType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                foreach (EntityData entry in dataList)
                    list.Add(LoadDictionary(baseEntity, entry, expectedEntryType, log));
            else if (expectedEntryType.Namespace == "System")
                foreach (object entry in dataList)
                    list.Add(Convert.ChangeType(entry.ToString(), expectedEntryType));
            else
                foreach (EntityData entry in dataList)
                    list.Add(FactoryInstantiator.CreateEntity(expectedEntryType, entry, log));

            return list;
        }

        public static IDictionary LoadDictionary(IEntityWithId baseEntity, object data, Type dictionaryType, ContentImportLog log)
        {
            EntityData entityData = data as EntityData;
            if (entityData == null)
            {
                log.LogWarning(String.Format("'{0}' dictionary in '{0}' {1} is wrong format, skip loading", baseEntity.Id, baseEntity.GetType().Name));
                return null;
            }

            IDictionary dictionary = FactoryInstantiator.CreateObjectWithDefaultConstructor(dictionaryType) as IDictionary;
            Type dictionaryKeyType = dictionaryType.GetGenericArguments()[0];
            Type dictionaryValueType = dictionaryType.GetGenericArguments()[1];

            if (dictionaryValueType.IsGenericType && dictionaryValueType.GetGenericTypeDefinition() == typeof(List<>))
                foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                {
                    IList list = LoadList(baseEntity, dictionaryEntry.Value as ArrayList, dictionaryValueType, log);
                    dictionary.Add(Convert.ChangeType(dictionaryEntry.Key.ToString(), dictionaryKeyType), list);
                }
            else if (dictionaryValueType.IsGenericType && dictionaryValueType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                {
                    IDictionary nestedDictionary = LoadDictionary(baseEntity, dictionaryEntry.Value as EntityData, dictionaryValueType, log);
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

    public class Delayer : MonoBehaviour
    {
        public static Delayer Schedule(MethodInfo action, object actor = null, object[] parameters = null)
        {
            GameObject gameObject = new GameObject();
            Delayer delayer = gameObject.AddComponent<Delayer>();
            delayer.StartCoroutine(delayer.ExecuteDelayed(action, actor, parameters));

            return delayer;
        }

        public IEnumerator ExecuteDelayed(MethodInfo action, object actor, object[] parameters)
        {
            yield return new WaitForEndOfFrame();
            action.Invoke(actor, parameters);
            Destroy(this.gameObject);
        }
    }
}