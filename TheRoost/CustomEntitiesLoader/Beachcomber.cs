using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;

using HarmonyLib;

using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;

using TheRoost;
using UnityEngine;

namespace TheRoost
{
    public class Beachcomber
    {
        private readonly static Dictionary<Type, Dictionary<string, Type>> knownUnknownProperties = new Dictionary<Type, Dictionary<string, Type>>();
        private readonly static Dictionary<IEntityWithId, Dictionary<string, object>> beachcomberStorage = new Dictionary<IEntityWithId, Dictionary<string, object>>();
        private readonly static List<Type> cuckooEggs = new List<Type>();

        private static void Invoke()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            TheRoostMachine.Patch(
                original: typeof(AbstractEntity<Element>).GetMethod("PopUnknownKeysToLog", BindingFlags.NonPublic | BindingFlags.Instance),
                prefix: typeof(Beachcomber).GetMethod("KnowUnknown", BindingFlags.NonPublic | BindingFlags.Static));

            TheRoostMachine.Patch(
                original: typeof(CompendiumLoader).GetMethod("PopulateCompendium"),
                transpiler: typeof(Beachcomber).GetMethod("CuckooTranspiler", BindingFlags.NonPublic | BindingFlags.Static));
        }

        public static void ClaimProperty<T>(string propertyName, Type propertyType) where T : AbstractEntity<T>
        {
            Type entityType = typeof(T);
            if (entityType.GetCustomAttribute(typeof(FucineImportable), false) == null)
            {
                Twins.Sing("Trying to claim '{0}' of {1}s, but {1} has no FucineImportable attribute and will not be loaded.", propertyName, entityType.Name);
                return;
            }

            if (knownUnknownProperties.ContainsKey(entityType) == false)
                knownUnknownProperties[entityType] = new Dictionary<string, Type>();

            knownUnknownProperties[entityType].Add(propertyName.ToLower(), propertyType);
        }

        public static T RetrieveProperty<T>(IEntityWithId owner, string propertyName)
        {
            propertyName = propertyName.ToLower();
            if (beachcomberStorage.ContainsKey(owner) && beachcomberStorage[owner].ContainsKey(propertyName))
                return (T)beachcomberStorage[owner][propertyName];
            else
                return default(T);
        }

        public static void InfectFucineWith<T>() where T : AbstractEntity<T>
        {
            cuckooEggs.Add(typeof(T));
        }

        private static void KnowUnknown(IEntityWithId __instance, Hashtable ___UnknownProperties, ContentImportLog log)
        {
            if (knownUnknownProperties.ContainsKey(__instance.GetType()))
            {
                Hashtable propertiesToComb = new Hashtable(___UnknownProperties);
                Dictionary<string, Type> propertiesToClaim = knownUnknownProperties[__instance.GetType()];

                if (propertiesToClaim.Count > 0)
                    foreach (string propertyName in propertiesToComb.Keys)
                        if (propertiesToClaim.ContainsKey(propertyName))
                        {
                            log.LogInfo(String.Format("Known-Unknown property '{0}' for '{1}' {2}", propertyName, __instance.Id, __instance.GetType().Name));
                            ___UnknownProperties.Remove(propertyName);

                            if (beachcomberStorage.ContainsKey(__instance) == false)
                                beachcomberStorage[__instance] = new Dictionary<string, object>();

                            object value = BeachcomberImporter.LoadValue(__instance, propertiesToComb[propertyName], propertiesToClaim[propertyName], log);
                            if (value != null)
                                beachcomberStorage[__instance].Add(propertyName, value);
                        }
            }
        }

        private static IEnumerable<CodeInstruction> CuckooTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            MethodInfo targetMethod = typeof(Compendium).GetMethod("InitialiseForEntityTypes");
            MethodInfo cuckoo = typeof(Beachcomber).GetMethod("Cuckoo", BindingFlags.Static | BindingFlags.NonPublic);

            for (int i = 0; i < codes.Count; i++)
                if (codes[i].Calls(targetMethod))
                {
                    i -= 2;

                    //should've left those comments that explained what exactly I do there
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, 2));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, 1));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_2));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_0));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldfld, typeof(CompendiumLoader).GetField("_log", BindingFlags.Instance | BindingFlags.NonPublic)));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Call, cuckoo));

                    return codes.AsEnumerable();
                }

            return codes.AsEnumerable();
        }

        private static void Cuckoo(ref List<Type> nativeFucineClasses, ref Dictionary<string, EntityTypeDataLoader> fucineLoaders, string cultureId, ContentImportLog log)
        {
            foreach (Type customFucineClass in cuckooEggs)
            {
                nativeFucineClasses.Add(customFucineClass);
                FucineImportable fucineImportable = (FucineImportable)customFucineClass.GetCustomAttribute(typeof(FucineImportable), false);
                fucineLoaders.Add(fucineImportable.TaggedAs.ToLower(), new EntityTypeDataLoader(customFucineClass, fucineImportable.TaggedAs, cultureId, log));
            }
        }
    }

    public static class BeachcomberImporter
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
                Twins.Sing("Failed to load custom property for '{0}' {1}", baseEntity.Id, baseEntity.GetType().Name);

            return propertyValue;
        }

        public static IList LoadList(IEntityWithId baseEntity, object data, Type listType, ContentImportLog log)
        {
            ArrayList dataList = data as ArrayList;
            if (dataList == null)
            {
                Twins.Sing("List in '{0}' {1} is wrong format, skip loading", baseEntity.Id, baseEntity.GetType().Name);
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
                Twins.Sing("Dictionary in '{0}' {1} is wrong format, skip loading", baseEntity.Id, baseEntity.GetType().Name);
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
}