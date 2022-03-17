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
            return instructions.ReplaceAllAfterMask(mask, myCode, true);
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
                Hoard.InterceptClaimedProperties(entity, entityData, typeof(T));

                foreach (CachedFucineProperty<T> cachedProperty in TypeInfoCache<T>.GetCachedFucinePropertiesForType())
                {
                    if (cachedProperty.LowerCaseName == "id" || Ostrich.Ignores(typeof(T), cachedProperty.LowerCaseName))
                        continue;

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
                        if (propertyType.isSomething())
                            propertyValue = Panimporter.ConstuctFromParameters(cachedProperty.FucineAttribute.DefaultValue, propertyType);
                        else if (propertyType.IsValueType || propertyType == typeof(string))
                            propertyValue = cachedProperty.FucineAttribute.DefaultValue;
                        else
                            propertyValue = FactoryInstantiator.CreateObjectWithDefaultConstructor(propertyType);
                    }

                    cachedProperty.SetViaFastInvoke(entity as T, propertyValue);
                }

                AbstractEntity<T> abstractEntity = entity as AbstractEntity<T>;
                foreach (object key in entityData.ValuesTable.Keys)
                    abstractEntity.PushUnknownProperty(key, entityData.ValuesTable[key]);
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack("Failed to import entity {0} '{1}', reason:\n{2}", typeof(T).Name, entity.Id, ex);
            }
        }
    }
}
