﻿using System;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Commands;
using SecretHistories.UI;
using SecretHistories.Entities;

using Roost.Twins.Entities;

namespace Roost.World.Verbs
{
    internal static class VerbStickySlotsMaster
    {

        private const string stickySlots = "stickySlots";

        public static void Enact()
        {
            Machine.ClaimProperty<Verb, bool>(stickySlots, false, false);

            Machine.Patch(
                original: Machine.GetMethod<Situation>(nameof(Situation.DumpUnstartedBusiness)),
                prefix: typeof(VerbStickySlotsMaster).GetMethodInvariant(nameof(DontDumpIfSticky)));

            Machine.Patch(
                original: Machine.GetMethod<Situation>("Open"),
                prefix: typeof(VerbStickySlotsMaster).GetMethodInvariant(nameof(DontDumpIfSticky)));
        }

        private static bool DontDumpIfSticky(Situation __instance)
        {
            return !__instance.Verb.RetrieveProperty<bool>(stickySlots);
        }
    }
}
