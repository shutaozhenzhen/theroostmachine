using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;

using HarmonyLib;

using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Fucine.DataImport;

using UnityEngine;

namespace TheRoost
{
    public static class Beachcomber
    {
        private readonly static Dictionary<Type, Dictionary<string, Type>> knownUnknownProperties = new Dictionary<Type, Dictionary<string, Type>>();
        private readonly static Dictionary<IEntityWithId, Dictionary<string, object>> beachcomberStorage = new Dictionary<IEntityWithId, Dictionary<string, object>>();

        internal static void Claim()
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

        public static void ClaimProperty<TEntity, TProperty>(string propertyName) where TEntity : AbstractEntity<TEntity>
        {
            Type entityType = typeof(TEntity);
            Type propertyType = typeof(TProperty);
            if (entityType.GetCustomAttribute(typeof(FucineImportable), false) == null)
            {
                Birdsong.Sing("Trying to claim '{0}' of {1}s, but {1} has no FucineImportable attribute and will not be loaded.", propertyName, entityType.Name);
                return;
            }

            if (knownUnknownProperties.ContainsKey(entityType) == false)
                knownUnknownProperties[entityType] = new Dictionary<string, Type>();

            knownUnknownProperties[entityType].Add(propertyName.ToLower(), propertyType);
        }

        public static T RetrieveProperty<T>(this IEntityWithId owner, string propertyName)
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

        //not sure *why* we need refs here, but we *need* them (otherwise error (no joke, don't delete refs!!!!!))
        private static void Cuckoo(ref List<Type> typesToLoad, ref Dictionary<string, EntityTypeDataLoader> fucineLoaders, string cultureId, ContentImportLog log)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.isModAssembly())
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
        }

        static readonly string modLocationLocal = Application.persistentDataPath.Replace('/', '\\');
        const string modLocationWorkshop = "steamapps\\workshop\\content";
        static bool isModAssembly(this Assembly assembly)
        {
            string assemblyLocation = assembly.Location.Replace('/', '\\');
            return (assemblyLocation.Contains(modLocationLocal) || assemblyLocation.Contains(modLocationWorkshop));
        }
    }

    public static class BeachcomberImporter
    {
        delegate object Importer(IEntityWithId parentEntity, object valueData, Type propertyType, ContentImportLog log);
        static Importer GetImporterForType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return ImportList;
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return ImportDictionary;
            else if (type.IsClass && type.GetInterfaces().Contains(typeof(IEntityWithId)))
                return ImportFucineAbstractEntity;
            else if (type.IsValueType && !type.IsEnum && type.Namespace != "System")
                return ImportStruct;

            return ImportSimpleValue;
        }

        public static object ImportProperty(IEntityWithId parentEntity, object valueData, Type propertyType, ContentImportLog log)
        {
            Importer importer = GetImporterForType(propertyType);
            object propertyValue = importer.Invoke(parentEntity, valueData, propertyType, log);

            if (propertyValue == null)
                log.cawk("Failed to load custom property for {1} id '{0}'", parentEntity.Id, parentEntity.GetType().Name);

            return propertyValue;
        }

        public static IList ImportList(IEntityWithId parentEntity, object data, Type listType, ContentImportLog log)
        {
            IList list = FactoryInstantiator.CreateObjectWithDefaultConstructor(listType) as IList;

            Type expectedEntryType = listType.GetGenericArguments()[0];
            Importer entryImporter = GetImporterForType(expectedEntryType);

            try
            {
                ArrayList dataList = data as ArrayList;

                foreach (object entry in dataList)
                {
                    object importedValue = entryImporter.Invoke(parentEntity, entry, expectedEntryType, log);
                    list.Add(importedValue);
                }
            }
            catch (Exception ex)
            {
                log.cawk(ex.ToString());
                log.cawk("Unable to import list in {1} id '{0}', skipping", parentEntity.Id, parentEntity.GetType().Name);
            }

            return list;
        }

        public static IDictionary ImportDictionary(IEntityWithId parentEntity, object data, Type dictionaryType, ContentImportLog log)
        {
            IDictionary dictionary = FactoryInstantiator.CreateObjectWithDefaultConstructor(dictionaryType) as IDictionary;

            Type dictionaryKeyType = dictionaryType.GetGenericArguments()[0];
            Importer keyImporter = GetImporterForType(dictionaryKeyType);

            Type dictionaryValueType = dictionaryType.GetGenericArguments()[1];
            Importer valueImporter = GetImporterForType(dictionaryValueType);

            try
            {
                EntityData entityData = data as EntityData;

                foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                {
                    object key = keyImporter.Invoke(parentEntity, dictionaryEntry.Key, dictionaryKeyType, log);
                    object value = valueImporter.Invoke(parentEntity, dictionaryEntry.Value, dictionaryValueType, log);
                    dictionary.Add(key, value);
                }
            }
            catch (Exception ex)
            {
                log.cawk(ex.ToString());
                log.cawk("Unable to import dictionary in {1} id '{0}', skipping", parentEntity.Id, parentEntity.GetType().Name);
            }

            return dictionary;
        }

        public static IEntityWithId ImportFucineAbstractEntity(IEntityWithId parentEntity, object entityData, Type entityType, ContentImportLog log)
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
                log.cawk(ex.ToString());
                log.cawk("Unable to import {0} in {2} id '{1}', skipping", entityType.Name, parentEntity.Id, parentEntity.GetType().Name);
            }

            return null;
        }

        public static object ImportSimpleValue(IEntityWithId parentEntity, object entityData, Type valueType, ContentImportLog log)
        {
            try
            {
                return TypeDescriptor.GetConverter(valueType).ConvertFromInvariantString(entityData.ToString());
            }
            catch (Exception ex)
            {
                log.cawk(ex.ToString());
                log.cawk("Unable to parse value {0} in {1} id '{2}', skipping", entityData.ToString(), parentEntity.GetType().Name, parentEntity.Id);
                return 0;
            }
        }

        public static object ImportStruct(IEntityWithId parentEntity, object structData, Type structType, ContentImportLog log)
        {
            try
            {
                EntityData entityData = structData as EntityData;

                if (entityData == null) //trying to construct with single string constructor
                {
                    ConstructorInfo constructor = structType.GetConstructor(new Type[] { typeof(string) });
                    return constructor.Invoke(new object[] { structData.ToString() });
                }
                else //if it's a valid entity data, searching for a matching constructor (not really trying though)
                {
                    var ctor = structType.GetConstructors()[0];

                    string[] paramNames = ctor.GetParameters().Select(p => p.Name).ToArray();
                    object[] parameters = new object[paramNames.Length];
                    for (int i = 0; i < parameters.Length; ++i)
                    {
                        parameters[i] = Type.Missing;
                    }
                    foreach (DictionaryEntry item in entityData.ValuesTable)
                    {
                        var paramName = item.Key;
                        var paramIndex = Array.IndexOf(paramNames, paramName);
                        if (paramIndex >= 0)
                        {
                            parameters[paramIndex] = item.Value;
                        }
                    }

                    return ctor.Invoke(parameters);
                }
            }
            catch (Exception ex)
            {
                log.cawk(ex.ToString());
                log.cawk("Unable to parse struct in {0} id '{1}', skipping", parentEntity.GetType().Name, parentEntity.Id);
                return null;
            }
        }

        private static void cawk(this ContentImportLog log, string format, params object[] args)
        {
            log.LogProblem(String.Format(format, args));
        }
    }
}