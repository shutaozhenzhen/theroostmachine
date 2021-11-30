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

using TheRoost.Entities;
using UnityEngine;

namespace TheRoost
{
    public class Beachcomber
    {
        private readonly static Dictionary<Type, Dictionary<string, Type>> knownUnknownProperties = new Dictionary<Type, Dictionary<string, Type>>();
        private readonly static Dictionary<IEntityWithId, Dictionary<string, object>> beachcomberStorage = new Dictionary<IEntityWithId, Dictionary<string, object>>();

        internal static void Enact()
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
                Birdsong.Sing("Trying to claim '{0}' of {1}s, but {1} has no FucineImportable attribute and will not be loaded.", propertyName, entityType.Name);
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

                            object value = BeachcomberImporter.ImportProperty(__instance, propertiesToComb[propertyName], propertiesToClaim[propertyName], log);
                            if (value != null)
                                beachcomberStorage[__instance].Add(propertyName, value);
                        }
            }
        }

        private static IEnumerable<CodeInstruction> CuckooTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            MethodInfo injectingMyCodeRightBeforeThisMethod = typeof(Compendium).GetMethod("InitialiseForEntityTypes");

            for (int i = 0; i < codes.Count; i++)
                if (codes[i].Calls(injectingMyCodeRightBeforeThisMethod))
                {
                    i -= 2; //injecting the code two lines above of InitialiseForEntityTypes() is called 
                    //(those two lines ar argument loading for InitialiseForEntityTypes)

                    //all I do here is locate several local variables (and one instance's private), load them as arguments
                    //and lastly invoke Beachcomber.Cuckoo method with these as its arguments
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, 2)); //list of loadable types (local)
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, 1)); //dictionary of entity loaders (local)
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_2)); //culture id (argument)    
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_0)); //instance itself (is needed to locate its private variable next)
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldfld, //locating instance's private variable _log
                        typeof(CompendiumLoader).GetField("_log", BindingFlags.Instance | BindingFlags.NonPublic)));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Call, //calling Beachcomber.Cuckoo() with all of these
                        typeof(Beachcomber).GetMethod("Cuckoo", BindingFlags.Static | BindingFlags.NonPublic)));

                    return codes.AsEnumerable();
                }

            return codes.AsEnumerable();
        }

        //not sure *why* we need refs here, but we *need* them (otherwise error)
        private static void Cuckoo(ref List<Type> typesToLoad, ref Dictionary<string, EntityTypeDataLoader> fucineLoaders, string cultureId, ContentImportLog log)
        {
            var mods = from mod in Watchman.Get<SecretHistories.Constants.Modding.ModManager>().GetEnabledMods()
                       select System.Text.RegularExpressions.Regex.Replace(mod.Name, "[^a-zA-Z0-9_]+", "");

            foreach (string mod in mods)
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    if (assembly.GetType(mod) != null)
                    {
                        foreach (Type type in assembly.GetTypes())
                            if (typesToLoad.Contains(type) == false)
                            {
                                FucineImportable fucineImportable = (FucineImportable)type.GetCustomAttribute(typeof(FucineImportable), false);
                                if (fucineImportable != null)
                                {
                                    typesToLoad.Add(type);
                                    fucineLoaders.Add(fucineImportable.TaggedAs.ToLower(), new EntityTypeDataLoader(type, fucineImportable.TaggedAs, cultureId, log));
                                }
                            }

                        break;
                    }
        }
    }

    public static class BeachcomberImporter
    {
        public static object ImportProperty(IEntityWithId parentEntity, object valueData, Type propertyType, ContentImportLog log)
        {
            object propertyValue = null;

            ImportType importType = GetImportType(propertyType);
            switch (importType)
            {
                case ImportType.Value:
                    propertyValue = ImportDataType(parentEntity, valueData, propertyType, log);
                    break;
                case ImportType.FucineEntity:
                    propertyValue = ImportEntity(parentEntity, valueData, propertyType, log);
                    break;
                case ImportType.List:
                    propertyValue = ImportList(parentEntity, valueData, propertyType, log);
                    break;
                case ImportType.Dictionary:
                    propertyValue = ImportDictionary(parentEntity, valueData, propertyType, log);
                    break;
                case ImportType.Expression:
                    propertyValue = ImportExpression(parentEntity, valueData, propertyType, log);
                    break;
            }

            if (propertyValue == null)
                log.cawk("Failed to load custom property for {1} id '{0}'", parentEntity.Id, parentEntity.GetType().Name);

            return propertyValue;
        }

        public static IList ImportList(IEntityWithId parentEntity, object data, Type listType, ContentImportLog log)
        {
            IList list = FactoryInstantiator.CreateObjectWithDefaultConstructor(listType) as IList;
            Type expectedEntryType = listType.GetGenericArguments()[0];

            try
            {
                ArrayList dataList = data as ArrayList;

                switch (GetImportType(expectedEntryType))
                {
                    case ImportType.Value:
                        foreach (object entry in dataList)
                            list.Add(ImportDataType(parentEntity, entry, expectedEntryType, log));
                        break;
                    case ImportType.FucineEntity:
                        foreach (object entry in dataList)
                            list.Add(ImportEntity(parentEntity, entry, expectedEntryType, log));
                        break;
                    case ImportType.List:
                        foreach (ArrayList entry in dataList)
                            list.Add(ImportList(parentEntity, entry, expectedEntryType, log));
                        break;
                    case ImportType.Dictionary:
                        foreach (EntityData entry in dataList)
                            list.Add(ImportDictionary(parentEntity, entry, expectedEntryType, log));
                        break;
                    case ImportType.Expression:
                        foreach (object entry in dataList)
                            list.Add(ImportExpression(parentEntity, entry, expectedEntryType, log));
                        break;
                }
            }
            catch (Exception ex)
            {
                log.cawk(ex);
                log.cawk("List in {1} id '{0}' is wrong format, skipping", parentEntity.Id, parentEntity.GetType().Name);
            }

            return list;
        }

        public static IDictionary ImportDictionary(IEntityWithId parentEntity, object data, Type dictionaryType, ContentImportLog log)
        {
            IDictionary dictionary = FactoryInstantiator.CreateObjectWithDefaultConstructor(dictionaryType) as IDictionary;
            Type dictionaryKeyType = dictionaryType.GetGenericArguments()[0];
            Type dictionaryValueType = dictionaryType.GetGenericArguments()[1];

            try
            {
                EntityData entityData = data as EntityData;

                switch (GetImportType(dictionaryValueType))
                {
                    case ImportType.Value:
                        foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                            dictionary.Add(
                                ImportDataType(parentEntity, dictionaryEntry.Key, dictionaryKeyType, log),
                                ImportDataType(parentEntity, dictionaryEntry.Value, dictionaryKeyType, log));
                        break;
                    case ImportType.FucineEntity:
                        foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                        {
                            IEntityWithId entity = ImportEntity(parentEntity, dictionaryEntry.Value, dictionaryValueType, log);
                            dictionary.Add(ImportDataType(parentEntity, dictionaryEntry.Key, dictionaryKeyType, log), entity);
                        }
                        break;
                    case ImportType.List:
                        foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                        {
                            IList nestedList = ImportList(parentEntity, dictionaryEntry.Value as ArrayList, dictionaryValueType, log);
                            dictionary.Add(ImportDataType(parentEntity, dictionaryEntry.Key, dictionaryKeyType, log), nestedList);
                        }
                        break;
                    case ImportType.Dictionary:
                        foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                        {
                            IDictionary nestedDictionary = ImportDictionary(parentEntity, dictionaryEntry.Value as EntityData, dictionaryValueType, log);
                            dictionary.Add(ImportDataType(parentEntity, dictionaryEntry.Key, dictionaryKeyType, log), nestedDictionary);
                        }
                        break;
                    case ImportType.Expression:
                        foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                        {
                            var fucineexpression = ImportExpression(parentEntity, dictionaryEntry.Value, dictionaryValueType, log);
                            dictionary.Add(ImportDataType(parentEntity, dictionaryEntry.Key, dictionaryKeyType, log), fucineexpression);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                log.cawk(ex);
                log.cawk("Unable to import dictionary in {1} id '{0}', skipping", parentEntity.Id, parentEntity.GetType().Name);
            }

            return dictionary;
        }

        public static object ImportDataType(IEntityWithId parentEntity, object entityData, Type entityType, ContentImportLog log)
        {
            try
            {
                return Convert.ChangeType(entityData.ToString(), entityType);
            }
            catch (Exception ex)
            {
                log.cawk(ex);
                log.cawk("Unable to parse value in {1} id '{0}', skipping", parentEntity.Id, parentEntity.GetType().Name);
                return null;
            }
        }

        public static IEntityWithId ImportEntity(IEntityWithId parentEntity, object entityData, Type entityType, ContentImportLog log)
        {
            try
            {
                EntityData fullSpecEntityData = entityData as EntityData;

                if (entityData != null)
                {
                    IEntityWithId entity = FactoryInstantiator.CreateEntity(entityType, entityData as EntityData, log);
                    return entity;
                }
                else if (entityType.GetInterfaces().Contains(typeof(IQuickSpecEntity)))
                {
                    IQuickSpecEntity quickSpecEntity = FactoryInstantiator.CreateObjectWithDefaultConstructor(entityType) as IQuickSpecEntity;
                    quickSpecEntity.QuickSpec(entityData.ToString());
                    return quickSpecEntity as IEntityWithId;
                }
            }
            catch (Exception ex)
            {
                log.cawk(ex);
                log.cawk("Unable to import {0} in {2} id '{1}', skipping", entityType.Name, parentEntity.Id, parentEntity.GetType().Name);
            }

            return null;
        }

        public static object ImportExpression(IEntityWithId parentEntity, object entityData, Type entityType, ContentImportLog log)
        {
            try
            {
                return entityType.GetConstructor(new Type[] { typeof(string) }).Invoke(new object[] { entityData.ToString() });
            }
            catch (Exception ex)
            {
                log.cawk(ex);
                log.cawk("Unable to parse expression in {1} id '{0}', skipping", parentEntity.Id, parentEntity.GetType().Name);
                return null;
            }
        }

        enum ImportType { Value, List, Dictionary, FucineEntity, Expression };
        static ImportType GetImportType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return ImportType.List;
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return ImportType.Dictionary;
            else if (type.Namespace == "System")
                return ImportType.Value;
            else if ((FucineExpression)type.GetCustomAttribute(typeof(FucineExpression), false) != null)
                return ImportType.Expression;

            return ImportType.FucineEntity;
        }

        private static void cawk(this ContentImportLog log, string format, params object[] args)
        {
            log.LogProblem(String.Format(format, args));
        }

        private static void cawk(this ContentImportLog logger, object obj)
        {
            logger.LogProblem(obj.ToString());
        }
    }
}