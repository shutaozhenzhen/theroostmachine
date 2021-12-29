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

namespace TheRoost.Beachcomber
{
    public static class CustomLoader
    {
        private readonly static Dictionary<Type, Dictionary<string, Type>> knownUnknownProperties = new Dictionary<Type, Dictionary<string, Type>>();
        private readonly static Dictionary<IEntityWithId, Dictionary<string, object>> beachcomberStorage = new Dictionary<IEntityWithId, Dictionary<string, object>>();

        internal static void Claim()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            TheRoostMachine.Patch(
                original: typeof(AbstractEntity<Element>).GetMethod("PopUnknownKeysToLog", BindingFlags.NonPublic | BindingFlags.Instance),
                prefix: typeof(CustomLoader).GetMethod("KnowUnknown", BindingFlags.NonPublic | BindingFlags.Static));

            TheRoostMachine.Patch(
                original: typeof(CompendiumLoader).GetMethod("PopulateCompendium"),
                transpiler: typeof(CustomLoader).GetMethod("CuckooTranspiler", BindingFlags.NonPublic | BindingFlags.Static));

            Usurper.OverthrowNativeImporting();
        }

        internal static void ClaimProperty<TEntity, TProperty>(string propertyName) where TEntity : AbstractEntity<TEntity>
        {
            Type entityType = typeof(TEntity);
            Type propertyType = typeof(TProperty);
            if (entityType.GetCustomAttribute(typeof(FucineImportable), false) == null)
            {
                Birdsong.Sing("Trying to claim '{0}' of type {1}, but {1} has no FucineImportable attribute and will not be loaded.", propertyName, entityType.Name);
                //(actually it totally will)
                return;
            }

            if (knownUnknownProperties.ContainsKey(entityType) == false)
                knownUnknownProperties.Add(entityType, new Dictionary<string, Type>());

            if (knownUnknownProperties[entityType].ContainsKey(propertyName.ToLower()))
            {
                Birdsong.Sing("Trying to claim '0' of type {1}, but the property of the same name for the same type is already claimed", propertyName, entityType.Name);
                return;
            }

            knownUnknownProperties[entityType].Add(propertyName.ToLower(), propertyType);
        }

        internal static T RetrieveProperty<T>(IEntityWithId owner, string propertyName)
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

                            object value = CustomImporter.ImportProperty(__instance, propertiesToComb[propertyName], propertiesToClaim[propertyName]);
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
                    //(those two lines are argument loading for InitialiseForEntityTypes)

