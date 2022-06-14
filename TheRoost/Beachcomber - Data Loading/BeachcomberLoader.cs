using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

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

            Machine.Patch(
                original: typeof(AbstractEntity<SecretHistories.Entities.Element>).GetMethodInvariant("PopUnknownKeysToLog"),
                transpiler: typeof(Cuckoo).GetMethodInvariant(nameof(PopUnknownKeysToLog)));
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

        private static IEnumerable<CodeInstruction> PopUnknownKeysToLog(IEnumerable<CodeInstruction> instructions)
        {
            //one last simple transpiler, they said 
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldstr, "Unknown property '{1}' for {2} id '{0}'"),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Callvirt, typeof(AbstractEntity<SecretHistories.Entities.Element>).GetPropertyInvariant("Id").GetGetMethod())
            };

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int n = 0;
            for (; n < codes.Count; n++)
                if (codes[n].opcode == OpCodes.Ldstr)
                {
                    codes.RemoveAt(n);
                    codes.InsertRange(n, myCode);
                    break;
                }

            for (; n < codes.Count; n++)
                if (codes[n].Calls(typeof(string).GetMethodInvariant("Format", typeof(string), typeof(object), typeof(object))))
                {
                    codes[n].operand = typeof(string).GetMethodInvariant("Format", typeof(string), typeof(object), typeof(object), typeof(object));
                    break;
                }

            for (; n < codes.Count; n++)
                if (codes[n].Calls(typeof(ContentImportLog).GetMethodInvariant(nameof(ContentImportLog.LogInfo))))
                {
                    codes[n].operand = typeof(ContentImportLog).GetMethodInvariant(nameof(ContentImportLog.LogWarning));
                    break;
                }

            return codes.AsEnumerable();
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

            if (defaultValue != null
                && typeof(TProperty).IsValueType == false && typeof(TProperty) != typeof(string))
            {
                Birdsong.Tweet($"Trying to assign default value for a {entityType.Name} property '{propertyName}'; but its type - {nameof(TProperty)} - is a reference type and thus can only be null.");
                defaultValue = default(TProperty);
            }

            if (claimedProperties.ContainsKey(entityType) == false)
                claimedProperties.Add(entityType, new Dictionary<string, CustomProperty>());

            propertyName = propertyName.ToLower();
            if (claimedProperties[entityType].ContainsKey(propertyName))
                Birdsong.Tweet($"Trying to add {entityType.Name} property '{propertyName}', but the property of the same name for the same type is already added.");
            else
                claimedProperties[entityType][propertyName] = new CustomProperty(typeof(TProperty), defaultValue, localize);
        }

        internal static object RetrieveProperty(IEntityWithId entity, string propertyName)
        {
            propertyName = propertyName.ToLower();

            if (entity.HasCustomProperty(propertyName))
                return loadedData[entity][propertyName];
            else
            {
                Type entityType = entity.GetType();
                if (claimedProperties.ContainsKey(entityType) && claimedProperties[entityType].ContainsKey(propertyName))
                    return claimedProperties[entityType][propertyName].defaultValue;
                else
                    return false;
            }
        }

        internal static void RemoveProperty(IEntityWithId entity, string propertyName)
        {
            propertyName = propertyName.ToLower();

            if (entity.HasCustomProperty(propertyName))
            {
                loadedData[entity].Remove(propertyName);
                if (loadedData[entity].Count == 0)
                    loadedData.Remove(entity);
            }
            else
                Birdsong.Tweet($"Trying to remove property {propertyName} from {entity.GetType().Name} {entity.Id}, but that property isn't set.");
        }

        internal static void SetProperty(IEntityWithId owner, string propertyName, object value)
        {
            propertyName = propertyName.ToLower();
            Type ownerType = owner.GetType();

            if (claimedProperties.ContainsKey(ownerType) == false || claimedProperties[ownerType].ContainsKey(propertyName) == false)
                Birdsong.Tweet($"Setting an unclaimed property '{propertyName}' for {ownerType.Name} '{owner.Id}'; possible typo.");

            if (loadedData.ContainsKey(owner) == false)
                loadedData[owner] = new Dictionary<string, object>();

            loadedData[owner][propertyName] = value;
        }

        internal static bool HasCustomProperty(IEntityWithId entity, string propertyName)
        {
            return loadedData.ContainsKey(entity) && loadedData[entity].ContainsKey(propertyName.ToLower());
        }

        internal static void InterceptClaimedProperties(IEntityWithId entity, EntityData entityData, Type entityType, ContentImportLog log)
        {
            if (claimedProperties.ContainsKey(entityType))
                foreach (string propertyName in claimedProperties[entityType].Keys)
                    if (entityData.ValuesTable.ContainsKey(propertyName))
                    {
                        LoadCustomProperty(entity, propertyName, entityData.ValuesTable[propertyName], log);
                        entityData.ValuesTable.Remove(propertyName);
                    }
        }

        internal static void LoadCustomProperty(IEntityWithId entity, string propertyName, object data, ContentImportLog log)
        {
            Type entityType = entity.GetType();
            propertyName = propertyName.ToLower();

            if (claimedProperties.ContainsKey(entityType) && claimedProperties[entityType].ContainsKey(propertyName))
            {
                if (Ostrich.Ignores(entityType, propertyName))
                {
                    Birdsong.Tweet(VerbosityLevel.SystemChatter, 0, "Ignoring custom property '{0}' for '{1}' {2}", propertyName, entity.Id, entityType.Name);
                    return;
                }

                try
                {
                    object importedValue = Panimporter.ImportProperty(data, claimedProperties[entityType][propertyName].type, log);

                    if (loadedData.ContainsKey(entity) == false)
                        loadedData[entity] = new Dictionary<string, object>();
                    loadedData[entity].Add(propertyName, importedValue);
                }
                catch (Exception ex)
                {
                    log.LogProblem($"FAILED TO IMPORT CUSTOM PROPERTY '{propertyName}' FOR {entityType.Name.ToUpper()} '{entity.Id}', error:\n{ex.FormatException()}");
                }
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
            return (T)Beachcomber.Hoard.RetrieveProperty(owner, propertyName);
        }

        public static object RetrieveProperty(this IEntityWithId owner, string propertyName)
        {
            return Beachcomber.Hoard.RetrieveProperty(owner, propertyName);
        }

        public static void SetProperty(this IEntityWithId owner, string propertyName, object value)
        {
            Beachcomber.Hoard.SetProperty(owner, propertyName, value);
        }

        public static void RemoveProperty(this IEntityWithId owner, string propertyName)
        {
            Beachcomber.Hoard.RemoveProperty(owner, propertyName);
        }

        public static bool HasCustomProperty(this IEntityWithId owner, string propertyName)
        {
            return Beachcomber.Hoard.HasCustomProperty(owner, propertyName);
        }

        public static T GetEntity<T>(string id) where T : AbstractEntity<T>
        {
            return SecretHistories.UI.Watchman.Get<Compendium>().GetEntityById<T>(id);
        }

        public static void AddImportMolding<T>(Action<EntityData, ContentImportLog> moldingForType)
        {
            Beachcomber.Usurper.AddMolding<T>(moldingForType);
        }
    }
}