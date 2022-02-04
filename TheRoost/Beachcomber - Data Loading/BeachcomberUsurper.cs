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

namespace TheRoost.Beachcomber
{
    internal static class Usurper
    {
        internal static void OverthrowNativeImporting()
        {
            //here we completely replace how the game handles importing
            //(well, json loading and thus localizing/merging/mod $ stays intact, actually, 
            //it's just the process of porting jsons into actual game entities that gets changed)

            ///this little thing below actually hijacks the entirety of the CS loading proccess and replaces it all with Beachcomber's pipeline;
            ///the original thing has a... history, which makes it powerful in some regards but decrepit in others
            ///it can load QuickSpec entities only if they are contained in the ***Dictionary<string,List<IQuickSpecEntity>>*** (hilarious)
            ///can't load structs (not that anyone needs that)
            ///its values loading sometimes hardcoded etc etc
            ///still, there's no much reason to overthrow it entirely (save from "for sport") - most of the edge cases will never be required anyway
            ///nobody probably will ever write a root fucine class with expressions
            ///nobody will ever need a <float, bool> dictionary, or, god forbid, <List, List> 
            ///(well, that last one is actually impossible anyway since it won't be parsed as a correct json, but theoretical possibility exists)
            ///struct loading is a mad enterprise in particular
            ///the sport was good thought
            Machine.Patch(
                typeof(AbstractEntity<Element>).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0],
                transpiler: typeof(Usurper).GetMethodInvariant("AbstractEntityConstructorTranspiler"));

