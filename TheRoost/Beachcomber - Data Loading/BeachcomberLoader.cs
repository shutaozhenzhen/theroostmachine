﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Entities.NullEntities;
using SecretHistories.Services;

using HarmonyLib;
using UnityEngine;

namespace Roost.Beachcomber
{
    //class that ensures loading of custom properties and entities by planting them inside the game's loading pipeline
    internal static class Cuckoo
    {
        internal static void Enact()
        {
            //in a rare case we want some of our custom properties to be localizable
            //we ask the main game very politely to look it up for us
            Machine.Patch(
                original: typeof(EntityTypeDataLoader).GetMethodInvariant("GetLocalisableKeysForEntityType"),
                postfix: typeof(Cuckoo).GetMethodInvariant(nameof(InsertCustomLocalizableKeys)));

            //sometimes, a single property just isn't enough; sometimes we want to inject the whole class so it's loaded on par with native entities
            //now, I really don't want to handle the loading of these custom classes
            //but the good thing that the game doesn't really care whether classes it loads are native or not, as long they are in The List
            //(The List and The Dictionary, to be precise)
            //so we insert all the custom classes marked as FucineImportable from all the loaded mods into the List 
            //so the game now courteously loads them for us
            //the operation requires a bit of :knock: since we need to modify the local variables of PopulateCompendium(), but the result well worth it
            Machine.Patch(
                original: typeof(CompendiumLoader).GetMethodInvariant(nameof(CompendiumLoader.PopulateCompendium)),
                transpiler: typeof(Cuckoo).GetMethodInvariant(nameof(CuckooTranspiler)));
        }

        //EntityTypeDataLoader.GetLocalisableKeysForEntityType() postfix
        private static void InsertCustomLocalizableKeys(EntityTypeDataLoader __instance, HashSet<string> __result)
        {
            foreach (string property in Hoard.GetLocalizableProperties(__instance.EntityType))
                __result.Add(property);
        }

