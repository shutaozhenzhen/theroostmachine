using System;
using System.Collections;
using SecretHistories.Fucine.DataImport;
using Roost.Beachcomber;
using Roost;

namespace SecretHistories.Fucine
{
    public interface IImporter { object Import(object data, Type type, ContentImportLog log); }
    public class PropertyPanImporter : AbstractImporter, IImporter
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
        public virtual object Import(object data, Type type, ContentImportLog log)
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

            return FactoryInstantiator.CreateObjectWithDefaultConstructor(propertyType);
        }
    }

    class ListPanImporter : PropertyPanImporter
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new ListPanImporter(); return false; }
        public override object Import(object data, Type type, ContentImportLog log) { return Panimporter.ImportListWithDefaultSubImporter(data, type, log); }

        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        { return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType); }
    }

    class DictPanImporer : PropertyPanImporter
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new DictPanImporer(); return false; }
        public override object Import(object data, Type type, ContentImportLog log) { return Panimporter.ImportDictionaryWithDefaultSubImporters(data, type, log); }

        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        { return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType); }
    }

    class ValuePanImporter : PropertyPanImporter
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new ValuePanImporter(); return false; }
        public override object Import(object data, Type type, ContentImportLog log) { return Panimporter.ImportSimpleValue(data, type, log); }

        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        { return cachedFucineProperty.FucineAttribute.DefaultValue; }
    }

    class SubEntityPanImporter : PropertyPanImporter
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new SubEntityPanImporter(); return false; }
        public override object Import(object data, Type type, ContentImportLog log) { return Panimporter.ImportFucineEntity(data, type, log); }
        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        {
            if (cachedFucineProperty.FucineAttribute.DefaultValue != null)
                return Panimporter.ConstuctFromParameters(cachedFucineProperty.FucineAttribute.DefaultValue, cachedFucineProperty.ThisPropInfo.PropertyType, log);
            return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType);
        }
    }

    class ConstructorPanImporter : PropertyPanImporter
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new ConstructorPanImporter(); return false; }
        public override object Import(object data, Type type, ContentImportLog log) { return Panimporter.ConstuctFromParameters(data, type, log); }
        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        {
            if (cachedFucineProperty.FucineAttribute.DefaultValue != null)
                return Panimporter.ConstuctFromParameters(cachedFucineProperty.FucineAttribute.DefaultValue, cachedFucineProperty.ThisPropInfo.PropertyType, log);

            return FactoryInstantiator.CreateObjectWithDefaultConstructor(cachedFucineProperty.ThisPropInfo.PropertyType);
        }
    }

    class FucinePathPanImporter : PropertyPanImporter
    {
        public static bool CreateImporterInstance(ref AbstractImporter __result) { __result = new FucinePathPanImporter(); return false; }
        public override object Import(object pathData, Type type, ContentImportLog log) { return Panimporter.ImportFucinePath(pathData, type, log); }
        protected override object GetDefaultValue<T>(CachedFucineProperty<T> cachedFucineProperty, ContentImportLog log)
        {
            return Roost.Twins.TwinsParser.ParseSpherePath(cachedFucineProperty.FucineAttribute.DefaultValue?.ToString());
        }
    }

    class CustomListPanImporter : AbstractImporter
    {
        public CustomListPanImporter(Type entryImporter)
        {
            this._entryImporter = Activator.CreateInstance(entryImporter) as IImporter;
        }

        private IImporter _entryImporter;
        public override bool TryImportProperty<T>(T entity, CachedFucineProperty<T> cachedFucineProperty, EntityData entityData, ContentImportLog log)
        {
            string propertyName = cachedFucineProperty.LowerCaseName;
            Type propertyType = cachedFucineProperty.ThisPropInfo.PropertyType;

            try
            {
                if (entityData.ValuesTable.Contains(propertyName))
                {
                    object result = Panimporter.ImportList(entityData.ValuesTable[propertyName], propertyType, log, _entryImporter.Import);
                    cachedFucineProperty.SetViaFastInvoke(entity, result);
                    return true;
                }

                cachedFucineProperty.SetViaFastInvoke(entity, FactoryInstantiator.CreateObjectWithDefaultConstructor(propertyType));
                return false;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    class CustomDictPanImporter : AbstractImporter
    {
        public CustomDictPanImporter(Type KeyImporter, Type ValueImporter)
        {
            this._keyImporter = Activator.CreateInstance(KeyImporter) as IImporter;
            this._valueImporter = Activator.CreateInstance(ValueImporter) as IImporter;
        }

        private IImporter _keyImporter;
        private IImporter _valueImporter;

        public override bool TryImportProperty<T>(T entity, CachedFucineProperty<T> cachedFucineProperty, EntityData entityData, ContentImportLog log)
        {
            string propertyName = cachedFucineProperty.LowerCaseName;
            Type propertyType = cachedFucineProperty.ThisPropInfo.PropertyType;

            try
            {
                if (entityData.ValuesTable.Contains(propertyName))
                {
                    object result = Panimporter.ImportDictionary(entityData.ValuesTable[propertyName], propertyType, log, _keyImporter.Import, _valueImporter.Import);
                    cachedFucineProperty.SetViaFastInvoke(entity, result);
                    return true;
                }

                cachedFucineProperty.SetViaFastInvoke(entity, FactoryInstantiator.CreateObjectWithDefaultConstructor(propertyType));
                return false;
            }
            catch (Exception ex)
            {
                throw ex;
            }
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

        // Token: 0x060010D2 RID: 4306 RVA: 0x0002F57C File Offset: 0x0002D77C
        public FucineCustomList(Type EntryImporter) { this.EntryImporter = EntryImporter; }
        public override AbstractImporter CreateImporterInstance() { return new CustomListPanImporter(EntryImporter); }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class FucineCustomDict : Fucine
    {
        public Type KeyImporter { get; private set; }
        public Type ValueImporter { get; private set; }

        // Token: 0x060010D2 RID: 4306 RVA: 0x0002F57C File Offset: 0x0002D77C
        public FucineCustomDict(Type KeyImporter, Type ValueImporter) { this.KeyImporter = KeyImporter; this.ValueImporter = ValueImporter; }
        public override AbstractImporter CreateImporterInstance() { return new CustomDictPanImporter(KeyImporter, ValueImporter); }
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