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


            Machine.Patch(typeof(EntityMod).GetMethodInvariant("ApplyModTo"),
                prefix: typeof(Usurper).GetMethodInvariant("ApplyDerives"));
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
            //this is a relatively harmless fix for a rather obscure feature
            //which is "using "extends" for inheriting properties that contain sub-entities"
            //extending, when applied to sub-entity properties, makes it so that one instance of EntityData appear in several places
            //but the main game loader (and, previously, Usurper, following its footsteps) deletes properties from EntityData when importing
            //therefore, the first time that spread-across-several-other-entitydatas entitydata is loaded, it becomes blank, with all its properties removed
            //and each consequitive load gives you nothing
            //thus, we're fixing this by not removing properties, and just marking them as "recognized" instead
            //the data is cleared afterwards by GC anyway (I think)
            List<string> recognizedProperties = new List<string>();

            //it makes everything a bit more hacky but I want id to be set first for the possible logs
            if (entityData.ValuesTable.ContainsKey("id"))
            {
                entity.SetId(entityData.Id);
                recognizedProperties.Add("id");
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
                            throw Birdsong.Droppings("FAILED TO IMPORT JSON");
                        }

                        recognizedProperties.Add(propertyName);
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
                if (recognizedProperties.Contains(key.ToString().ToLower()) == false)
                    (entity as AbstractEntity<T>).PushUnknownProperty(key, entityData.ValuesTable[key]);
        }

        const string derivesPropertyName = "$derives";
        private static void ApplyDerives(Dictionary<string, EntityData> allCoreEntitiesOfType, EntityData ____modData)
        {
            EntityData derivative = ____modData;
            List<string> deriveFromEntities = ____modData.GetDerives();
            foreach (string rootId in deriveFromEntities)
                if (allCoreEntitiesOfType.ContainsKey(rootId))
                {
                    EntityData root = allCoreEntitiesOfType[rootId];

                    foreach (object key in root.ValuesTable.Keys)
                        try
                        {
                            if (derivative.ValuesTable.ContainsKey(key))
                                derivative.ValuesTable[key] = DeriveProperty(derivative.ValuesTable[key], root.ValuesTable[key]);
                            else
                                derivative.ValuesTable.Add(key, CopyDeep(root.ValuesTable[key]));
                        }
                        catch (Exception ex)
                        {
                            throw Birdsong.Droppings("Unable to derive property '{0}' of entity '{1}' from entity '{2}', reason:\n{3}", key, ____modData.Id, rootId, ex);
                        }
                }
        }

        private static List<string> GetDerives(this EntityData data)
        {
            if (!data.ValuesTable.ContainsKey(derivesPropertyName))
                return new List<string>();

            ArrayList arrayList = data.ValuesTable[derivesPropertyName] as ArrayList;
            data.ValuesTable.Remove(derivesPropertyName);
            if (arrayList == null)
                return new List<string>() { data.ValuesTable[derivesPropertyName].ToString() };

            return arrayList.Cast<string>().ToList();
        }

        private static object DeriveProperty(object derivative, object root)
        {
            try
            {
                if (derivative.GetType() == root.GetType())
                {
                    if (derivative is EntityData)
                    {
                        EntityData derivativeProperties = (derivative as EntityData);
                        EntityData rootProperties = (root as EntityData);

                        foreach (string key in rootProperties.ValuesTable.Keys)
                        {
                            if (derivativeProperties.ValuesTable.ContainsKey(key))
                                derivativeProperties.ValuesTable[key] = DeriveProperty(derivativeProperties.ValuesTable[key], rootProperties.ValuesTable[key]);
                            else
                                derivativeProperties.ValuesTable.Add(key, CopyDeep(rootProperties.ValuesTable[key]));
                        }
                    }
                    else if (derivative is ArrayList)
                        foreach (object value in (root as ArrayList))
                            (derivative as ArrayList).Add(value);
                    //there's a third case where both properties are simple values - in that case we don't modify it at all

                    return derivative;
                }
                else if (derivative.GetType().IsValueType == false && root.GetType().IsValueType == false)
                    throw new ApplicationException("Can't merge a list with a dictionary");
                else if (derivative is EntityData || root is EntityData)
                    throw Birdsong.Droppings("Can't merge a string with a dictionary");
                else if (derivative is ArrayList || root is ArrayList)
                {
                    ArrayList result;
                    if (root is ArrayList == false)
                    {
                        result = derivative as ArrayList;
                        result.Add(root);
                    }
                    else
                    {
                        result = CopyDeep(root) as ArrayList;
                        result.Insert(0, derivative);
                    }

                    return result;
                }

                throw Birdsong.Droppings("Can't process property types");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static object CopyDeep(object source)
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

            throw Birdsong.Droppings("Can't make a deep copy of type {0} (in fact, how did you even manage to get that inside a json?)", source.GetType());
        }
    }
}
