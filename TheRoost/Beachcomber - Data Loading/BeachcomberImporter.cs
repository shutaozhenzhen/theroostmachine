using System;
using System.Collections;
using System.Reflection;
using System.ComponentModel;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

namespace Roost.Beachcomber
{
    //leaving this one public in case someone/me will need to load something directly
    public static class Panimporter
    {
        public delegate object ImporterForType(object valueData, Type propertyType, ContentImportLog log);
        public static ImporterForType GetImporterForType(Type type)
        {
            if (typeof(IList).IsAssignableFrom(type)) //is a list
                return ImportList;

            if (typeof(IDictionary).IsAssignableFrom(type)) //is a dictionary
                return ImportDictionary;

            if (typeof(IEntityWithId).IsAssignableFrom(type)) //is a Fucine entity
                return ImportFucineEntity;

            //need something more flexible - putting every possible importer here is obviously not an option;
            //but writing a new importer for each new case is partly a reason why the original importers turned out so ugly
            //(a lot of individual cases mean they can't be edited/fixed in bulk, and amount of ugliness/errors accumulate over time)
            //a good solution'd be an universal Fucine attribute for collections, that allows to set specific importers for the collection's generic types
            if (typeof(FucinePath).IsAssignableFrom(type)) //is a path
                return ImportFucinePath;

            if (type != typeof(string) && (type.IsClass || (type.IsValueType && !type.IsEnum && type.Namespace != "System"))) //either non-AbstractEntity class or a struct
                return ConstuctFromParameters;

            return ImportSimpleValue;
        }

        public static object ImportProperty(object valueData, Type propertyType, ContentImportLog log)
        {
            try
            {
                ImporterForType Import = GetImporterForType(propertyType);
                return Import(valueData, propertyType, log);
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"UNABLE TO IMPORT PROPERTY - {ex.FormatException()}");
            }
        }

