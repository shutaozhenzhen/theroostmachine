using System;
using System.Collections.Generic;

namespace Roost.Beachcomber
{
    internal static class Ostrich
    {
        private readonly static Dictionary<Type, List<string>> ignoredProperties = new Dictionary<Type, List<string>>();
        private readonly static HashSet<string> ignoredEntityGroups = new HashSet<string>() { "examples" };

        internal static bool ignoreVanillaContent { get; set; }

        internal static void AddIgnoredProperty<TEntity>(string propertyName)
        {
            if (ignoredProperties[typeof(TEntity)] == null)
                ignoredProperties[typeof(TEntity)] = new List<string>();

            if (ignoredProperties[typeof(TEntity)].Contains(propertyName) == false)
                ignoredProperties[typeof(TEntity)].Add(propertyName);
        }

        internal static void AddIgnoredEntityGroup<TEntity>(string groupId)
        {
            if (ignoredEntityGroups.Contains(groupId) == false)
                ignoredEntityGroups.Add(groupId);
        }

        internal static bool Ignores(Type entityType, string propertyName)
        {
            return ignoredProperties.ContainsKey(entityType) && ignoredProperties[entityType].Contains(propertyName);
        }

        internal static bool Ignores(string groupId)
        {
            return ignoredEntityGroups.Contains(groupId);
        }
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static void AddIgnoredProperty<T>(string propertyName)
        {
            Beachcomber.Ostrich.AddIgnoredProperty<T>(propertyName);
        }

        public static void AddIgnoredEntityGroup<T>(string propertyName)
        {
            Beachcomber.Ostrich.AddIgnoredEntityGroup<T>(propertyName);
        }

        public static bool PropertyIsIgnored(Type entityType, string propertyName)
        {
            return Beachcomber.Ostrich.Ignores(entityType, propertyName);
        }

        public static bool EntityGroupIsIgnored(string groupId)
        {
            return Beachcomber.Ostrich.Ignores(groupId);
        }
    }
}