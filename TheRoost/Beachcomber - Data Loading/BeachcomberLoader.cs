using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;

using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Fucine.DataImport;

using HarmonyLib;
using UnityEngine;

namespace TheRoost.Beachcomber
{
    internal static class CuckooLoader
    {
        private readonly static Dictionary<Type, Dictionary<string, Type>> knownUnknownProperties = new Dictionary<Type, Dictionary<string, Type>>();
        private readonly static Dictionary<Type, List<string>> localizableUnknownProperties = new Dictionary<Type, List<string>>();
        private readonly static Dictionary<IEntityWithId, Dictionary<string, object>> loadedPropertiesStorage = new Dictionary<IEntityWithId, Dictionary<string, object>>();

        private readonly static Dictionary<Type, List<string>> ignoredProperties = new Dictionary<Type, List<string>>();
        //NB entities can only be ignored with Usurper
        private readonly static Dictionary<Type, List<string>> ignoredEntityGroups = new Dictionary<Type, List<string>>();

        internal static void Enact()
        {
            //the most convenient place to catch and load simple properties that main game doesn't want, but mods do want is here
            Machine.Patch(
                original: typeof(AbstractEntity<Element>).GetMethodInvariant("PushUnknownProperty"),
                prefix: typeof(CuckooLoader).GetMethodInvariant("KnowUnknown"));
            //as things stand now, I can load custom properties directly from Usurper
            //but I'm leaving this separated as it's compatible with native loading, in case I'll cut the Usurper out at some point;
            //BeachcomberImporter stays independent of Usurper for the same reason

            //in a rare case we want some of our custom properties to be localizable
            //we ask the main game very gently to look it up for us
            Machine.Patch(
                original: typeof(EntityTypeDataLoader).GetMethodInvariant("GetLocalisableKeysForEntityType"),
                postfix: typeof(CuckooLoader).GetMethodInvariant("InsertCustomLocalizableKeys"));

            //sometimes, a single property just isn't enough; sometimes we want to inject the whole class so it's loaded on par with native entities
            //now, I really don't want to handle the loading of these custom classes
            //but the good thing that the game doesn't really care whether classes it loads are native or not, as long they are in The List
            //(The List and The Dictionary, if you are being pedantic)
            //so we plant all custom classes marked as FucineImportable from all loaded mods into the List and so the game now courteously loads them for us
            //the operation requires a bit of :knock: since we need to modify local variables of PopulateCompendium()
            //the the result well worth it
            Machine.Patch(
                original: typeof(CompendiumLoader).GetMethodInvariant("PopulateCompendium"),
                transpiler: typeof(CuckooLoader).GetMethodInvariant("CuckooTranspiler"));
        }

        internal static void ClaimProperty<TEntity, TProperty>(string propertyName, bool localize) where TEntity : AbstractEntity<TEntity>
        {
            Type entityType = typeof(TEntity);
            Type propertyType = typeof(TProperty);

            if (typeof(AbstractEntity<TEntity>).IsAssignableFrom(typeof(TEntity)) == false)
            {
                Birdsong.Sing("Trying to claim property '{0}' for type {1}, but {1} doesn't derive from AbstractEntity and thus can't not be loaded.", propertyName, entityType.Name);
                return;
            }

            if (knownUnknownProperties.ContainsKey(entityType) == false)
                knownUnknownProperties.Add(entityType, new Dictionary<string, Type>());

            if (knownUnknownProperties[entityType].ContainsKey(propertyName.ToLower()))
            {
                Birdsong.Sing("Trying to claim property '{0}' for type {1}, but the property of the same name for the same type is already claimed (it's ok when happens on disabling/enabling modules though)", propertyName, entityType.Name);
                return;
            }

            knownUnknownProperties[entityType].Add(propertyName.ToLower(), propertyType);

            if (localize)
            {
                if (localizableUnknownProperties.ContainsKey(entityType) == false)
                    localizableUnknownProperties[entityType] = new List<string>();

                localizableUnknownProperties[entityType].Add(propertyName);
            }
        }

        internal static T RetrieveProperty<T>(IEntityWithId owner, string propertyName)
        {
            propertyName = propertyName.ToLower();
            if (loadedPropertiesStorage.ContainsKey(owner) && loadedPropertiesStorage[owner].ContainsKey(propertyName))
                return (T)loadedPropertiesStorage[owner][propertyName];
            else
                return default(T);
        }

        internal static Dictionary<string, Type> GetCustomProperties(Type type)
        {
            if (knownUnknownProperties.ContainsKey(type) == false)
                return new Dictionary<string, Type>();

            return new Dictionary<string, Type>(knownUnknownProperties[type]);
        }

        internal static bool HasCustomProperty(IEntityWithId owner, string propertyName)
        {
            propertyName = propertyName.ToLower();
            return loadedPropertiesStorage.ContainsKey(owner) && loadedPropertiesStorage[owner].ContainsKey(propertyName);
        }

        internal static void AddIgnoredProperty<TEntity>(string propertyName)
        {
            if (ignoredProperties[typeof(TEntity)] == null)
                ignoredProperties[typeof(TEntity)] = new List<string>();

            if (ignoredProperties[typeof(TEntity)].Contains(propertyName) == false)
                ignoredProperties[typeof(TEntity)].Add(propertyName);
        }

