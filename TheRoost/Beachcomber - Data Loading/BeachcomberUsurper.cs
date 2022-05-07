using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;
using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Fucine.DataImport;

namespace Roost.Beachcomber
{
    internal static class Usurper
    {
        internal static void OverthrowNativeImportingButNotCompletely()
        {
            //here we completely replace how the game handles importing
            //(well, json loading and thus localizing/merging/mod $ stays intact, actually, 
            //it's just the process of porting jsons into actual game entities that gets changed)

            //this little thing below actually hijacks the entirety of the CS loading proccess and replaces it all with Beachcomber's pipeline;
            //the original thing has a... history, which makes it powerful in some regards but decrepit in others
            //it can load QuickSpec entities only if they are contained in the ***Dictionary<string,List<IQuickSpecEntity>>*** (hilarious)
            //can't load structs (not that anyone needs that)
            //its values loading sometimes hardcoded etc etc
            //still, there's no much reason to overthrow it entirely (save from "for sport") - most of the edge cases will never be required anyway
            //nobody probably will ever write a root fucine class with expressions
            //nobody will ever need a <float, bool> dictionary, or, god forbid, <List, List> 
            //(well, that last one is actually impossible anyway since it won't be parsed as a correct json, but theoretical possibility exists)
            //struct loading is a mad enterprise in particular
            //the sport was good thought
            Machine.Patch(
                original: typeof(AbstractEntity<Element>).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0],
                transpiler: typeof(Usurper).GetMethodInvariant(nameof(AbstractEntityConstructorTranspiler)));

            //and another little essay, this time more on theme:
            //patching generics is tricky - the patch is applied to the whole generic class/method
            //it's somewhat convenient for me since I can patch only a single AbstractEntity<> for the patch to apply to all of them
            //it's also somewhat inconvenient since I can't patch the .ctor directly with my own generic method
            //(the last patch will be executed for all of the types)
            //thus, I have to create an intermediary - InvokeGenericImporterForAbstractRootEntity() - which calls the actual type-specific method
            //(generics are needed to mimic CS's own structure and since it makes accessing properties much more easierester)

            MethodInfo overrideImporter;
            string nativeMethodName = nameof(Fucine.CreateImporterInstance);

            overrideImporter = typeof(Usurper.DictImporer).GetMethodInvariant(nameof(Usurper.DictImporer.CreateImporterInstance));
            Machine.Patch(typeof(FucineDict).GetMethodInvariant(nativeMethodName), prefix: overrideImporter);
            Machine.Patch(typeof(FucineAspects).GetMethodInvariant(nativeMethodName), prefix: overrideImporter);

            overrideImporter = typeof(Usurper.ListImporter).GetMethodInvariant(nameof(Usurper.ListImporter.CreateImporterInstance));
            Machine.Patch(typeof(FucineList).GetMethodInvariant(nativeMethodName), prefix: overrideImporter);

            overrideImporter = typeof(Usurper.ValueImporter).GetMethodInvariant(nameof(Usurper.ValueImporter.CreateImporterInstance));
            Machine.Patch(typeof(FucineValue).GetMethodInvariant(nativeMethodName), prefix: overrideImporter);

            overrideImporter = typeof(Usurper.SubEntityImporter).GetMethodInvariant(nameof(Usurper.SubEntityImporter.CreateImporterInstance));
            Machine.Patch(typeof(FucineSubEntity).GetMethodInvariant(nativeMethodName), prefix: overrideImporter);

            overrideImporter = typeof(Usurper.ImporterConstructor).GetMethodInvariant(nameof(Usurper.ImporterConstructor.CreateImporterInstance));
            Machine.Patch(typeof(FucinePathValue).GetMethodInvariant(nativeMethodName), prefix: overrideImporter);
        }