            //and another little essay, this time more on theme:
            //patching generics is tricky - the patch is applied to the whole generic class/method
            //it's somewhat convenient for me since I can patch only a single AbstractEntity<T> for the patch to apply to all of them
            //it's also somewhat inconvenient since I can't patch the .ctor directly with my own generic method
            //(the last patch will be executed for all of the types)
            //thus, I have to create an intermediary - InvokeGenericImporterForAbstractRootEntity() - which calls the actual type-specific method
            //(generics are needed to mimic CS's own structure and since it makes accessing properties much more easierester)

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
                transpiler: typeof(Usurper).GetMethodInvariant("ModContentOpsFix"));
        }

        private static IEnumerable<CodeInstruction> AbstractEntityConstructorTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            ///transpiler is very simple this time - we just wait until the native code does the actual object creation
            ///after it's done, we call InvokeGenericImporterForAbstractRootEntity() to modify the object as we please
            ///all other native transmutations are skipped
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, typeof(Usurper).GetMethodInvariant("InvokeGenericImporterForAbstractRootEntity")),
            };

            Vagabond.CodeInstructionMask mask = instruction => instruction.opcode == OpCodes.Call;
            return instructions.ReplaceAllAfterMask(mask, myCode, true); ;
        }

        private static readonly Dictionary<Type, MethodInfo> genericImportMethods = new Dictionary<Type, MethodInfo>();
        private static void InvokeGenericImporterForAbstractRootEntity(IEntityWithId entity, EntityData entityData)
        {
            Type type = entity.GetType();

            if (genericImportMethods.ContainsKey(type) == false)
                genericImportMethods.Add(type, typeof(Usurper).GetMethodInvariant("ImportRootEntity").MakeGenericMethod(new Type[] { type }));
            genericImportMethods[type].Invoke(entity, new object[] { entity, entityData });
        }

        private static void ImportRootEntity<T>(IEntityWithId entity, EntityData entityData) where T : AbstractEntity<T>
        {
            //it makes everything a bit more hacky but I want id to be set first for the possible logs
            if (entityData.ValuesTable.ContainsKey("id"))
            {
                entity.SetId(entityData.Id);
                entityData.ValuesTable.Remove("id");
            }

            try
            {
                foreach (CachedFucineProperty<T> cachedProperty in TypeInfoCache<T>.GetCachedFucinePropertiesForType())
                    if (cachedProperty.LowerCaseName != "id")
                    {
                        string propertyName = cachedProperty.LowerCaseName;
                        Type propertyType = cachedProperty.ThisPropInfo.PropertyType;

                        object propertyValue;
                        if (entityData.ValuesTable.Contains(propertyName))
                        {
                            propertyValue = Panimporter.ImportProperty(entity, entityData.ValuesTable[propertyName], propertyType, propertyName);
                            entityData.ValuesTable.Remove(propertyName);
                        }
                        else
                        {
                            if (propertyType.isStruct() && cachedProperty.FucineAttribute.DefaultValue != null)
                                propertyValue = Panimporter.ImportStruct(cachedProperty.FucineAttribute.DefaultValue, propertyType);
                            else if (propertyType.isList() || propertyType.isDict() || propertyType.isFucineEntity() || propertyType.isStruct())
                                propertyValue = FactoryInstantiator.CreateObjectWithDefaultConstructor(propertyType);
                            else
                                propertyValue = cachedProperty.FucineAttribute.DefaultValue;
                        }

                        cachedProperty.SetViaFastInvoke(entity as T, propertyValue);
                    }

                foreach (object key in entityData.ValuesTable.Keys)
                    (entity as AbstractEntity<T>).PushUnknownProperty(key, entityData.ValuesTable[key]);
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack(ex);
            }
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
    }

    public static class ModOpManager
    {
        private const string INHERIT_ADDITIVE = "$derives";
        private const string INHERIT_OVERRIDE = "$overrides";
        private const string IHERIDE_OVERRIDE_LEGACY = "extends";

        private const string PRIORITY = "$priority";

        private static readonly MethodInfo processPropertyOperationsFromEntityMod = typeof(EntityMod).GetMethodInvariant("ProcessPropertyOperationsFromEntityMod");

        private static void ApplyModsToData(EntityTypeDataLoader loader, ref Dictionary<string, EntityData> alreadyLoadedEntities, List<LoadedDataFile> modContentFiles, ContentImportLog log)
        {
            var unpackObjectDataIntoCollection = typeof(EntityTypeDataLoader).GetMethodInvariant("UnpackObjectDataIntoCollection").CreateDelegate(typeof(Action<JToken, FucineUniqueIdBuilder, Dictionary<string, EntityData>, LoadedDataFile>), loader) as Action<JToken, FucineUniqueIdBuilder, Dictionary<string, EntityData>, LoadedDataFile>;

            List<EntityData> allModdedEntities = new List<EntityData>();
            Dictionary<string, EntityData> moddedEntityData = new Dictionary<string, EntityData>();
            foreach (LoadedDataFile contentFile in modContentFiles)
                foreach (JToken eachObject in ((JArray)contentFile.EntityContainer.Value))
                {
                    moddedEntityData.Clear();
                    FucineUniqueIdBuilder containerBuilder = new FucineUniqueIdBuilder(contentFile.EntityContainer);
                    unpackObjectDataIntoCollection(eachObject, containerBuilder, moddedEntityData, contentFile);

                    foreach (EntityData modeEntity in moddedEntityData.Values)
                    {
                        allModdedEntities.Add(modeEntity);
                        if (!alreadyLoadedEntities.ContainsKey(modeEntity.Id))
                            alreadyLoadedEntities.Add(modeEntity.Id, new EntityData(modeEntity.ValuesTable));
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

        private static ArrayList GetParentsOfInheritanceType(this EntityData data, string inheritanceProperty)
        {
            if (!data.ValuesTable.ContainsKey(inheritanceProperty))
                return new ArrayList();

            ArrayList arrayList = data.ValuesTable[inheritanceProperty] as ArrayList;
            if (arrayList == null)
                arrayList = new ArrayList { data.ValuesTable[inheritanceProperty] };

            data.ValuesTable.Remove(inheritanceProperty);

            return arrayList;
        }

        private static void ApplyOverrideParentInheritance(this EntityData child, Dictionary<string, EntityData> allCoreEntitiesOfType)
        {
            ArrayList extendsList = child.GetParentsOfInheritanceType(INHERIT_OVERRIDE);
            extendsList.AddRange(child.GetParentsOfInheritanceType(IHERIDE_OVERRIDE_LEGACY));

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
            ArrayList deriveFromEntities = derivative.GetParentsOfInheritanceType(INHERIT_ADDITIVE);

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
