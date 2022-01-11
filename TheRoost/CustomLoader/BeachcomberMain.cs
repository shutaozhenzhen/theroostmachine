﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;

using SecretHistories.Fucine;
using SecretHistories.Entities;

using HarmonyLib;
using UnityEngine;

namespace TheRoost.Beachcomber
{
    public static class CustomLoader
    {
        private readonly static Dictionary<Type, Dictionary<string, Type>> knownUnknownProperties = new Dictionary<Type, Dictionary<string, Type>>();
        private readonly static Dictionary<Type, List<string>> localizableUnknownProperties = new Dictionary<Type, List<string>>();
        private readonly static Dictionary<IEntityWithId, Dictionary<string, object>> loadedPropertiesStorage = new Dictionary<IEntityWithId, Dictionary<string, object>>();

        internal static void Claim()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            //the most convenient place to catch and load simple properties that main game doesn't want, but mods do want is here
            TheRoostMachine.Patch(
                original: typeof(AbstractEntity<Element>).GetMethod("PopUnknownKeysToLog", BindingFlags.NonPublic | BindingFlags.Instance),
                prefix: typeof(CustomLoader).GetMethod("KnowUnknown", BindingFlags.NonPublic | BindingFlags.Static));
            //as things stand now, I can load custom properties directly from Usurper
            //but I'm leaving this separated as it's compatible with native loading, in case I'll cut the Usurper out at some point;
            //BeachcomberImporter stays independent of Usurper for the same reason

            //in a rare case we want some of our custom properties to be localizable
            //we ask the main game very gently to look it up for us
            TheRoostMachine.Patch(
                original: typeof(EntityTypeDataLoader).GetMethod("GetLocalisableKeysForEntityType", BindingFlags.NonPublic | BindingFlags.Instance),
                postfix: typeof(CustomLoader).GetMethod("InsertCustomLocalizableKeys", BindingFlags.NonPublic | BindingFlags.Static));

            //sometimes, a single property just isn't enough; sometimes we want to inject the whole class so it's loaded on par with native entities
            //now, I really don't want to handle the loading of these custom classes
            //but the good thing that the game doesn't really care whether classes it loads are native or not, as long they are in The List
            //(The List and The Dictionary, if you are being pedantic)
            //so we plant all custom classes marked as FucineImportable from all loaded mods into the List and so the game now courteously loads them for us
            //the operation requires a bit of :knock: since we need to modify local variables of PopulateCompendium()
            //the the result well worth it
            TheRoostMachine.Patch(
                original: typeof(CompendiumLoader).GetMethod("PopulateCompendium"),
                transpiler: typeof(CustomLoader).GetMethod("CuckooTranspiler", BindingFlags.NonPublic | BindingFlags.Static));

            //now, as a finishing touch, we just completely replace how the game handles importing
            //(well, json loading and thus localizing/merging/mod $ stays intact, actually, 
            //it's just the process of porting jsons into actual game entities that gets changed)
            Usurper.OverthrowNativeImporting();
        }

        internal static void ClaimProperty<TEntity, TProperty>(string propertyName, bool localize) where TEntity : AbstractEntity<TEntity>
        {
            Type entityType = typeof(TEntity);
            Type propertyType = typeof(TProperty);

            if (typeof(AbstractEntity<TEntity>).IsAssignableFrom(typeof(TEntity)) == false)
            {
                Birdsong.Sing("Trying to claim property '{0}' of {1}, but {1} has doesn't derive from AbstractEntity and thus can't not be loaded.", propertyName, entityType.Name);
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

            if (localize)
            {
                if (localizableUnknownProperties.ContainsKey(entityType) == false)
                    localizableUnknownProperties[entityType] = new List<string>();

                localizableUnknownProperties[entityType].Add(propertyName);
            }
        }

        internal static T RetrieveProperty<T>(IEntityWithId owner, string propertyName)
        {
            object result = RetrieveProperty(owner, propertyName);
            if (result == null)
                return default(T);
            else
                return (T)result;
        }

        internal static object RetrieveProperty(IEntityWithId owner, string propertyName)
        {
            propertyName = propertyName.ToLower();
            if (loadedPropertiesStorage.ContainsKey(owner) && loadedPropertiesStorage[owner].ContainsKey(propertyName))
                return loadedPropertiesStorage[owner][propertyName];
            else
                return null;
        }

        internal static bool hasCustomProperty(IEntityWithId owner, string propertyName)
        {
            propertyName = propertyName.ToLower();
            return (loadedPropertiesStorage.ContainsKey(owner) && loadedPropertiesStorage[owner].ContainsKey(propertyName));
        }

        private static void KnowUnknown(IEntityWithId __instance, Hashtable ___UnknownProperties)
        {
            IEntityWithId entity = __instance;

            if (knownUnknownProperties.ContainsKey(entity.GetType()))
            {
                Hashtable propertiesToComb = new Hashtable(___UnknownProperties);
                Dictionary<string, Type> propertiesToClaim = knownUnknownProperties[entity.GetType()];

                if (propertiesToClaim.Count > 0)
                    foreach (string propertyName in propertiesToComb.Keys)
                        if (propertiesToClaim.ContainsKey(propertyName))
                        {
                            Birdsong.Sing(VerbosityLevel.SystemChatter, 0, "Known-Unknown property '{0}' for '{1}' {2}", propertyName, entity.Id, entity.GetType().Name);

                            ___UnknownProperties.Remove(propertyName);

                            if (loadedPropertiesStorage.ContainsKey(entity) == false)
                                loadedPropertiesStorage[entity] = new Dictionary<string, object>();

                            try
                            {
                                object value = CustomImporter.ImportProperty(entity, propertiesToComb[propertyName], propertyName, propertiesToClaim[propertyName]);
                                loadedPropertiesStorage[entity].Add(propertyName, value);
                            }
                            catch
                            {
                                throw new Exception("FAILED TO IMPORT JSON");
                            }
                        }
            }
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
            if (assembly.IsDynamic)
                return true;

            string assemblyLocation = assembly.Location.Replace('/', '\\');
            return (assemblyLocation.Contains(modLocationLocal) || assemblyLocation.Contains(modLocationWorkshop));
        }
    }
}

namespace TheRoost
{
    public partial class TheRoostAccessExtensions
    {
        public static T RetrieveProperty<T>(this IEntityWithId owner, string propertyName)
        {
            return TheRoost.Beachcomber.CustomLoader.RetrieveProperty<T>(owner, propertyName);
        }

        public static object RetrieveProperty(this IEntityWithId owner, string propertyName)
        {
            return TheRoost.Beachcomber.CustomLoader.RetrieveProperty(owner, propertyName);
        }

        public static bool HasCustomProperty(this IEntityWithId owner, string propertyName)
        {
            return TheRoost.Beachcomber.CustomLoader.hasCustomProperty(owner, propertyName);
        }
    }
}