using System;
using SecretHistories.Fucine;

namespace Roost.Beachcomber
{
    public static class Pantiment
    {
        public static bool GetDefaultValue(ref object __result)
        {
            __result = string.Empty;
            return false;
        }

        public static ImportMethods.ImportFunc GetImportFunc(AbstractImporter importer)
        {
            return Delegate.CreateDelegate(typeof(ImportMethods.ImportFunc), importer, importer.GetType().GetMethod("Import"))
                as ImportMethods.ImportFunc;
        }
    }

}

namespace Roost
{
    public static partial class Machine
    {
        public static T ConvertTo<T>(this object value) where T : IConvertible
        {
            return (T)ImportMethods.ConvertValue(value, typeof(T));
        }
    }
}