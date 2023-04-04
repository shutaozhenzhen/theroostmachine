using System;
using System.Collections;
using SecretHistories.Fucine.DataImport;
using Roost.Beachcomber;
using Roost;

namespace SecretHistories.Fucine
{

    public class PropertyPanImporter : AbstractImporter
    {
        public override object Import(object data, Type type)
        {
            return Hoard.ImportProperty(data, type);
        }

        public override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty)
        {
            Type propertyType = cachedFucineProperty.ThisPropInfo.PropertyType;

            if (propertyType.Namespace == "System" || propertyType.IsEnum)
                return cachedFucineProperty.FucineAttribute.DefaultValue;
            else if (cachedFucineProperty.FucineAttribute.DefaultValue != null)
                return ImportMethods.ImportWithConstructor(cachedFucineProperty.FucineAttribute.DefaultValue, propertyType);

            return FactoryInstantiator.CreateObjectWithDefaultConstructor(propertyType);
        }


    }

    class CustomListPanImporter : AbstractImporter
    {
        public CustomListPanImporter(Type entryImporter)
        {
            this._entryImporter = Activator.CreateInstance(entryImporter) as AbstractImporter;
        }

        private AbstractImporter _entryImporter;
        public override object Import(object importData, Type propertyType)
        {
            return ImportMethods.ImportList(importData, propertyType, Pantiment.GetImportFunc(_entryImporter));
        }

        public override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty)
        {
            return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType);
        }
    }

    class CustomDictPanImporter : AbstractImporter
    {
        public CustomDictPanImporter(Type KeyImporter, Type ValueImporter)
        {
            this._keyImporter = Activator.CreateInstance(KeyImporter) as AbstractImporter;
            this._valueImporter = Activator.CreateInstance(ValueImporter) as AbstractImporter;
        }

        private AbstractImporter _keyImporter;
        private AbstractImporter _valueImporter;

        public override object Import(object importData, Type propertyType)
        {
            return ImportMethods.ImportDictionary(importData, propertyType, Pantiment.GetImportFunc(_keyImporter), Pantiment.GetImportFunc(_valueImporter));
        }

        public override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty)
        {
            return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType);
        }
    }

    class ConstructorPanImporter : AbstractImporter
    {
        public override object Import(object importData, Type type)
        {
            object result;
            try
            {
                result = ImportMethods.ImportWithConstructor(importData, type);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return result;
        }
        public override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty)
        {
            if (cachedFucineProperty.FucineAttribute.DefaultValue != null)
                return ImportMethods.ImportWithConstructor(cachedFucineProperty.FucineAttribute.DefaultValue, cachedFucineProperty.ThisPropInfo.PropertyType);

            return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType);
        }
    }

    //normal FucineValue won't accept array as DefaultValue; but we need that to construct some structs/classes
    [AttributeUsage(AttributeTargets.Property)]
    public class FucineEverValue : Fucine
    {
        public FucineEverValue() { DefaultValue = new ArrayList(); }
        public FucineEverValue(object defaultValue) { DefaultValue = defaultValue; }
        public FucineEverValue(params object[] defaultValue) { DefaultValue = new ArrayList(defaultValue); }

        public override AbstractImporter CreateImporterInstance() { return new PropertyPanImporter(); }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FucineConstruct : Fucine
    {
        public FucineConstruct() { DefaultValue = new ArrayList(); }
        public FucineConstruct(object defaultValue) { DefaultValue = defaultValue; }
        public FucineConstruct(params object[] defaultValue) { DefaultValue = new ArrayList(defaultValue); }

        public override AbstractImporter CreateImporterInstance() { return new ConstructorPanImporter(); }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FucineCustomList : Fucine
    {
        public Type EntryImporter { get; private set; }
        public FucineCustomList(Type EntryImporter) { this.EntryImporter = EntryImporter; }
        public override AbstractImporter CreateImporterInstance() { return new CustomListPanImporter(EntryImporter); }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FucineCustomDict : Fucine
    {
        public Type KeyImporter { get; private set; }
        public Type ValueImporter { get; private set; }
        public FucineCustomDict(Type KeyImporter, Type ValueImporter) { this.KeyImporter = KeyImporter; this.ValueImporter = ValueImporter; }
        public override AbstractImporter CreateImporterInstance() { return new CustomDictPanImporter(KeyImporter, ValueImporter); }
    }

    public class ExtendedPathImporter : AbstractImporter
    {
        public override object Import(object importData, Type propertyType)
        {
            try
            {
                FucinePath fucinePath = Roost.Twins.TwinsParser.ParseSpherePath(importData.ToString());
                if (fucinePath.IsValid())
                    return fucinePath;

                return FucinePath.Current();
            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("MALFORMED FUCINE PATH '{0}': {1}", importData, ex.FormatException()));
            }
        }

        public override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty)
        {
            if (cachedFucineProperty.FucineAttribute.DefaultValue != null)
            {
                FucinePath fucinePath = new FucinePath(cachedFucineProperty.FucineAttribute.DefaultValue.ToString());
                if (fucinePath.IsValid())
                    return fucinePath;
            }

            return FucinePath.Current();
        }
    }

    public interface ICustomSpecEntity
    {
        void CustomSpec(EntityData data, ContentImportLog log);
    }

    public interface IMalleable
    {
        void Mold(EntityData data, ContentImportLog log);
    }
}