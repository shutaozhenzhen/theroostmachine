using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using HarmonyLib;

using Newtonsoft.Json.Linq;
using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Fucine.DataImport;

namespace Roost.Beachcomber
{
    //don't know an appropriate bird name for this one yet
    public static class ModOpManager
    {
        private const string INHERIT_ADDITIVE = "$derives";
        private const string INHERIT_OVERRIDE = "$extends";
        private const string INHERIT_OVERRIDE_LEGACY = "extends";
        private const string CONTENT_GROUPS = "$contentgroups";
        private const string PRIORITY = "$priority";

        internal static void Enact()
        {
            //another thing that gets usurped (and rightly so, in my humble opinion), is a proccess of applying mod data
            //significant changes:
            //1. now $ ops are applied entity by entity - thus, no flattening whatsoever
            //1.1 solves $ ops incompatibility between mods;
            //1.2 solves inability to modify modded content with $ ops
            //2. extended properties are passed to the inheriting entity as copies; solves the problem of not being able to inherit subentities
            //3. added '$derives' property that does the same thing as 'extends', only additively (merges properties of child and parent)
            //4. added $priority property that controls what definitions are applied first
            //5 maybe something else? i forgot
            Machine.Patch(
                typeof(EntityTypeDataLoader).GetMethodInvariant("LoadEntityDataFromSuppliedFiles"),
                transpiler: typeof(ModOpManager).GetMethodInvariant("ModContentOpsFix"));
        }