        private static IEnumerable<CodeInstruction> AbstractEntityConstructorTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            //transpiler is very simple this time - we just wait until the native code does the actual object creation
            //after it's done, we call InvokeGenericImporterForAbstractRootEntity() to modify the object as we please
            //all other native transmutations are skipped
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, typeof(Usurper).GetMethodInvariant(nameof(InvokeGenericImporterForAbstractRootEntity))),
            };

            Vagabond.CodeInstructionMask mask = instruction => instruction.opcode == OpCodes.Call;
            return instructions.ReplaceAllAfterMask(mask, myCode, true);
        }


        private static readonly Dictionary<Type, MethodInfo> genericImportMethods = new Dictionary<Type, MethodInfo>();
        private static void InvokeGenericImporterForAbstractRootEntity(IEntityWithId entity, EntityData entityData, ContentImportLog log)
        {
            Type type = entity.GetType();

            if (genericImportMethods.ContainsKey(type) == false)
                genericImportMethods.Add(type, typeof(Usurper).GetMethodInvariant(nameof(ImportRootEntity))
                                                                                                            .MakeGenericMethod(new Type[] { type })
                                        );

            genericImportMethods[type].Invoke(entity, new object[] { entity, entityData, log });
        }

        private static void ImportRootEntity<T>(IEntityWithId entity, EntityData importDataForEntity, ContentImportLog log) where T : AbstractEntity<T>
        {
            //it makes everything a bit more hacky but I want id to be set first for the possible logs
            if (importDataForEntity.ValuesTable.ContainsKey("id"))
            {
                entity.SetId(importDataForEntity.Id);
                importDataForEntity.ValuesTable.Remove("id");
            }

            try
            {
                Hoard.InterceptClaimedProperties(entity, importDataForEntity, typeof(T));

                foreach (CachedFucineProperty<T> cachedFucineProperty in TypeInfoCache<T>.GetCachedFucinePropertiesForType())
                {
                    if (cachedFucineProperty.LowerCaseName == "id")
                        continue;

                    if (!Ostrich.Ignores(typeof(T), cachedFucineProperty.LowerCaseName))
                        cachedFucineProperty.GetImporterForProperty().TryImportProperty<T>(entity as T, cachedFucineProperty, importDataForEntity, log);

                    importDataForEntity.ValuesTable.Remove(cachedFucineProperty.LowerCaseName);
                }
                AbstractEntity<T> abstractEntity = entity as AbstractEntity<T>;
                foreach (object key in importDataForEntity.ValuesTable.Keys)
                    abstractEntity.PushUnknownProperty(key, importDataForEntity.ValuesTable[key]);
            }
            catch (Exception ex)
            {
                log.LogProblem($"Failed to import {typeof(T).Name} id '{entity.Id ?? importDataForEntity.Id}', reason:\n{ex}");
            }
        }
        abstract class PanimporterShard : AbstractImporter
        {
            protected abstract object Import(object data, Type type);
            public override bool TryImportProperty<T>(T entity, CachedFucineProperty<T> cachedProperty, EntityData entityData, ContentImportLog log)
            {
                if (entityData.ValuesTable.Contains(cachedProperty.LowerCaseName))
                    try
                    {
                        object result = Import(entityData.ValuesTable[cachedProperty.LowerCaseName], cachedProperty.ThisPropInfo.PropertyType);

                        cachedProperty.SetViaFastInvoke(entity, result);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }

                object defaultValue = cachedProperty.FucineAttribute.DefaultValue ?? FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedProperty.ThisPropInfo.PropertyType);
                cachedProperty.SetViaFastInvoke(entity, defaultValue);
                return false;
            }
        }
        class DictImporer : PanimporterShard
        {
            public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new Usurper.DictImporer(); return false; }
            protected override object Import(object data, Type type) { return Panimporter.ImportDictionary(data, type); }
        }
        class ListImporter : PanimporterShard
        {
            public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new Usurper.ListImporter(); return false; }
            protected override object Import(object data, Type type) { return Panimporter.ImportList(data, type); }
        }
        class ValueImporter : PanimporterShard
        {
            public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new Usurper.ValueImporter(); return false; }
            protected override object Import(object data, Type type) { return Panimporter.ImportSimpleValue(data, type); }
        }
        class SubEntityImporter : PanimporterShard
        {
            public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new Usurper.SubEntityImporter(); return false; }
            protected override object Import(object data, Type type) { return Panimporter.ImportFucineEntity(data, type); }
        }
        class ImporterConstructor : PanimporterShard
        {
            public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new Usurper.ImporterConstructor(); return false; }
            protected override object Import(object data, Type type) { return Panimporter.ConstuctFromParameters(data, type); }
        }
    }
}