                    //all I do here is locate several local variables (and one instance's private), load them as arguments
                    //and lastly invoke Beachcomber.Cuckoo method with these as its arguments
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, 2)); //list of loadable types (local)
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, 1)); //dictionary of entity loaders (local)
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_2)); //culture id (argument)    
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_0)); //instance itself (is needed to locate its private variable next)
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldfld, //locating instance's private variable _log
                        typeof(CompendiumLoader).GetField("_log", BindingFlags.Instance | BindingFlags.NonPublic)));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Call, //calling Beachcomber.Cuckoo() with all of these
                        typeof(CustomLoader).GetMethod("Cuckoo", BindingFlags.Static | BindingFlags.NonPublic)));

                    break;
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

        private static readonly string modLocationLocal = Application.persistentDataPath.Replace('/', '\\');
        private const string modLocationWorkshop = "steamapps\\workshop\\content";
        private static bool isModAssembly(this Assembly assembly)
        {
            string assemblyLocation = assembly.Location.Replace('/', '\\');
            return (assemblyLocation.Contains(modLocationLocal) || assemblyLocation.Contains(modLocationWorkshop));
        }
    }

    //leaving this one public in case someone/me will need to load something with the methods
    public static class CustomImporter
    {
        delegate object Importer(IEntityWithId parentEntity, object valueData, Type propertyType);
        static Importer GetImporterForType(Type type)
        {
            if (typeof(IList).IsAssignableFrom(type))
                return ImportList;
            else if (typeof(IDictionary).IsAssignableFrom(type))
                return ImportDictionary;
            else if (typeof(IEntityWithId).IsAssignableFrom(type))
                return ImportFucineEntity;
            else if (type.IsValueType && !type.IsEnum && type.Namespace != "System")
                return ImportStruct;

            return ImportSimpleValue;
        }

        public static object ImportProperty(IEntityWithId parentEntity, object valueData, Type propertyType)
        {
            Importer importer = GetImporterForType(propertyType);
            object propertyValue = importer.Invoke(parentEntity, valueData, propertyType);

            if (propertyValue == null)
                Birdsong.Sing("Failed to load property for {1} id '{0}'", parentEntity.Id, parentEntity.GetType().Name);

            return propertyValue;
        }

        public static IList ImportList(IEntityWithId parentEntity, object data, Type listType)
        {
            IList list = FactoryInstantiator.CreateObjectWithDefaultConstructor(listType) as IList;

            while (listType.IsGenericType == false)
                listType = listType.BaseType;

            Type expectedEntryType = listType.GetGenericArguments()[0];
            Importer entryImporter = GetImporterForType(expectedEntryType);

            try
            {
                ArrayList dataList = data as ArrayList;

                if (dataList == null)
                {
                    object importedSingularEntry = entryImporter.Invoke(parentEntity, data, expectedEntryType);
                    list.Add(importedSingularEntry);
                }
                else foreach (object entry in dataList)
                    {
                        object importedEntry = entryImporter.Invoke(parentEntity, entry, expectedEntryType);
                        list.Add(importedEntry);
                    }
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex.ToString());
                Birdsong.Sing("Unable to import list in {1} id '{0}', skipping", parentEntity.Id, parentEntity.GetType().Name);
            }

            return list;
        }

        public static IDictionary ImportDictionary(IEntityWithId parentEntity, object data, Type dictionaryType)
        {
            IDictionary dictionary = FactoryInstantiator.CreateObjectWithDefaultConstructor(dictionaryType) as IDictionary;

            while (dictionaryType.IsGenericType == false)
                dictionaryType = dictionaryType.BaseType;

            Type dictionaryKeyType = dictionaryType.GetGenericArguments()[0];
            Importer keyImporter = GetImporterForType(dictionaryKeyType);

            Type dictionaryValueType = dictionaryType.GetGenericArguments()[1];
            Importer valueImporter = GetImporterForType(dictionaryValueType);

            try
            {
                EntityData entityData = data as EntityData;

                foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                {
                    object key = keyImporter.Invoke(parentEntity, dictionaryEntry.Key, dictionaryKeyType);
                    object value = valueImporter.Invoke(parentEntity, dictionaryEntry.Value, dictionaryValueType);
                    dictionary.Add(key, value);
                }
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex.ToString());
                Birdsong.Sing("Unable to import dictionary in {1} id '{0}', skipping", parentEntity.Id, parentEntity.GetType().Name);
            }

            return dictionary;
        }

        public static IEntityWithId ImportFucineEntity(IEntityWithId parentEntity, object entityData, Type entityType)
        {
            try
            {
                EntityData fullSpecEntityData = entityData as EntityData;

                if (fullSpecEntityData != null)
                {
                    IEntityWithId entity = FactoryInstantiator.CreateEntity(entityType, entityData as EntityData, null);
                    return entity;
                }
                else if (entityType.GetInterfaces().Contains(typeof(IQuickSpecEntity)))
                {
                    IQuickSpecEntity quickSpecEntity = FactoryInstantiator.CreateObjectWithDefaultConstructor(entityType) as IQuickSpecEntity;
                    quickSpecEntity.QuickSpec(entityData.ToString());
                    return quickSpecEntity as IEntityWithId;
                }
                else
                    Birdsong.Sing("Entity data for {0} isn't a dictionary, and the entity isn't quick spec", entityType.Name);
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex.ToString());
                Birdsong.Sing("Unable to import {0} in {2} id '{1}', skipping", entityType.Name, parentEntity.Id, parentEntity.GetType().Name);
            }

            return null;
        }

        public static object ImportSimpleValue(IEntityWithId parentEntity, object entityData, Type valueType)
        {
            try
            {
                return TypeDescriptor.GetConverter(valueType).ConvertFromInvariantString(entityData.ToString());
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex.ToString());
                Birdsong.Sing("Unable to parse value {0} in {1} id '{2}', skipping", entityData.ToString(), parentEntity.GetType().Name, parentEntity.Id);
                return 0;
            }
        }

        public static object ImportStruct(IEntityWithId parentEntity, object structData, Type structType)
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
                Birdsong.Sing(ex.ToString());
                Birdsong.Sing("Unable to parse struct in {0} id '{1}', skipping", parentEntity.GetType().Name, parentEntity.Id);
                return null;
            }
        }
    }

    public static class Usurper
    {
        public static void OverthrowNativeImporting()
        {
            ///this little thing below actually hijacks the entirety of the CS loading proccess and replaces it all with Beachcomber's pipeline;
            ///the original thing has a... history, which makes it powerful in some regards but decrepit in others
            ///it can load QuickSpec entities only if they are contained in the ***Dictionary<string,List<IQuickSpecEntity>>*** (hilarious)
            ///can't load structs (and therefore, my FucineExpressions)
            ///its values loading sometimes hardcoded etc etc
            ///still, there's no much reason to overthrow it entirely (save from sport) - most of the edge cases will never be required anyway
            ///nobody probably will ever write a root fucine class with expressions
            ///nobody will ever need a <float, bool> dictionary
            ///the struct loading is a mad enterprise in particular
            ///the sport was good though
            TheRoostMachine.Patch(
                typeof(AbstractEntity<>).MakeGenericType(new Type[] { typeof(Verb) }).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0],
                transpiler: typeof(Usurper).GetMethod("ImportTranspiler", BindingFlags.NonPublic | BindingFlags.Static));

            //and another little essay, this time more on theme:
            //patching generics is tricky - the patch is applied to the whole generic class/method
            //it's somewhat convenient for me since I can patch only a single AbstractEntity<T> for the patch to apply to all of them
            //it's also somewhat inconvenient since I can't patch the .ctor directly with my own generic method
            //thus, I have to create an intermediary - InvokeGenericImporterForAbstractRootEntity() - which will, in turn, call the real generic for the type
            //(generics are needed to mimic CS's own structure and since it makes accessing properties much more easierester)
        }

        private static IEnumerable<CodeInstruction> ImportTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            MethodInfo injectingMyCodeRightBeforeThisMethod = typeof(Compendium).GetMethod("InitialiseForEntityTypes");
            var finalCodes = new List<CodeInstruction>();
            for (int i = 0; i < codes.Count; i++)
            {
                finalCodes.Add(codes[i]);
                if (codes[i].opcode == OpCodes.Call)
                    break;
            }

            ///transpiler is very simple this time - we just wait until the native code does the actual object creation
            ///after it's done, we call InvokeGenericImporterForAbstractRootEntity() to modify the object as we please
            ///all other native transmutations are skipped
            finalCodes.Add(new CodeInstruction(OpCodes.Ldarg_0));
            finalCodes.Add(new CodeInstruction(OpCodes.Ldarg_1));
            finalCodes.Add(new CodeInstruction(OpCodes.Call,
                       typeof(Usurper).GetMethod("InvokeGenericImporterForAbstractRootEntity", BindingFlags.Static | BindingFlags.NonPublic)));
            finalCodes.Add(new CodeInstruction(OpCodes.Ret));

            return finalCodes.AsEnumerable();
        }

        private static readonly Dictionary<Type, MethodInfo> genericImportMethods = new Dictionary<Type, MethodInfo>();
        private static void InvokeGenericImporterForAbstractRootEntity(IEntityWithId entity, EntityData entityData)
        {
            Type type = entity.GetType();

            if (genericImportMethods.ContainsKey(type) == false)
                genericImportMethods.Add(type, typeof(Usurper).GetMethod("ImportRootEntity").MakeGenericMethod(new Type[] { type }));
            genericImportMethods[type].Invoke(entity, new object[] { entity, entityData });
        }

        public static void ImportRootEntity<T>(IEntityWithId entity, EntityData entityData) where T : AbstractEntity<T>
        {
            //it makes everything a bit more hacky but I want id to be set first for the possible logs
            if (entityData.ValuesTable.ContainsKey("id"))
            {
                entity.SetId(entityData.Id);
                entityData.ValuesTable.Remove("id");
            }

            foreach (CachedFucineProperty<T> cachedProperty in TypeInfoCache<T>.GetCachedFucinePropertiesForType())
                if (cachedProperty.LowerCaseName != "id")
                {
                    string propertyName = cachedProperty.LowerCaseName;
                    Type propertyType = cachedProperty.ThisPropInfo.PropertyType;

                    object propertyValue;
                    if (entityData.ValuesTable.Contains(propertyName))
                    {
                        propertyValue = CustomImporter.ImportProperty(entity, entityData.ValuesTable[propertyName], propertyType);
                        entityData.ValuesTable.Remove(propertyName);
                    }
                    else
                    {
                        //lists, dicts and entities should always be created as an empty object at least (the main game does the same)
                        //Expulsion in LinkedRecipeDetails;
                        //MorphEffectType in MorphEffectDetails
                        //Internal Deck in Recipe
                        if (typeof(IList).IsAssignableFrom(propertyType)
                            || typeof(IDictionary).IsAssignableFrom(propertyType)
                            || typeof(IEntityWithId).IsAssignableFrom(propertyType))
                            propertyValue = FactoryInstantiator.CreateObjectWithDefaultConstructor(propertyType);
                        else
                            propertyValue = cachedProperty.FucineAttribute.DefaultValue;
                    }

                    cachedProperty.SetViaFastInvoke(entity as T, propertyValue);
                }

            foreach (object key in entityData.ValuesTable.Keys)
                (entity as AbstractEntity<T>).PushUnknownProperty(key, entityData.ValuesTable[key]);
        }
    }
}