        public static IList ImportList(object listData, Type listType, ContentImportLog log)
        {
            IList list = FactoryInstantiator.CreateObjectWithDefaultConstructor(listType) as IList;

            //in case we are loading a custom type that derives from List (a la AspectsList : List<string>)
            //to know the actual value type we need to find its base List class
            while (listType.IsGenericType == false)
                listType = listType.BaseType;

            Type expectedEntryType = listType.GetGenericArguments()[0];
            ImporterForType ImportEntity = GetImporterForType(expectedEntryType);

            try
            {
                ArrayList dataAsArrayList = listData as ArrayList;

                if (dataAsArrayList == null)
                {
                    //to reduce boilerplate in json, allow loading of a single-entry lists from plain strings
                    //i.e. { "someList": [ "entry" ] } and { "someList": "entry" } will yield the same result
                    object importedSingleEntry = ImportEntity(listData, expectedEntryType, log);
                    list.Add(importedSingleEntry);
                }
                else foreach (object entry in dataAsArrayList)
                    {
                        object importedEntry = ImportEntity(entry, expectedEntryType, log);
                        list.Add(importedEntry);
                    }

                return list;
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"LIST[] IS MALFORMED - {ex.FormatException()}");
            }
        }

        public static IDictionary ImportDictionary(object dictionaryData, Type dictionaryType, ContentImportLog log)
        {
            IDictionary dictionary = FactoryInstantiator.CreateObjectWithDefaultConstructor(dictionaryType) as IDictionary;

            //in case we are loading a custom type that derives from Dictionary<,> (a la AspectsDictionary : Dictionary<string,int>)
            //to know the actual key/value types we need to find its base Dict class
            while (dictionaryType.IsGenericType == false)
                dictionaryType = dictionaryType.BaseType;

            Type dictionaryKeyType = dictionaryType.GetGenericArguments()[0];
            ImporterForType ImportKey = GetImporterForType(dictionaryKeyType);

            Type dictionaryValueType = dictionaryType.GetGenericArguments()[1];
            ImporterForType ImportValue = GetImporterForType(dictionaryValueType);

            try
            {
                EntityData entityData = dictionaryData as EntityData;

                if (entityData == null)
                    throw Birdsong.Cack($"DICTIONARY IS DEFINED AS {dictionaryData.GetType().Name.ToUpper()}");

                foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                {
                    object key = ImportKey(dictionaryEntry.Key, dictionaryKeyType, log);
                    object value = ImportValue(dictionaryEntry.Value, dictionaryValueType, log);
                    dictionary.Add(key, value);
                }

                return dictionary;
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"DICTIONARY{{}} IS MALFORMED - {ex.FormatException()}");
            }
        }

        public static IEntityWithId ImportFucineEntity(object entityData, Type entityType, ContentImportLog log)
        {
            try
            {
                EntityData fullSpecEntityData = entityData as EntityData;

                if (fullSpecEntityData != null)
                {
                    IEntityWithId entity = FactoryInstantiator.CreateEntity(entityType, entityData as EntityData, log);
                    if (typeof(ICustomSpecEntity).IsAssignableFrom(entityType))
                        (entity as ICustomSpecEntity).CustomSpec(fullSpecEntityData.ValuesTable);

                    return entity;
                }
                else if (typeof(IQuickSpecEntity).IsAssignableFrom(entityType))
                {
                    if (entityType.GetConstructor(Type.EmptyTypes) == null)
                        throw Birdsong.Cack("QUICK SPEC ENTITY MUST HAVE AN EMPTY CONSTRUCTOR");

                    IQuickSpecEntity quickSpecEntity = FactoryInstantiator.CreateObjectWithDefaultConstructor(entityType) as IQuickSpecEntity;
                    quickSpecEntity.QuickSpec(entityData.ToString());
                    return quickSpecEntity as IEntityWithId;
                }

                throw Birdsong.Cack("ENTITY DATA IS NOT A DICTIONARY{}, AND THE ENTITY ISN'T A QUICK SPEC ENTITY, SO IT CAN NOT LOAD FROM A SINGLE STRING");
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"ENTITY DATA{{}} IS MALFORMED:\n{ex.FormatException()}");
            }
        }

        public static object ConstuctFromParameters(object parametersData, Type type, ContentImportLog log)
        {
            try
            {
                if (parametersData == null)
                    return FactoryInstantiator.CreateObjectWithDefaultConstructor(type);

                if (parametersData is EntityData)
                {
                    EntityData namedParametersData = parametersData as EntityData;


                    ConstructorInfo[] constructors = type.GetConstructors();
                    foreach (ConstructorInfo constructor in constructors)
                    {
                        ParameterInfo[] constructorParameters = constructor.GetParameters();
                        if (constructorParameters.Length != namedParametersData.ValuesTable.Count)
                            continue;

                        foreach (ParameterInfo parameter in constructorParameters)
                            if (namedParametersData.ContainsKey(parameter.Name.ToLower()) == false)
                                goto NEXT_CONSTRUCTOR;

                        //all parameter names match, set up the array and invoke
                        object[] parametersInOrder = new object[namedParametersData.ValuesTable.Count];
                        foreach (ParameterInfo parameter in constructorParameters)
                            parametersInOrder[parameter.Position] = ConvertValue(namedParametersData[parameter.Name.ToLower()], parameter.ParameterType);

                        return constructor.Invoke(parametersInOrder);

                    NEXT_CONSTRUCTOR:
                        continue;
                    }


                    throw Birdsong.Cack($"NO MATCHING CONSTRUCTOR FOUND FOR {type.Name} FOR PARAMETER NAMES '{namedParametersData.ValuesTable.Keys.UnpackAsString()}'");
                }

                ArrayList parametersList = parametersData as ArrayList;
                if (parametersList == null)
                    parametersList = new ArrayList() { parametersData };

                Type[] parameterTypes = new Type[parametersList.Count];
                for (int n = 0; n < parameterTypes.Length; n++)
                {
                    //temporary (hopefully) solution; 
                    if (parametersList[n].GetType().IsClass)
                        parametersList[n] = parametersList[n].ToString();

                    parameterTypes[n] = parametersList[n].GetType();
                }

                //trying to find a ctor that matches passed parameter types
                ConstructorInfo matchingConstructor = type.GetConstructor(parameterTypes);
                //but since these types are loaded and interpreted by JSON, they can be borked and this won't always work
                if (matchingConstructor != null)
                    return matchingConstructor.Invoke(parametersList.ToArray());

                //in this case we are trying to at least find a constructor with the matching number of arguments (now *this* should work in most cases)
                foreach (ConstructorInfo constructor in type.GetConstructors())
                {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    if (parameters.Length == parametersList.Count)
                    {
                        //need to cast these parameters to be usable
                        for (int n = 0; n < parametersList.Count; n++)
                            parametersList[n] = ConvertValue(parametersList[n], parameters[n].ParameterType);

                        return constructor.Invoke(parametersList.ToArray());
                    }
                }

                throw Birdsong.Cack($"NO MATCHING CONSTRUCTOR FOUND FOR {type.Name} WITH PARAMETER TYPES '{parameterTypes.UnpackAsString()}'");
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"PROPERTY DATA IS MALFORMED - {ex.FormatException()}");
            }
        }

        public static object ImportSimpleValue(object valueData, Type destinationType, ContentImportLog log)
        {
            try
            {
                return ConvertValue(valueData, destinationType);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static object ImportFucinePath(object pathdata, Type destinationType, ContentImportLog log)
        {
            string pathValueAsString = pathdata.ToString();

            try
            {
                FucinePath pathValue = Roost.Twins.FuncineParser.ParseSpherePath(pathValueAsString);

                if (pathValue.IsValid())
                    return pathValue;
                else
                    throw Birdsong.Cack(pathValue.GetDisplayStatus());
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"Problem importing FucinePath {pathValueAsString}: {ex.FormatException()}");
            }
        }

        public static object ConvertValue(object data, Type destinationType)
        {
            Type sourceType = data.GetType();
            if (sourceType == destinationType)
                return data;

            try
            {
                if ((sourceType.IsValueType == false && sourceType != typeof(string))
                    || (destinationType.IsValueType == false && destinationType != typeof(string)))
                    throw Birdsong.Cack($"Trying to convert data '{data}' to {destinationType.Name}, but at least one of these two is not a value type");

                if (data is string)
                    return TypeDescriptor.GetConverter(destinationType).ConvertFromInvariantString(data.ToString());
                else if (destinationType == typeof(string))
                    return TypeDescriptor.GetConverter(sourceType).ConvertToInvariantString(data);

                if (data is bool && destinationType != typeof(string))
                    data = ((bool)data == true) ? 1 : -1;

                return TypeDescriptor.GetConverter(sourceType).ConvertTo(data, destinationType);// System.Convert.ChangeType(data, destinationType);
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"UNABLE TO CONVERT {sourceType.Name} '{data}' TO {destinationType.Name}: {ex.FormatException()}");
            }
        }
    }

    public class PanimporterShard : AbstractImporter
    {
        public override bool TryImportProperty<T>(T entity, CachedFucineProperty<T> cachedFucineProperty, EntityData entityData, ContentImportLog log)
        {
            string propertyName = cachedFucineProperty.LowerCaseName;
            Type propertyType = cachedFucineProperty.ThisPropInfo.PropertyType;

            try
            {
                if (entityData.ValuesTable.Contains(propertyName))
                {
                    object result = Import(entityData.ValuesTable[propertyName], propertyType, log);

                    cachedFucineProperty.SetViaFastInvoke(entity, result);

                    return true;
                }

                cachedFucineProperty.SetViaFastInvoke(entity, GetDefaultValue(cachedFucineProperty, log));

                return false;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        protected virtual object Import(object data, Type type, ContentImportLog log)
        {
            return Panimporter.ImportProperty(data, type, log);
        }

        protected virtual object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log) where T : AbstractEntity<T>
        {
            Type propertyType = cachedFucineProperty.ThisPropInfo.PropertyType;

            if (propertyType.Namespace == "System" || propertyType.IsEnum)
                return cachedFucineProperty.FucineAttribute.DefaultValue;
            else if (cachedFucineProperty.FucineAttribute.DefaultValue != null)
                return Panimporter.ConstuctFromParameters(cachedFucineProperty.FucineAttribute.DefaultValue, propertyType, log);
            else
                return FactoryInstantiator.CreateObjectWithDefaultConstructor(propertyType);
        }
    }

    class ListPanImporter : PanimporterShard
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new ListPanImporter(); return false; }
        protected override object Import(object data, Type type, ContentImportLog log) { return Panimporter.ImportList(data, type, log); }

        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        { return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType); }
    }
    class DictPanImporer : PanimporterShard
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new DictPanImporer(); return false; }
        protected override object Import(object data, Type type, ContentImportLog log) { return Panimporter.ImportDictionary(data, type, log); }

        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        { return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType); }
    }

    class ValuePanImporter : PanimporterShard
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new ValuePanImporter(); return false; }
        protected override object Import(object data, Type type, ContentImportLog log) { return Panimporter.ImportSimpleValue(data, type, log); }

        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        { return cachedFucineProperty.FucineAttribute.DefaultValue; }
    }
    class SubEntityPanImporter : PanimporterShard
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new SubEntityPanImporter(); return false; }
        protected override object Import(object data, Type type, ContentImportLog log) { return Panimporter.ImportFucineEntity(data, type, log); }
        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        {
            if (cachedFucineProperty.FucineAttribute.DefaultValue != null)
                return Panimporter.ConstuctFromParameters(cachedFucineProperty.FucineAttribute.DefaultValue, cachedFucineProperty.ThisPropInfo.PropertyType, log);
            else
                return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType);
        }
    }
    class ConstructorPanImporter : PanimporterShard
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new ConstructorPanImporter(); return false; }
        protected override object Import(object data, Type type, ContentImportLog log) { return Panimporter.ConstuctFromParameters(data, type, log); }
        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        {
            if (cachedFucineProperty.FucineAttribute.DefaultValue != null)
                return Panimporter.ConstuctFromParameters(cachedFucineProperty.FucineAttribute.DefaultValue, cachedFucineProperty.ThisPropInfo.PropertyType, log);
            else
                return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType);
        }
    }
    class FucinePathPanImporter : PanimporterShard
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new FucinePathPanImporter(); return false; }
        protected override object Import(object pathData, Type type, ContentImportLog log) { return Panimporter.ImportFucinePath(pathData, type, log); }
        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        { return cachedFucineProperty.FucineAttribute.DefaultValue ?? FucinePath.Current(); }
    }
}



namespace SecretHistories.Fucine
{
    //normal FucineValue won't accept array as DefaultValue; but we need that to construct some structs/classes
    [AttributeUsage(AttributeTargets.Property)]
    public class FucineEverValue : Fucine
    {
        public FucineEverValue() { DefaultValue = new ArrayList(); }
        public FucineEverValue(object defaultValue) { DefaultValue = defaultValue; }
        public FucineEverValue(params object[] defaultValue) { DefaultValue = new ArrayList(defaultValue); }

        public override AbstractImporter CreateImporterInstance() { return new Roost.Beachcomber.PanimporterShard(); }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FucineConstruct : Fucine
    {
        public FucineConstruct() { DefaultValue = new ArrayList(); }
        public FucineConstruct(object defaultValue) { DefaultValue = defaultValue; }
        public FucineConstruct(params object[] defaultValue) { DefaultValue = new ArrayList(defaultValue); }

        public override AbstractImporter CreateImporterInstance() { return new Roost.Beachcomber.ConstructorPanImporter(); }
    }

    public interface ICustomSpecEntity
    {
        void CustomSpec(Hashtable data);
    }
}