        internal static void AddIgnoredEntityGroup<TEntity>(string groupId)
        {
            if (ignoredEntityGroups[typeof(TEntity)] == null)
                ignoredEntityGroups[typeof(TEntity)] = new List<string>();

            if (ignoredEntityGroups[typeof(TEntity)].Contains(groupId) == false)
                ignoredEntityGroups[typeof(TEntity)].Add(groupId);
        }

        internal static bool isIgnoredEntity(this EntityData data, Type type)
        {
            if (data.ValuesTable.Contains("ignoreGroups"))
            {
                ArrayList entityGroups = data.ValuesTable["ignoreGroups"] as ArrayList;
                foreach (string entityGroup in entityGroups)
                    if (ignoredEntityGroups[type].Contains(entityGroup))
                        return true;
            }

            return false;
        }

        private static bool KnowUnknown(IEntityWithId __instance, object key, object value)
        {
            IEntityWithId entity = __instance;
            Type entityType = entity.GetType();
            string propertyName = key.ToString();

            if (knownUnknownProperties.ContainsKey(entityType) && knownUnknownProperties[entityType].ContainsKey(propertyName))
            {
                if (ignoredProperties.ContainsKey(entityType) && ignoredProperties[entityType].Contains(propertyName))
                {
                    Birdsong.Sing(VerbosityLevel.SystemChatter, 0, "Known-Unknown - but ignored - property '{0}' for '{1}' {2}", propertyName, entity.Id, entityType.Name);
                    return false;
                }

                Birdsong.Sing(VerbosityLevel.SystemChatter, 0, "Known-Unknown property '{0}' for '{1}' {2}", propertyName, entity.Id, entityType.Name);
                object importedValue = Panimporter.ImportProperty(entity, value, knownUnknownProperties[entityType][propertyName], propertyName);

                if (loadedPropertiesStorage.ContainsKey(entity) == false)
                    loadedPropertiesStorage[entity] = new Dictionary<string, object>();
                loadedPropertiesStorage[entity].Add(propertyName, importedValue);

                return false;
            }

            return true;
        }

        private static void InsertCustomLocalizableKeys(EntityTypeDataLoader __instance, HashSet<string> __result)
        {
            if (localizableUnknownProperties.ContainsKey(__instance.EntityType))
                foreach (string property in localizableUnknownProperties[__instance.EntityType])
                    __result.Add(property);
        }

        private static IEnumerable<CodeInstruction> CuckooTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            MethodInfo injectingMyCodeRightBeforeThisMethod = typeof(Compendium).GetMethodInvariant("InitialiseForEntityTypes");

            for (int i = 0; i < codes.Count; i++)
                if (codes[i].Calls(injectingMyCodeRightBeforeThisMethod))
                {
                    i -= 2; //injecting the code two lines above of InitialiseForEntityTypes() is called 
                    //(those two lines are argument loading for InitialiseForEntityTypes)

                    //all I do here is locate several local variables (and one instance's private), load them as arguments
                    //and lastly invoke InsertCustomTypesForLoading() method with these as its arguments

                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, 2)); //list of loadable types (local)

                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, 1)); //dictionary of entity loaders (local)

                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_2)); //culture id (argument)    

                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_0)); //instance itself (is needed to locate its private variable next)
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldfld, //locating instance's private variable _log
                        typeof(CompendiumLoader).GetFieldInvariant("_log")));

                    //finally, calling InsertCustomTypesForLoading() with all of these arguments
                    codes.Insert(i++, new CodeInstruction(OpCodes.Call, typeof(CuckooLoader).GetMethodInvariant("InsertCustomTypesForLoading")));

                    break;
                }

            return codes.AsEnumerable();
        }

        //not sure *why* we need refs for types that are reference anyway, but we *need* them (otherwise error (no joke, don't delete refs!!!(/srs)!!))
        private static void InsertCustomTypesForLoading(ref List<Type> typesToLoad, ref Dictionary<string, EntityTypeDataLoader> fucineLoaders, string cultureId, ContentImportLog log)
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
            if (assembly.IsDynamic)
                return true;

            string assemblyLocation = assembly.Location.Replace('/', '\\');
            return (assemblyLocation.Contains(modLocationLocal) || assemblyLocation.Contains(modLocationWorkshop));
        }
    }
}

namespace TheRoost
{
    public partial class Machine
    {
        public static void ClaimProperty<TEntity, TProperty>(string propertyName, bool localize = false) where TEntity : AbstractEntity<TEntity>
        {
            Beachcomber.CuckooLoader.ClaimProperty<TEntity, TProperty>(propertyName, localize);
        }

        public static T RetrieveProperty<T>(this IEntityWithId owner, string propertyName)
        {
            return Beachcomber.CuckooLoader.RetrieveProperty<T>(owner, propertyName);
        }

        public static Dictionary<string, Type> GetCustomProperties(Type type)
        {
            return Beachcomber.CuckooLoader.GetCustomProperties(type);
        }

        public static bool HasCustomProperty(this IEntityWithId owner, string propertyName)
        {
            return Beachcomber.CuckooLoader.HasCustomProperty(owner, propertyName);
        }

        public static void AddIgnoredProperty<T>(string propertyName)
        {
            Beachcomber.CuckooLoader.AddIgnoredProperty<T>(propertyName);
        }

        public static void AddIgnoredEntityGroup<T>(string propertyName)
        {
            Beachcomber.CuckooLoader.AddIgnoredEntityGroup<T>(propertyName);
        }
    }
}