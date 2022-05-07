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
        public static object ImportProperty(IEntityWithId parentEntity, object valueData, Type propertyType, string propertyName)
        {
            try
            {
                ImporterForType Import = GetImporterForType(propertyType);
                return Import(valueData, propertyType);
            }
            catch (Exception ex)
            {
                Birdsong.Sing($"FAILED TO LOAD PROPERTY '{propertyName}' FOR {parentEntity.GetType().Name.ToUpper()} ID '{parentEntity.Id}' - {ex.Message}");
                throw;
            }
        }

        public delegate object ImporterForType(object valueData, Type propertyType);
        public static ImporterForType GetImporterForType(Type type)
        {
            if (typeof(IList).IsAssignableFrom(type)) //is a list
                return ImportList;

            else if (typeof(IDictionary).IsAssignableFrom(type)) //is a dictionary
                return ImportDictionary;

            else if (typeof(AbstractEntity<>).IsAssignableFrom(type)) //is a Fucine entity
                return ImportFucineEntity;

            else if (type != typeof(string) && (type.IsClass || (type.IsValueType && !type.IsEnum && type.Namespace != "System"))) //either non-AbstractEntity class or a struct
                return ConstuctFromParameters;

            else
                return ImportSimpleValue;
        }

        public static IList ImportList(object listData, Type listType)
        {
            IList list = FactoryInstantiator.CreateObjectWithDefaultConstructor(listType) as IList;

            //in case we are loading a custom type that derives from List (a la AspectsDictionary : Dictionary<string,int>)
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
                    //to reduce boilerplate in json, I allow loading of a single-entry lists from plain strings
                    //i.e. { "someList": [ "entry" ] } and { "someList": "entry" } will yield the same result
                    object importedSingleEntry = ImportEntity(listData, expectedEntryType);
                    list.Add(importedSingleEntry);
                }
                else foreach (object entry in dataAsArrayList)
                    {
                        object importedEntry = ImportEntity(entry, expectedEntryType);
                        list.Add(importedEntry);
                    }

                return list;
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"LIST[] IS MALFORMED - {ex.Message}");
            }
        }

        public static IDictionary ImportDictionary(object dictionaryData, Type dictionaryType)
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
                    object key = ImportKey(dictionaryEntry.Key, dictionaryKeyType);
                    object value = ImportValue(dictionaryEntry.Value, dictionaryValueType);
                    dictionary.Add(key, value);
                }

                return dictionary;
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"DICTIONARY{{}} IS MALFORMED - {ex.Message}");
            }
        }

        public static object ImportSimpleValue(object valueData, Type destinationType)
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

        public static IEntityWithId ImportFucineEntity(object entityData, Type entityType)
        {
            try
            {
                EntityData fullSpecEntityData = entityData as EntityData;

                if (fullSpecEntityData != null)
                {
                    IEntityWithId entity = FactoryInstantiator.CreateEntity(entityType, entityData as EntityData, null);
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
                throw Birdsong.Cack($"ENTITY DATA{{}} IS MALFORMED - {ex.Message}");
            }
        }

        public static object ConstuctFromParameters(object parametersData, Type type)
        {
            try
            {
                if (parametersData == null)
                    return FactoryInstantiator.CreateObjectWithDefaultConstructor(type);

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

                throw Birdsong.Cack($"NO MATCHING CONSTRUCTOR FOUND FOR {type.Name} WITH ARGUMENTS '{parameterTypes.UnpackAsString()}'");
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"PROPERTY DATA IS MALFORMED - {ex.Message}");
            }
        }

        public static object ConvertValue(object data, Type destinationType)
        {
            try
            {
                Type sourceType = data.GetType();
                if (sourceType == destinationType)
                    return data;

                if ((sourceType.IsValueType == false && sourceType != typeof(string))
                    || (destinationType.IsValueType == false && destinationType != typeof(string)))
                    throw Birdsong.Cack($"Trying to convert data '{data}' to {destinationType.Name}, but at least one of these two is not a value type");

                if (data is string)
                    return TypeDescriptor.GetConverter(destinationType).ConvertFromInvariantString(data.ToString());
                else if (destinationType == typeof(string))
                    return TypeDescriptor.GetConverter(sourceType).ConvertToInvariantString(data);

                if (data is bool && destinationType != typeof(string))
                    data = ((bool)data == true) ? 1 : -1;

                return System.Convert.ChangeType(data, destinationType);
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"UNABLE TO PARSE A VALUE: '{data}' AS {destinationType.Name.ToUpper()}: {ex.Message}");
            }
        }
    }

}

namespace SecretHistories.Fucine
{
    //normal FucineValue won't accept array as DefaultValue; but we need that to construct some structs/classes
    [AttributeUsage(AttributeTargets.Property)]
    public class FucineUniValue : Fucine
    {
        public FucineUniValue() { }
        public FucineUniValue(object defaultValue) { DefaultValue = defaultValue; }
        public FucineUniValue(params object[] defaultValue) { DefaultValue = new ArrayList(defaultValue); }

        public override AbstractImporter CreateImporterInstance()
        {
            return new PanimporterInstance();
        }
    }


    public class PanimporterInstance : AbstractImporter
    {
        public override bool TryImportProperty<T>(T entity, CachedFucineProperty<T> cachedProperty, EntityData entityData, ContentImportLog log)
        {
            try
            {
                string propertyName = cachedProperty.LowerCaseName;
                Type propertyType = cachedProperty.ThisPropInfo.PropertyType;

                object propertyValue;
                if (entityData.ValuesTable.Contains(propertyName))
                {
                    propertyValue = Roost.Beachcomber.Panimporter.ImportProperty(entity, entityData.ValuesTable[propertyName], propertyType, propertyName);
                    entityData.ValuesTable.Remove(propertyName);
                }
                else
                {
                    if (cachedProperty.FucineAttribute.DefaultValue is ArrayList)
                        propertyValue = Roost.Beachcomber.Panimporter.ConstuctFromParameters(cachedProperty.FucineAttribute.DefaultValue, propertyType);
                    else if (propertyType.IsValueType || propertyType == typeof(string))
                        propertyValue = cachedProperty.FucineAttribute.DefaultValue;
                    else
                        propertyValue = FactoryInstantiator.CreateObjectWithDefaultConstructor(propertyType);
                }

                cachedProperty.SetViaFastInvoke(entity as T, propertyValue);

                return true;
            }
            catch (Exception ex)
            {
                log.LogProblem(ex.ToString());
                return false;
            }
        }
    }

    public interface ICustomSpecEntity
    {
        void CustomSpec(Hashtable data);
    }
}