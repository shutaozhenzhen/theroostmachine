using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Assets.Scripts.Application.Entities.NullEntities;
using SecretHistories.Entities;
using SecretHistories.Services;
using SecretHistories.Commands;

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
        internal static void SetGlobalValue(string key, string value)
        {
            Roost.Elegiast.ElegiastArchive.SetValue(key, value);
        }

        internal static string GetGlobalValue(string key)
        {
            return Roost.Elegiast.ElegiastArchive.GetValue(key);
        }
    }
}