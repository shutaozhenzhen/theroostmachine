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
                return ImportListWithDefaultSubImporter;

            if (typeof(IDictionary).IsAssignableFrom(type)) //is a dictionary
                return ImportDictionaryWithDefaultSubImporters;

            if (typeof(IEntityWithId).IsAssignableFrom(type)) //is a Fucine entity
                return ImportFucineEntity;

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

        public static IList ImportListWithDefaultSubImporter(object listData, Type listType, ContentImportLog log)
        {
            //in case we are loading a custom type that derives from List (a la AspectsList : List<string>)
            //to know the actual value type we need to find its base List class
            while (listType.IsGenericType == false)
                listType = listType.BaseType;

            Type expectedEntryType = listType.GetGenericArguments()[0];
            ImporterForType EntryImporter = GetImporterForType(expectedEntryType);
            try
            {
                return ImportList(listData, listType, log, EntryImporter);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static IList ImportList(object listData, Type listType, ContentImportLog log, ImporterForType ImportEntry)
        {
            IList list = FactoryInstantiator.CreateObjectWithDefaultConstructor(listType) as IList;

            //in case we are loading a custom type that derives from List (a la AspectsList : List<string>)
            //to know the actual value type we need to find its base List class
            while (listType.IsGenericType == false)
                listType = listType.BaseType;
            Type expectedEntryType = listType.GetGenericArguments()[0];

            try
            {
                ArrayList dataAsArrayList = listData as ArrayList;

                if (dataAsArrayList == null)
                {
                    //to reduce boilerplate in json, allow loading of a single-entry lists from plain strings
                    //i.e. { "someList": [ "entry" ] } and { "someList": "entry" } will yield the same result
                    object importedSingleEntry = ImportEntry(listData, expectedEntryType, log);
                    list.Add(importedSingleEntry);
                }
                else foreach (object entry in dataAsArrayList)
                    {
                        object importedEntry = ImportEntry(entry, expectedEntryType, log);
                        list.Add(importedEntry);
                    }

                return list;
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"LIST[] IS MALFORMED - {ex.FormatException()}");
            }
        }

        public static IDictionary ImportDictionaryWithDefaultSubImporters(object dictionaryData, Type dictionaryType, ContentImportLog log)
        {
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
                return ImportDictionary(dictionaryData, dictionaryType, log, ImportKey, ImportValue);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static IDictionary ImportDictionary(object dictionaryData, Type dictionaryType, ContentImportLog log, ImporterForType ImportKey, ImporterForType ImportValue)
        {
            IDictionary dictionary = FactoryInstantiator.CreateObjectWithDefaultConstructor(dictionaryType) as IDictionary;

            //in case we are loading a custom type that derives from Dictionary<,> (a la AspectsDictionary : Dictionary<string,int>)
            //to know the actual key/value types we need to find its base Dict class
            while (dictionaryType.IsGenericType == false)
                dictionaryType = dictionaryType.BaseType;

            Type dictionaryKeyType = dictionaryType.GetGenericArguments()[0];
            Type dictionaryValueType = dictionaryType.GetGenericArguments()[1];

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
                    return entity;
                }
                else if (typeof(IQuickSpecEntity).IsAssignableFrom(entityType))
                {
                    if (entityType.GetConstructor(Type.EmptyTypes) == null)
                        throw Birdsong.Cack($"QUICK SPEC ENTITY {entityType.Name.ToUpper()} MUST HAVE AN EMPTY CONSTRUCTOR");

                    IQuickSpecEntity quickSpecEntity = FactoryInstantiator.CreateObjectWithDefaultConstructor(entityType) as IQuickSpecEntity;
                    quickSpecEntity.QuickSpec(entityData.ToString());
                    return quickSpecEntity as IEntityWithId;
                }

                throw Birdsong.Cack($"ENTITY DATA IS NOT A DICTIONARY{{}}, AND THE ENTITY TYPE {entityType.Name.ToUpper()} ISN'T QUICKSPEC, SO IT CAN NOT LOAD FROM A SINGLE STRING");
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"ENTITY DATA IS MALFORMED:\n{ex.FormatException()}");
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


                    throw Birdsong.Cack($"NO MATCHING CONSTRUCTOR FOUND FOR {type.Name} FOR PARAMETER NAMES '{namedParametersData.ValuesTable.Keys.LogCollection()}'");
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

                throw Birdsong.Cack($"NO MATCHING CONSTRUCTOR FOUND FOR {type.Name} WITH PARAMETER TYPES '{parameterTypes.LogCollection()}'");
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
                FucinePath pathValue = Roost.Twins.TwinsParser.ParseSpherePath(pathValueAsString);

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

        public static object ConvertValue(object sourceValue, Type destinationType)
        {
            Type sourceType = sourceValue.GetType();
            if (sourceType == destinationType)
                return sourceValue;

            try
            {
                if (sourceType == typeof(string))
                    return TypeDescriptor.GetConverter(destinationType).ConvertFromInvariantString((string)sourceValue);
                else if (destinationType == typeof(string))
                    return TypeDescriptor.GetConverter(sourceType).ConvertToInvariantString(sourceValue);

                if (sourceType == typeof(bool))
                    return System.Convert.ChangeType((bool)sourceValue ? 1 : 0, destinationType);
                if (destinationType == typeof(bool))
                    return (float)sourceValue > 0 ? true : false;

                return TypeDescriptor.GetConverter(sourceType).ConvertTo(sourceValue, destinationType);
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"UNABLE TO CONVERT {sourceType.Name} '{sourceValue}' TO {destinationType.Name}: {ex.FormatException()}");
            }
        }
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static T ConvertTo<T>(this object value) where T : IConvertible
        {
            return (T)Roost.Beachcomber.Panimporter.ConvertValue(value, typeof(T));
        }
    }
}