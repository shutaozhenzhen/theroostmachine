using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Entities;

namespace Roost.Elegiast
{
    internal static class ElegiastArchive
    {
        private static readonly List<string> permanentLevers = new List<string>();

        internal static void MarkLeverAsPermanent(string levers)
        {
            permanentLevers.Add(levers);
        }

        internal static void SetLeverPast(string lever, string value)
        {
            Watchman.Get<Stable>().Protag().SetOrOverwritePastLegacyEventRecord(lever, value);
        }

        internal static void SetLeverFuture(string lever, string value)
        {
            Watchman.Get<Stable>().Protag().SetOrOverwritePastLegacyEventRecord(lever, value);
        }

        internal static string GetLeverPast(string lever)
        {
            return Watchman.Get<Stable>().Protag().GetPastLegacyEventRecord(lever);
        }

        internal static string GetLeverFuture(string lever)
        {
            return Watchman.Get<Stable>().Protag().GetFutureLegacyEventRecord(lever);
        }
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static void SetLeverPast(string lever, string value)
        {
            Roost.Elegiast.ElegiastArchive.SetLeverPast(lever, value);
        }

        public static string GetLeverPast(string lever)
        {
            return Roost.Elegiast.ElegiastArchive.GetLeverPast(lever);
        }

        public static void SetLeverFuture(string lever, string value)
        {
            Roost.Elegiast.ElegiastArchive.SetLeverFuture(lever, value);
        }

        public static string GetLeverFuture(string lever)
        {
            return Roost.Elegiast.ElegiastArchive.GetLeverFuture(lever);
        }
    }
}