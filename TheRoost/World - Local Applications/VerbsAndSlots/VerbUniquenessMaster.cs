using System;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Commands;
using SecretHistories.UI;
using SecretHistories.Entities;

using Roost.Twins.Entities;

using HarmonyLib;

namespace Roost.World.Verbs
{
    internal static class VerbUniquenessMaster
    {

        private const string MAX = "maxUnique";

        public static void Enact()
        {
            Machine.ClaimProperty<Verb, FucineExp<int>>(MAX, false, "1");

            Machine.Patch(
                original: Machine.GetMethod<SituationCreationCommand>(nameof(SituationCreationCommand.Execute)),
                transpiler: typeof(VerbUniquenessMaster).GetMethodInvariant(nameof(InjectVerbCounting)));
        }

        private static IEnumerable<CodeInstruction> InjectVerbCounting(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, typeof(VerbUniquenessMaster).GetMethodInvariant(nameof(AreThereTooMuchVerbs))),
            };

            Vagabond.CodeInstructionMask start = instruction => true;
            Vagabond.CodeInstructionMask end = instruction => instruction.opcode == OpCodes.Brfalse_S;
            
            return instructions.ReplaceSegment(start, end, myCode, replaceStart: true, replaceEnd: false);
        }

        private static bool AreThereTooMuchVerbs(SituationCreationCommand newSituation)
        {
            string verbId = newSituation.VerbId;
            Verb verb = Watchman.Get<Compendium>().GetEntityById<Verb>(verbId);

            if (!verb.IsValid())
                return false;

            int maxCount = verb.RetrieveProperty<FucineExp<int>>(MAX).value;

            if (maxCount < 0)
                return false;

            int matchingCount = Watchman.Get<HornedAxe>().GetRegisteredSituations().Count(situation => situation.Unique && situation.VerbId == verbId);

            return matchingCount >= maxCount;
        }
    }
}
