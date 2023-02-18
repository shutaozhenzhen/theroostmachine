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
            //grand reqs
            Machine.ClaimProperty<Recipe, Dictionary<FucineExp<int>, FucineExp<int>>>(GRAND_REQS);

            Machine.Patch(
                original: typeof(Situation).GetMethodInvariant(nameof(Situation.GetAspects)),
                prefix: typeof(GrandReqsMaster).GetMethodInvariant(nameof(StoreSituationForReqs)));

            AtTimeOfPower.RecipeRequirementsCheck.Schedule<Recipe, AspectsInContext>(CheckGrandReqsForSituation);
        }

        public static bool CheckGrandReqs(Dictionary<FucineExp<int>, FucineExp<int>> grandreqs)
        {
            //grand reqs usually require the calling context to be marked as "local" for the Crossroads
            //ie MarkCurrentSituation, MarkCurrentSphere, MarkCurrentToken
            //so don't forget to do that (!!)

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

        //normal reqs don't know for what Situation they are working now; but for grandreqs it's a vital info
        //thus, we need to store and retrieve the Situation; but reqs are called from an inconvenietly many places
        //so we store it on Situation.GetAspects(), which preceeds every req check anyway
        private static Situation currentSituation;
        private static void StoreSituationForReqs(Situation __instance)
        {
            currentSituation = __instance;
        }

        private static bool CheckGrandReqsForSituation(Recipe __instance, AspectsInContext aspectsInContext)
        {
            Dictionary<FucineExp<int>, FucineExp<int>> grandreqs = __instance.RetrieveProperty(GRAND_REQS) as Dictionary<FucineExp<int>, FucineExp<int>>;
            if (grandreqs == null || grandreqs.Count == 0)
                return true;

            Crossroads.MarkLocalSituation(currentSituation);
            bool result = CheckGrandReqs(grandreqs);
            Crossroads.ResetCache();
            return result;
        }
    }
}