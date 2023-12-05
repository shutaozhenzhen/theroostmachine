using System;
using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.Logic;

using Roost.Twins;
using Roost.Twins.Entities;

namespace Roost.World.Recipes
{
    public static class GrandReqsMaster
    {
        const string GRAND_REQS = "grandreqs";

        internal static void Enact()
        {
            Machine.ClaimProperty<Recipe, Dictionary<FucineExp<int>, FucineExp<int>>>(GRAND_REQS);

            //wherever we get aspects in context for situation, that means we're going to evaluate recipes for it
            //that means we need to provide the context
            Machine.Patch(
                original: typeof(Situation).GetMethodInvariant(nameof(Situation.GetAspects)),
                prefix: typeof(GrandReqsMaster).GetMethodInvariant(nameof(MarkSituation)));

            Machine.Patch<Recipe>(nameof(Recipe.RequirementsSatisfiedBy),
                postfix: typeof(GrandReqsMaster).GetMethodInvariant(nameof(CheckGrandReqsForSituation)));
        }

        //Situation.GetAspectsInContext()
        public static bool situationIsFresh = true;
        private static void MarkSituation(Situation __instance)
        {
            if (situationIsFresh)
            {
                Crossroads.ResetCache();
                Crossroads.MarkLocalSituation(__instance);
            }
        }

        private static void CheckGrandReqsForSituation(Recipe __instance, ref bool __result)
        {
            if (__result == false)
                return;

            Dictionary<FucineExp<int>, FucineExp<int>> grandreqs = __instance.RetrieveProperty(GRAND_REQS) as Dictionary<FucineExp<int>, FucineExp<int>>;
            if (grandreqs == null || grandreqs.Count == 0)
                return;

            __result = CheckGrandReqs(grandreqs);
        }

        public static bool CheckGrandReqs(Dictionary<FucineExp<int>, FucineExp<int>> grandreqs)
        {
            foreach (KeyValuePair<FucineExp<int>, FucineExp<int>> req in grandreqs)
            {
                int presentValue = req.Key.value;
                int requiredValue = req.Value.value;

                //Birdsong.Sing($"{req.Key.formula}: {req.Value.formula} --> {presentValue}: {requiredValue}");

                if (!RequirementsArbiter.CheckRequirement(requiredValue, presentValue))
                    return false;
            }

            return true;
        }

    }
}