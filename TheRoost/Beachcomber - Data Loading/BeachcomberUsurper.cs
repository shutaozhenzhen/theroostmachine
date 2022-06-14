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
        private static Dictionary<Type, List<Action<EntityData, ContentImportLog>>> _moldings = new Dictionary<Type, List<Action<EntityData, ContentImportLog>>>();

        internal static void OverthrowNativeImportingButNotCompletely()
        {
            //patching generics is tricky - the patch is applied to the whole generic class/method
            //it's somewhat convenient for me since I can patch only a single AbstractEntity<> for the patch to apply to all of them
            //it's also somewhat inconvenient since I can't patch the .ctor directly with my own generic method
            //(the last patch will be executed for all of the types)
            //thus, I have to create an intermediary - InvokeGenericImporterForAbstractRootEntity() - which calls the actual type-specific method
            //(generics are needed to mimic CS's own structure and since it makes accessing properties much more easierester)
            Machine.Patch(
                original: typeof(AbstractEntity<Element>).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0],
                transpiler: typeof(Usurper).GetMethodInvariant(nameof(AbstractEntityConstructorTranspiler)));

            string nativeMethodName = nameof(Fucine.CreateImporterInstance);
            Machine.Patch(typeof(FucineList).GetMethodInvariant(nativeMethodName), prefix: typeof(ListPanImporter).GetMethodInvariant(nativeMethodName));
            Machine.Patch(typeof(FucineDict).GetMethodInvariant(nativeMethodName), prefix: typeof(DictPanImporer).GetMethodInvariant(nativeMethodName));
            Machine.Patch(typeof(FucineValue).GetMethodInvariant(nativeMethodName), prefix: typeof(ValuePanImporter).GetMethodInvariant(nativeMethodName));
            Machine.Patch(typeof(FucineSubEntity).GetMethodInvariant(nativeMethodName), prefix: typeof(SubEntityPanImporter).GetMethodInvariant(nativeMethodName));
            Machine.Patch(typeof(FucinePathValue).GetMethodInvariant(nativeMethodName), prefix: typeof(FucinePathPanImporter).GetMethodInvariant(nativeMethodName));
            //not touching any specific importers: AspectImporter, IdImporter - they are suitable
            //need to replace PathImporter only to allow loading of FucinePathPlus
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
            return instructions.ReplaceAfterMask(mask, myCode, true);
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

            if (_moldings.ContainsKey(typeof(T)))
                foreach (Action<EntityData, ContentImportLog> Mold in _moldings[typeof(T)])
                    try
                    {
                        Mold(importDataForEntity, log);
                    }
                    catch (Exception ex)
                    {
                        log.LogProblem($"Failed to apply molding '{Mold.Method.Name}' to {typeof(T).Name} '{entity.Id}', reason:\n{ex.FormatException()}");
                    }

            if (typeof(IMalleable).IsAssignableFrom(typeof(T)))
                (entity as IMalleable).Mold(importDataForEntity, log);

            Hoard.InterceptClaimedProperties(entity, importDataForEntity, typeof(T), log);

            foreach (CachedFucineProperty<T> cachedFucineProperty in TypeInfoCache<T>.GetCachedFucinePropertiesForType())
            {
                if (cachedFucineProperty.LowerCaseName == "id")
                    continue;

                try
                {
                    if (!Ostrich.Ignores(typeof(T), cachedFucineProperty.LowerCaseName))
                        cachedFucineProperty.GetImporterForProperty().TryImportProperty<T>(entity as T, cachedFucineProperty, importDataForEntity, log);

                    importDataForEntity.ValuesTable.Remove(cachedFucineProperty.LowerCaseName);
                }
                catch (Exception ex)
                {
                    log.LogProblem($"Failed to import property '{cachedFucineProperty.LowerCaseName}' of {typeof(T).Name} '{entity.Id}', reason:\n{ex.FormatException()}");
                }
            }

            if (typeof(ICustomSpecEntity).IsAssignableFrom(typeof(T)))
                (entity as ICustomSpecEntity).CustomSpec(importDataForEntity, log);

            AbstractEntity<T> abstractEntity = entity as AbstractEntity<T>;
            foreach (object key in importDataForEntity.ValuesTable.Keys)
                abstractEntity.PushUnknownProperty(key, importDataForEntity.ValuesTable[key]);
        }

        internal static void AddMolding<T>(Action<EntityData, ContentImportLog> moldingForType)
        {
            if (_moldings.ContainsKey(typeof(T)) == false)
                _moldings[typeof(T)] = new List<Action<EntityData, ContentImportLog>>();
            _moldings[typeof(T)].Add(moldingForType);
        }

    }
}