        //CompendiumLoader.PopulateCompendium(); inserts InsertCustomTypesForLoading() call before CompendiumLoader.InitialiseForEntityTypes() 
        private static IEnumerable<CodeInstruction> CuckooTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldloca_S, 2), //list of loadable types (local)
                new CodeInstruction(OpCodes.Ldloca_S, 1), //dictionary of entity loaders (local)
                new CodeInstruction(OpCodes.Ldarg_2), //culture id (argument)
                new CodeInstruction(OpCodes.Ldarg_0), //instance itself (is needed to locate its private variable next)
                new CodeInstruction(OpCodes.Ldfld, typeof(CompendiumLoader).GetFieldInvariant("_log")),//locating instance's private variable _log
                //finally, calling InsertCustomTypesForLoading() with all these arguments
                new CodeInstruction(OpCodes.Call, typeof(Cuckoo).GetMethodInvariant(nameof(InsertCustomTypesForLoading))),
            };
            MethodInfo injectingMyCodeRightBeforeThisMethod = typeof(Compendium).GetMethodInvariant(nameof(Compendium.InitialiseForEntityTypes));

            return instructions.InsertBeforeMethodCall(injectingMyCodeRightBeforeThisMethod, myCode);
        }

        //called from CuckooTranspiler() (previous method), schedules custom types loading
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

    //class that stores custom properties info
    internal static class Hoard
    {
        readonly static Dictionary<IEntityWithId, Dictionary<string, object>> loadedData = new Dictionary<IEntityWithId, Dictionary<string, object>>();
        readonly static Dictionary<Type, Dictionary<string, CustomProperty>> claimedProperties = new Dictionary<Type, Dictionary<string, CustomProperty>>();

        //registers a custom/hijacked property for an entity type
        internal static void AddCustomProperty<TEntity, TProperty>(string propertyName, bool localize, TProperty defaultValue)
            where TEntity : AbstractEntity<TEntity>
        {
            Type entityType = typeof(TEntity);
            propertyName = propertyName.ToLower(System.Globalization.CultureInfo.InvariantCulture);

            if (defaultValue != null
                && typeof(TProperty).IsValueType == false && typeof(TProperty) != typeof(string))
            {
                Birdsong.TweetLoud($"Trying to assign default value for a {entityType.Name} property '{propertyName}'; but its type - {nameof(TProperty)} - is a reference type and thus can only be null.");
                defaultValue = default(TProperty);
            }

            if (claimedProperties.ContainsKey(entityType) == false)
                claimedProperties.Add(entityType, new Dictionary<string, CustomProperty>(StringComparer.InvariantCultureIgnoreCase));

            if (claimedProperties[entityType].ContainsKey(propertyName))
                Birdsong.TweetLoud($"Trying to add {entityType.Name} property '{propertyName}', but the property of the same name for the same type is already added.");
            else
                claimedProperties[entityType][propertyName] = new CustomProperty(typeof(TProperty), defaultValue, localize);
        }

        internal static object RetrieveProperty(IEntityWithId entity, string propertyName)
        {
            if (entity == null || propertyName == null)
            {
                Birdsong.TweetLoud($"Trying to retrieve a custom property '{propertyName}' from '{entity}', but one of these is null");
                return null;
            }

            if (HasCustomProperty(entity, propertyName))
                return loadedData[entity][propertyName];
            else
            {
                Type entityType = entity.GetType();
                if (claimedProperties.ContainsKey(entityType) && claimedProperties[entityType].ContainsKey(propertyName))
                    return claimedProperties[entityType][propertyName].defaultValue;
            }

            return null;
        }

        internal static Dictionary<string, object> GetCustomProperties(IEntityWithId entity)
        {
            if (loadedData.ContainsKey(entity))
                return loadedData[entity];

            return null;
        }

        internal static void RemoveProperty(IEntityWithId entity, string propertyName)
        {
            if (entity.HasCustomProperty(propertyName))
            {
                loadedData[entity].Remove(propertyName);
                if (loadedData[entity].Count == 0)
                    loadedData.Remove(entity);
            }
            else
                Birdsong.TweetLoud($"Trying to remove property {propertyName} from {entity.GetType().Name} {entity.Id}, but that property isn't set.");
        }

        internal static void SetCustomProperty(IEntityWithId owner, string propertyName, object value)
        {
            Type ownerType = owner.GetType();

            if (claimedProperties.ContainsKey(ownerType) == false || claimedProperties[ownerType].ContainsKey(propertyName) == false)
                Birdsong.TweetLoud($"Setting an unclaimed property '{propertyName}' for {ownerType.Name} '{owner.Id}'; possible typo.");

            if (loadedData.ContainsKey(owner) == false)
                loadedData[owner] = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

            loadedData[owner][propertyName] = value;
        }

        internal static bool HasCustomProperty(IEntityWithId entity, string propertyName)
        {
            return loadedData.ContainsKey(entity) && loadedData[entity].ContainsKey(propertyName);
        }

        internal static void InterceptClaimedProperties(IEntityWithId entity, EntityData entityData, Type entityType, ContentImportLog log)
        {
            if (claimedProperties.ContainsKey(entityType))
                foreach (string claimedProperty in claimedProperties[entityType].Keys)
                {
                    //need to keep ToLower() here as ValuesTable is case sensitive (and always lowercase)
                    string propertyLowercaseName = claimedProperty.ToLower(System.Globalization.CultureInfo.InvariantCulture);

                    if (entityData.ValuesTable.ContainsKey(propertyLowercaseName))
                    {
                        LoadCustomProperty(entity, propertyLowercaseName, entityData.ValuesTable[propertyLowercaseName], log);
                        entityData.ValuesTable.Remove(propertyLowercaseName);
                    }
                }
        }

        internal static void LoadCustomProperty(IEntityWithId entity, string propertyName, object data, ContentImportLog log)
        {
            Type entityType = entity.GetType();

            if (claimedProperties.ContainsKey(entityType) && claimedProperties[entityType].ContainsKey(propertyName))
            {
                if (Ostrich.Ignores(entityType, propertyName))
                {
                    Birdsong.TweetQuiet($"Ignoring custom property '{propertyName}' for '{entity.Id}' {entityType.Name}");
                    return;
                }

                try
                {
                    object importedValue = ImportProperty(data, claimedProperties[entityType][propertyName].type);
                    SetCustomProperty(entity, propertyName, importedValue);
                }
                catch (Exception ex)
                {
                    log.LogProblem($"FAILED TO IMPORT CUSTOM PROPERTY '{propertyName}' FOR {entityType.Name.ToUpper()} '{entity.Id}', error:\n{ex.FormatException()}");
                }
            }
        }

        public static object ImportProperty(object valueData, Type propertyType)
        {
            try
            {
                var Import = ImportMethods.GetDefaultImportFuncForType(propertyType);
                return Import(valueData, propertyType);
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"UNABLE TO IMPORT PROPERTY - {ex.FormatException()}");
            }
        }

        internal static IEnumerable<string> GetLocalizableProperties(Type type)
        {
            if (claimedProperties.ContainsKey(type))
                return claimedProperties[type].Where(property => property.Value.localize == true).Select(property => property.Key);
            else
                return new List<string>();
        }

        struct CustomProperty
        {
            public Type type;
            public readonly bool localize;
            public readonly object defaultValue;

            public CustomProperty(Type type, object value, bool localize)
            {
                this.type = type;
                this.defaultValue = value;
                this.localize = localize;
            }
        }
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static void ClaimProperty<TEntity, TProperty>(string propertyName, bool localize = false, TProperty defaultValue = default(TProperty))
            where TEntity : AbstractEntity<TEntity>
        {
            Beachcomber.Hoard.AddCustomProperty<TEntity, TProperty>(propertyName, localize, defaultValue);
        }

        public static void ClaimProperties<TEntity>(Dictionary<string, Type> propertiesNamesAndTypes, bool localize = false)
            where TEntity : AbstractEntity<TEntity>
        {
            MethodInfo claim = typeof(Machine).GetMethodInvariant("ClaimProperty");
            Type[] invokeGenericTypes = new Type[] { typeof(TEntity), null };
            object[] invokeArguments = new object[] { null, localize, null };

            foreach (string propertyName in propertiesNamesAndTypes.Keys)
            {
                Type propertyType = propertiesNamesAndTypes[propertyName];
                invokeGenericTypes[1] = propertyType;

                object defaultValue;
                if (propertyType == typeof(string))
                    defaultValue = string.Empty;
                if (propertyType.IsValueType)
                    defaultValue = Activator.CreateInstance(propertyType);
                else
                    defaultValue = null;

                invokeArguments[0] = propertyName;
                invokeArguments[2] = defaultValue;
                claim.MakeGenericMethod(invokeGenericTypes).Invoke(null, invokeArguments);
            }
        }

        public static T RetrieveProperty<T>(this IEntityWithId owner, string propertyName)
        {
            var value = Beachcomber.Hoard.RetrieveProperty(owner, propertyName);

            return value == null ? default(T) : (T)value;
        }

        public static object RetrieveProperty(this IEntityWithId owner, string propertyName)
        {
            return Beachcomber.Hoard.RetrieveProperty(owner, propertyName);
        }

        public static bool TryRetrieveProperty<T>(this IEntityWithId owner, string propertyName, out T result)
        {
            object property = Beachcomber.Hoard.RetrieveProperty(owner, propertyName);

            if (property == null)
            {
                result = default(T);
                return false;
            }

            result = (T)property;
            return true;
        }

        public static void SetCustomProperty(this IEntityWithId owner, string propertyName, object value)
        {
            Beachcomber.Hoard.SetCustomProperty(owner, propertyName, value);
        }

        public static void RemoveProperty(this IEntityWithId owner, string propertyName)
        {
            Beachcomber.Hoard.RemoveProperty(owner, propertyName);
        }

        public static bool HasCustomProperty(this IEntityWithId owner, string propertyName)
        {
            return Beachcomber.Hoard.HasCustomProperty(owner, propertyName);
        }

        public static Dictionary<string, object> GetCustomProperties(this IEntityWithId owner)
        {
            return Beachcomber.Hoard.GetCustomProperties(owner);
        }

        public static T GetEntity<T>(string id) where T : AbstractEntity<T>
        {
            return SecretHistories.UI.Watchman.Get<Compendium>().GetEntityById<T>(id);
        }

        public static bool IsNullEntity(this SecretHistories.Entities.Element element)
        {
            return element.Id == NullElement.NULL_ELEMENT_ID;
        }

        public static bool IsNullEntity(this SecretHistories.Entities.Recipe recipe)
        {
            return recipe == SecretHistories.Entities.NullRecipe.Create();
        }

        public static void AddImportMolding<T>(Action<EntityData> moldingForType)
        {
            Beachcomber.Usurper.AddMolding<T>(moldingForType);
        }
    }
}