using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Entities;

namespace Roost.Elegiast
{
    internal static class ElegiastArchive
    {
        internal static void SetValue(string key, string value)
        {
            Watchman.Get<Stable>().Protag().SetOrOverwritePastLegacyEventRecord(key, value);
        }

        internal static string GetValue(string key)
        {
            return Watchman.Get<Stable>().Protag().GetPastLegacyEventRecord(key);
        }
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static void SetGlobalValue(string key, string value)
        {
            Roost.Elegiast.ElegiastArchive.SetValue(key, value);
        }

        public static string GetGlobalValue(string key)
        {
            return Roost.Elegiast.ElegiastArchive.GetValue(key);
        }
    }
}