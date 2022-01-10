using System;
using System.ComponentModel;
using System.Collections;
using System.Reflection;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

namespace TheRoost.Beachcomber
{
    //leaving this one public in case someone/me will need to load something directly
    public static class CustomImporter
    {
        public static object ImportProperty(IEntityWithId parentEntity, object valueData, Type propertyType)
        {
            Importer importer = GetImporterForType(propertyType);
            object propertyValue = importer.Invoke(parentEntity, valueData, propertyType);

            return propertyValue;
        }

        delegate object Importer(IEntityWithId parentEntity, object valueData, Type propertyType);
        static Importer GetImporterForType(Type type)
        {
            if (type.isList())
                return ImportList;
            else if (type.isDict())
                return ImportDictionary;
            else if (type.isFucineEntity())
                return ImportFucineEntity;
            else if (type.isStruct())
                return ImportStruct;

            return ImportSimpleValue;
        }

        public static bool isList(this Type type) { return typeof(IList).IsAssignableFrom(type); }
        public static bool isDict(this Type type) { return typeof(IDictionary).IsAssignableFrom(type); }
        public static bool isFucineEntity(this Type type) { return typeof(IEntityWithId).IsAssignableFrom(type); }
        public static bool isStruct(this Type type) { return type.IsValueType && !type.IsEnum && type.Namespace != "System"; }

        public static IList ImportList(IEntityWithId parentEntity, object listData, Type listType)
        {
            IList list = FactoryInstantiator.CreateObjectWithDefaultConstructor(listType) as IList;

            //in case we are loading a custom type that derives from List (a la AspectsDictionary : Dictionary<string,int>)
            //to know the actual value type we need to find its base List class
            while (listType.IsGenericType == false)
                listType = listType.BaseType;

            Type expectedEntryType = listType.GetGenericArguments()[0];
            Importer entryImporter = GetImporterForType(expectedEntryType);

            try
            {
                ArrayList dataAsArrayList = listData as ArrayList;

                if (dataAsArrayList == null)
                {
                    //to reduce boilerplate in json, I allow loading of a single-entry lists from plain strings
                    //i.e. { "someList": [ "entry" ] } and { "someList": "entry" } will yield the same result
                    object importedSingularEntry = entryImporter.Invoke(parentEntity, listData, expectedEntryType);
                    list.Add(importedSingularEntry);
                }
                else foreach (object entry in dataAsArrayList)
                    {
                        object importedEntry = entryImporter.Invoke(parentEntity, entry, expectedEntryType);
                        list.Add(importedEntry);
                    }

                return list;
            }
            catch (Exception ex)
            {
                Birdsong.Sing("Unable to import a list[] in {0} id '{1}':\n{2}", parentEntity.GetType().Name, parentEntity.Id, ex);
                return null;
            }
        }

        public static IDictionary ImportDictionary(IEntityWithId parentEntity, object dictionaryData, Type dictionaryType)
        {
            IDictionary dictionary = FactoryInstantiator.CreateObjectWithDefaultConstructor(dictionaryType) as IDictionary;

            //in case we are loading a custom type that derives from Dictionary<,> (a la AspectsDictionary : Dictionary<string,int>)
            //to know the actual key/value types we need to find its base Dict class
            while (dictionaryType.IsGenericType == false)
                dictionaryType = dictionaryType.BaseType;

            Type dictionaryKeyType = dictionaryType.GetGenericArguments()[0];
            Importer keyImporter = GetImporterForType(dictionaryKeyType);

            Type dictionaryValueType = dictionaryType.GetGenericArguments()[1];
            Importer valueImporter = GetImporterForType(dictionaryValueType);

            try
            {
                EntityData entityData = dictionaryData as EntityData;

                foreach (DictionaryEntry dictionaryEntry in entityData.ValuesTable)
                {
                    object key = keyImporter.Invoke(parentEntity, dictionaryEntry.Key, dictionaryKeyType);
                    object value = valueImporter.Invoke(parentEntity, dictionaryEntry.Value, dictionaryValueType);
                    dictionary.Add(key, value);
                }

                return dictionary;
            }
            catch (Exception ex)
            {
                Birdsong.Sing("Unable to import a dictionary{} in {0} id '{1}':\n{2}", parentEntity.GetType().Name, parentEntity.Id, ex);
                return null;
            }
        }

        public static object ImportSimpleValue(IEntityWithId parentEntity, object valueData, Type destinationType)
        {
            try
            {
                TypeConverter converter = TypeDescriptor.GetConverter(destinationType);
                Type sourceType = valueData.GetType();

                if (sourceType == destinationType)
                    return valueData;
                else if (sourceType == typeof(string) || destinationType.IsEnum)
                    return converter.ConvertFromInvariantString(valueData.ToString());
                else if (converter.CanConvertFrom(sourceType))
                    return converter.ConvertFrom(valueData);
                else
                    return System.Convert.ChangeType(valueData, destinationType);
            }
            catch (Exception ex)
            {
                Birdsong.Sing("Unable to parse a value {0} in {1} id '{2}':\n{3}", valueData, parentEntity.GetType().Name, parentEntity.Id, ex);
                return null;
            }
        }

        public static IEntityWithId ImportFucineEntity(IEntityWithId parentEntity, object entityData, Type entityType)
        {
            try
            {
                EntityData fullSpecEntityData = entityData as EntityData;

                if (fullSpecEntityData != null)
                {
                    IEntityWithId entity = FactoryInstantiator.CreateEntity(entityType, entityData as EntityData, null);

                    if (typeof(IFancySpecEntity).IsAssignableFrom(entityType))
                        (entity as IFancySpecEntity).FancySpec(fullSpecEntityData.ValuesTable);

                    return entity;
                }
                else if (typeof(IQuickSpecEntity).IsAssignableFrom(entityType))
                {
                    IQuickSpecEntity quickSpecEntity = FactoryInstantiator.CreateObjectWithDefaultConstructor(entityType) as IQuickSpecEntity;
                    quickSpecEntity.QuickSpec(entityData.ToString());
                    return quickSpecEntity as IEntityWithId;
                }

                throw new Exception("Entity Data isn't a dictionary{}, and the entity isn't a Quick Spec entity - so it cannot load from a simple string");
            }
            catch (Exception ex)
            {
                Birdsong.Sing("Unable to import {0}{} in {1} id '{2}':\n{3}", entityType.Name, parentEntity.GetType().Name, parentEntity.Id, ex);
                return null;
            }
        }

        public static object ImportStruct(IEntityWithId parentEntity, object structData, Type structType)
        {
            try
            {
                ArrayList dataAsList = structData as ArrayList;

                if (dataAsList != null) //list-loading of structs is allowed precisely for one (the first one) constructor
                    //it's definitely enough for now for loading of vectors/colours
                {
                    ConstructorInfo constructor = structType.GetConstructors()[0];
                    return constructor.Invoke(dataAsList.ToArray());
                }
                else //otherwise we're trying to create a struct from a single string
                {
                    ConstructorInfo constructor = structType.GetConstructor(new Type[] { typeof(string) });
                    return constructor.Invoke(new object[] { structData.ToString() });
                }
            }
            catch (Exception ex)
            {
                Birdsong.Sing("Unable to parse a struct[] in {0} id '{1}', reason:\n{2}", parentEntity.GetType().Name, parentEntity.Id, ex);
                return null;
            }
        }
    }
}
