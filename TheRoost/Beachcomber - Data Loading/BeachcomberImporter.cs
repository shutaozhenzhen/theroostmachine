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
                Birdsong.Sing("FAILED TO LOAD PROPERTY '{0}' FOR {1} ID '{2}' - {3}", propertyName, parentEntity.GetType().Name.ToUpper(), parentEntity.Id, ex.Message);
                throw;
            }
        }

        public delegate object ImporterForType(object valueData, Type propertyType);
        public static ImporterForType GetImporterForType(Type type)
        {
            if (type.isList())
                return ImportList;
            else if (type.isDict())
                return ImportDictionary;
            else if (type.isFucineEntity())
                return ImportFucineEntity;
            else if (type.isSomething())
                return ConstuctWithParameters;
            else
                return ImportSimpleValue;
        }

        public static bool isList(this Type type) { return typeof(IList).IsAssignableFrom(type); }
        public static bool isDict(this Type type) { return typeof(IDictionary).IsAssignableFrom(type); }
        public static bool isFucineEntity(this Type type) { return typeof(IEntityWithId).IsAssignableFrom(type); }
        public static bool isSomething(this Type type)
        { //either non-AbstractEntity class or a struct 
            return (type.IsClass && type != typeof(string) & !type.isList() && !type.isDict() && !type.isFucineEntity()) || //type is a stray class
                   (type.IsValueType && !type.IsEnum && type.Namespace != "System"); //type is a struct
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
                throw Birdsong.Cack("LIST[] IS MALFORMED - {0}", ex.Message);
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
                    throw Birdsong.Cack("DICTIONARY IS DEFINED AS {0}", dictionaryData.GetType().Name.ToUpper());

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
                throw Birdsong.Cack("{1} IS MALFORMED - {0}", ex.Message, "DICTIONARY{}");
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
                    if (entityType.IsAbstract || entityType.IsInterface)
                        entityType = SpecifyType(fullSpecEntityData, entityType);

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
                throw Birdsong.Cack("ENTITY DATA IS MALFORMED - {0}", ex.Message);
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, Type> quickTypeId = new System.Collections.Generic.Dictionary<string, Type>();
        private const string TYPE_SPECIFIER = "$type";
        private static Type SpecifyType(EntityData data, Type originalType)
        {
            string typeId = null;
            if (data.ValuesTable.ContainsKey(TYPE_SPECIFIER))
                typeId = data.ValuesTable[TYPE_SPECIFIER].ToString().ToLower();
            else if (data.ValuesTable.Count == 1) foreach (DictionaryEntry entry in data.ValuesTable)
                {
                    typeId = entry.Key.ToString();
                    break;
                }
            else
                throw Birdsong.Cack("Unspecified type for an abstract entity or interface.");

            Type newType;
            if (quickTypeId.ContainsKey(typeId))
                newType = quickTypeId[typeId];
            else
                newType = Type.GetType(typeId, true, false);

            if (originalType.IsAssignableFrom(newType) == false)
                throw Birdsong.Cack("Type '{0}' tries to substitute '{1}', but doesn't inherit from it.", newType, originalType);

            return newType;
        }

        public static object ConstuctWithParameters(object parametersData, Type type)
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

                throw Birdsong.Cack("NO MATCHING CONSTRUCTOR FOUND FOR {0} WITH ARGUMENTS '{1}'", type.Name, parameterTypes.UnpackAsString());
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack("PROPERTY DATA IS MALFORMED - {0}", ex.Message);
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
                    throw Birdsong.Cack("Trying to convert data '{0}' to {1}, but at least one of these two is not a value type", data, destinationType.Name);

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
                throw Birdsong.Cack("UNABLE TO PARSE A VALUE DATA: '{0}' AS {1}: {2}", data, destinationType.Name.ToUpper(), ex.Message);
            }
        }
    }
}

namespace SecretHistories.Fucine
{
    public interface ICustomSpecEntity
    {
        void CustomSpec(Hashtable data);
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FucineSpecial : Fucine
    {
        public FucineSpecial() { }

        public FucineSpecial(params object[] defaultValue)
        {
            DefaultValue = new ArrayList(defaultValue);
        }

        //won't work with the normal game importer, and Panimporter doesn't use this at all, but compiler wants this to exist
        public override AbstractImporter CreateImporterInstance()
        {
            throw new NotImplementedException();
        }
    }
}