        private static IEnumerable<CodeInstruction> ModContentOpsFix(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0), //instance itself as argument
                new CodeInstruction(OpCodes.Ldloca_S, 0), //alreadyLoadedEntities (local)            
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(EntityTypeDataLoader).GetFieldInvariant("_modContentFiles")), 
                new CodeInstruction(OpCodes.Ldarg_0), 
                new CodeInstruction(OpCodes.Ldfld, typeof(EntityTypeDataLoader).GetFieldInvariant("_log")),
                new CodeInstruction(OpCodes.Call, typeof(ModOpManager).GetMethodInvariant("ApplyModsToData")),
            };

            Vagabond.CodeInstructionMask mask = instruction => instruction.opcode == OpCodes.Stloc_0;
            return instructions.ReplaceAllAfterMask(mask, myCode, true); ;
        }

        private static readonly MethodInfo processPropertyOperationsFromEntityMod = typeof(EntityMod).GetMethodInvariant("ProcessPropertyOperationsFromEntityMod");

        private static void ApplyModsToData(EntityTypeDataLoader loader, ref Dictionary<string, EntityData> alreadyLoadedEntities, List<LoadedDataFile> modContentFiles, ContentImportLog log)
        {
            var unpackObjectDataIntoCollection = typeof(EntityTypeDataLoader).GetMethodInvariant("UnpackObjectDataIntoCollection").CreateDelegate(typeof(Action<JToken, FucineUniqueIdBuilder, Dictionary<string, EntityData>, LoadedDataFile>), loader) as Action<JToken, FucineUniqueIdBuilder, Dictionary<string, EntityData>, LoadedDataFile>;

            //:^)
            if (Ostrich.ignoreVanillaContent
                && loader.EntityType != typeof(Culture) && loader.EntityType != typeof(Dictum) && loader.EntityType != typeof(Setting))
                alreadyLoadedEntities.Clear();

            List<EntityData> allModdedEntities = new List<EntityData>();
            Dictionary<string, EntityData> moddedEntityData = new Dictionary<string, EntityData>();
            foreach (LoadedDataFile contentFile in modContentFiles)
                foreach (JToken eachObject in ((JArray)contentFile.EntityContainer.Value))
                {
                    moddedEntityData.Clear();
                    FucineUniqueIdBuilder containerBuilder = new FucineUniqueIdBuilder(contentFile.EntityContainer);
                    unpackObjectDataIntoCollection(eachObject, containerBuilder, moddedEntityData, contentFile);

                    foreach (EntityData modEntity in moddedEntityData.Values)
                    {
                        ArrayList contentgroups = modEntity.GetArrayList(CONTENT_GROUPS);
                        bool skipImport = false;
                        foreach (string groupId in contentgroups)
                            if (Ostrich.Ignores(groupId))
                            {
                                skipImport = true;
                                break;
                            }
                        if (skipImport)
                            continue;

                        allModdedEntities.Add(modEntity);
                        if (!alreadyLoadedEntities.ContainsKey(modEntity.Id))
                            alreadyLoadedEntities.Add(modEntity.Id, new EntityData(modEntity.ValuesTable));
                    }
                }

            foreach (EntityData modEntity in allModdedEntities.OrderBy(data =>
                data.ValuesTable.ContainsKey(PRIORITY) ? (int)data.ValuesTable[PRIORITY] : 0))
                ApplyModTo(new EntityMod(modEntity), modEntity, alreadyLoadedEntities, log);

            loader.GetType().GetPropertyInvariant("_allLoadedEntities").SetValue(loader, alreadyLoadedEntities);
        }

        private static void ApplyModTo(EntityMod entityMod, EntityData modData, Dictionary<string, EntityData> allEntitiesOfType, ContentImportLog log)
        {
            modData.ApplyAdditiveInheritance(allEntitiesOfType);
            modData.ApplyOverrideParentInheritance(allEntitiesOfType);

            EntityData coreDefinition = allEntitiesOfType[modData.Id];
            processPropertyOperationsFromEntityMod.Invoke(entityMod, new object[] { log, coreDefinition });

            if (coreDefinition != modData)
            {
                List<object> allKeys = modData.ValuesTable.Keys.OfType<object>().ToList();
                foreach (object key in allKeys)
                    coreDefinition.OverwriteOrAdd(key, modData.ValuesTable[key]);
            }
        }

        private static ArrayList GetArrayList(this EntityData data, string propertyName)
        {
            if (!data.ValuesTable.ContainsKey(propertyName))
                return new ArrayList();

            ArrayList arrayList = data.ValuesTable[propertyName] as ArrayList;
            if (arrayList == null)
                arrayList = new ArrayList { data.ValuesTable[propertyName] };

            data.ValuesTable.Remove(propertyName);

            return arrayList;
        }

        private static void ApplyOverrideParentInheritance(this EntityData child, Dictionary<string, EntityData> allCoreEntitiesOfType)
        {
            ArrayList extendsList = child.GetArrayList(INHERIT_OVERRIDE);
            extendsList.AddRange(child.GetArrayList(INHERIT_OVERRIDE_LEGACY));

            foreach (string extendId in extendsList)
            {
                EntityData parentEntity;
                if (allCoreEntitiesOfType.TryGetValue(extendId, out parentEntity))
                    foreach (object key in parentEntity.ValuesTable.Keys)
                        if (key.ToString().Contains("$") == false)
                            try
                            {
                                if (child.ValuesTable.ContainsKey(key) == false)
                                    child.ValuesTable.Add(key, CopyDeep(parentEntity.ValuesTable[key]));
                            }
                            catch (Exception ex)
                            {
                                throw Birdsong.Cack("Unable to extend property '{0}' of entity '{1}' from entity '{2}', reason:\n{3}", key, child.Id, extendId, ex);
                            }
                        else
                            Birdsong.Sing("'{0}' tried to extend from an entity that doesn't exist: {1}", child.Id, extendId);
            }
        }

        private static void ApplyAdditiveInheritance(this EntityData derivative, Dictionary<string, EntityData> allCoreEntitiesOfType)
        {
            ArrayList deriveFromEntities = derivative.GetArrayList(INHERIT_ADDITIVE);

            foreach (string rootId in deriveFromEntities)
                if (allCoreEntitiesOfType.ContainsKey(rootId))
                {
                    EntityData parentEntity = allCoreEntitiesOfType[rootId];

                    foreach (object key in parentEntity.ValuesTable.Keys)
                        if (key.ToString().Contains("$") == false)
                            try
                            {
                                if (derivative.ValuesTable.ContainsKey(key))
                                    derivative.ValuesTable[key] = MergeValues(derivative.ValuesTable[key], parentEntity.ValuesTable[key]);
                                else
                                    derivative.ValuesTable.Add(key, CopyDeep(parentEntity.ValuesTable[key]));
                            }
                            catch (Exception ex)
                            {
                                throw Birdsong.Cack("Unable to derive property '{0}' of entity '{1}' from entity '{2}', reason:\n{3}", key, derivative.Id, rootId, ex);
                            }
                }
                else
                    Birdsong.Sing("'{0}' tried to derive from an entity that doesn't exist: {1}", derivative.Id, rootId);
        }

        public static object MergeValues(object derivative, object root)
        {
            try
            {
                if (derivative.GetType() == root.GetType())
                {
                    if (derivative is EntityData)
                        return CombineEntityData(derivative as EntityData, root as EntityData);
                    else if (derivative is ArrayList)
                        return CombineArrayList(derivative as ArrayList, root as ArrayList);
                    else
                        return derivative;
                }
                else if (derivative is ArrayList || root is ArrayList)
                {
                    //if one of the properties is a list, we can still attempt to merge the properties
                    //if another property is a value type, it's interpreted as list's entry
                    //in some cases we can even merge it with entity data

                    if (derivative is EntityData || root is EntityData)
                    {
                        ArrayList list = derivative is ArrayList ? derivative as ArrayList : root as ArrayList;
                        //if lists already contain dicts, everything is fine; the merging happens by normal rules (later below) 
                        if (list.Count == 0 || (list[0] is EntityData) == false)
                            throw new ApplicationException("Can't merge a list with a dictionary.");
                    }

                    if (derivative is ArrayList)
                    {
                        (derivative as ArrayList).Add(root);
                        return derivative;
                    }
                    else
                    {
                        ArrayList result = CopyDeep(root) as ArrayList;
                        result.Insert(0, derivative);
                        return result;
                    }
                }
                else if (derivative is EntityData || root is EntityData)
                    throw Birdsong.Cack("Can't merge a value type with a dictionary");
                else
                    throw Birdsong.Cack("Can't merge two value type properties.");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static EntityData CombineEntityData(EntityData to, EntityData from)
        {
            foreach (string key in from.ValuesTable.Keys)
            {
                if (to.ValuesTable.ContainsKey(key))
                    to.ValuesTable[key] = MergeValues(to.ValuesTable[key], from.ValuesTable[key]);
                else
                    to.ValuesTable.Add(key, CopyDeep(from.ValuesTable[key]));
            }

            return to;
        }

        private static ArrayList CombineArrayList(ArrayList to, ArrayList from)
        {
            foreach (object value in from)
                to.Add(value);

            return to;
        }

        public static object CopyDeep(object source)
        {
            if (source.GetType().IsValueType || source is string)
                return source;
            else if (source is ArrayList)
            {
                ArrayList list = new ArrayList();
                foreach (object entry in source as ArrayList)
                    list.Add(CopyDeep(entry));

                return list;
            }
            else if (source is EntityData)
            {
                EntityData data = new EntityData();
                EntityData sourceData = source as EntityData;
                foreach (object key in sourceData.ValuesTable.Keys)
                    data.ValuesTable[CopyDeep(key)] = CopyDeep(sourceData.ValuesTable[key]);

                return data;
            }

            throw Birdsong.Cack("Can't make a deep copy of type {0} (in fact, how did you even manage to get that inside a json?)", source.GetType());
        }
    }
}
