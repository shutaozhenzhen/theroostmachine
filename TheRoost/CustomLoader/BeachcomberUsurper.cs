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

using Newtonsoft.Json.Linq;

namespace TheRoost.Beachcomber
{
    internal static class Usurper
    {
        internal static void OverthrowNativeImporting()
        {
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
            TheRoostMachine.Patch(
                typeof(AbstractEntity<Element>).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0],
                transpiler: typeof(Usurper).GetMethodInvariant("AbstractEntityConstructorTranspiler"));

            //and another little essay, this time more on theme:
            //patching generics is tricky - the patch is applied to the whole generic class/method
            //it's somewhat convenient for me since I can patch only a single AbstractEntity<T> for the patch to apply to all of them
            //it's also somewhat inconvenient since I can't patch the .ctor directly with my own generic method
            //(the last patch will be executed for all of the types)
            //thus, I have to create an intermediary - InvokeGenericImporterForAbstractRootEntity() - which calls the actual type-specific method
            //(generics are needed to mimic CS's own structure and since it makes accessing properties much more easierester)

            
            //fixes two things - $ ops being incompatible with each other and mod content being inaccessible for them
            TheRoostMachine.Patch(
                typeof(EntityTypeDataLoader).GetMethodInvariant("LoadEntityDataFromSuppliedFiles"),
                transpiler: typeof(Usurper).GetMethodInvariant("ModContentOpsFixer"));
        }

        private static IEnumerable<CodeInstruction> AbstractEntityConstructorTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
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
            finalCodes.Add(new CodeInstruction(OpCodes.Call, typeof(Usurper).GetMethodInvariant("InvokeGenericImporterForAbstractRootEntity")));
            finalCodes.Add(new CodeInstruction(OpCodes.Ret));

            return finalCodes.AsEnumerable();
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

            foreach (CachedFucineProperty<T> cachedProperty in TypeInfoCache<T>.GetCachedFucinePropertiesForType())
                if (cachedProperty.LowerCaseName != "id")
                {
                    string propertyName = cachedProperty.LowerCaseName;
                    Type propertyType = cachedProperty.ThisPropInfo.PropertyType;

                    object propertyValue;
                    if (entityData.ValuesTable.Contains(propertyName))
                    {
                        try
                        {
                            propertyValue = Panimporter.ImportProperty(entity, entityData.ValuesTable[propertyName], propertyName, propertyType);
                        }
                        catch
                        {
                            throw new Exception("FAILED TO IMPORT JSON");
                        }
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


        private static IEnumerable<CodeInstruction> ModContentOpsFixer(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var finalCodes = new List<CodeInstruction>();

            for (int i = 0; i < codes.Count; i++)
            {
                finalCodes.Add(codes[i]);
                if (codes[i].opcode == OpCodes.Stloc_0)
                    break;
            }

            finalCodes.Add(new CodeInstruction(OpCodes.Ldarg_0)); //instance itself
            finalCodes.Add(new CodeInstruction(OpCodes.Ldloca_S, 0)); //alreadyLoadedEntities (local)                   
            finalCodes.Add(new CodeInstruction(OpCodes.Call, typeof(Usurper).GetMethodInvariant("ApplyModsToDataFileByFile")));
            finalCodes.Add(new CodeInstruction(OpCodes.Ret));

            return finalCodes.AsEnumerable();
        }

        private static void ApplyModsToDataFileByFile(EntityTypeDataLoader loader, ref Dictionary<string, EntityData> alreadyLoadedEntities)
        {
            List<LoadedDataFile> modContentFiles = loader.GetType().GetFieldInvariant("_modContentFiles").GetValue(loader) as List<LoadedDataFile>;
            ContentImportLog log = loader.GetType().GetFieldInvariant("_log").GetValue(loader) as ContentImportLog;

            var unpackObjectDataIntoCollection = typeof(EntityTypeDataLoader).GetMethodInvariant("UnpackObjectDataIntoCollection").CreateDelegate(typeof(Action<JToken, FucineUniqueIdBuilder, Dictionary<string, EntityData>, LoadedDataFile>), loader) as Action<JToken, FucineUniqueIdBuilder, Dictionary<string, EntityData>, LoadedDataFile>;

            foreach (LoadedDataFile contentFile in modContentFiles)
            {
                Dictionary<string, EntityData> moddedEntityData = new Dictionary<string, EntityData>();
                FucineUniqueIdBuilder containerBuilder = new FucineUniqueIdBuilder(contentFile.EntityContainer);

                foreach (JToken eachObject in ((JArray)contentFile.EntityContainer.Value))
                {
                    moddedEntityData.Clear();
                    unpackObjectDataIntoCollection.Invoke(eachObject, containerBuilder, moddedEntityData, contentFile);

                    foreach (EntityData modData in moddedEntityData.Values)
                        new EntityMod(modData).ApplyModTo(alreadyLoadedEntities, log);
                }
            }

            loader.GetType().GetPropertyInvariant("_allLoadedEntities").SetValue(loader, alreadyLoadedEntities);
        }
    }
}
