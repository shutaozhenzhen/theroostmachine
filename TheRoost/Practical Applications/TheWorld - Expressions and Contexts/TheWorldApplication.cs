using System;
using System.Collections.Generic;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Enums;

using TheRoost.PracticalApplications.World.Entities;

namespace TheRoost.PracticalApplications.World
{
    static class TheWorldApplication
    {
        //Dictionary<Funcine<int>, Funcine<int>> - both parts are basically numbers which are checked by normal CS req rules
        const string refReqs = "@reqs";
        //Dictionary<string, Funcine<int>> - left side is just an element id, right side is an amount
        const string refEffects = "@effects";
        //Dictionary<Funcine<bool>, List<RefMutationEffect>> - right side is a filter by which affected tokens are selected, left is list of mutation effects;
        //mutation effects are identical to normal ones, not counting WeirdSpecs quirks, with the only difference of Level being Funcine<int>
        const string refMutations = "@mutations";

        internal static void Enact()
        {
            AtTimeOfPower.RecipeRequirementsCheck.Schedule<Recipe, AspectsInContext, bool>(RefReqs);
            AtTimeOfPower.RecipeExecution.Schedule<RecipeCompletionEffectCommand, Situation>(ExecuteEffectsWithReferences, PatchType.Prefix);

            Birdsong.ClaimProperty<Recipe, Dictionary<Funcine<int>, Funcine<int>>>(refReqs);
            Birdsong.ClaimProperty<Recipe, Dictionary<string, Funcine<int>>>(refEffects);
            Birdsong.ClaimProperty<Recipe, Dictionary<Funcine<bool>, List<RefMutationEffect>>>(refMutations);
        }

        private static bool RefReqs(Recipe __instance, AspectsInContext aspectsinContext, bool __result)
        {
            Dictionary<Funcine<int>, Funcine<int>> reqs = __instance.RetrieveProperty<Dictionary<Funcine<int>, Funcine<int>>>(refReqs);
            if (reqs == null)
            {
                __result = true;
                return true;
            }

            TheWorldHolder.ResetCache();

            //what I am about to do here should be illegal (and will be at some point of time in the bright future of humankind)
            //but I really need to know a *situation* instead of just aspects; and there is no easier way to find it
            bool situaitionFound = false;
            foreach (Situation situation in Watchman.Get<HornedAxe>().GetRegisteredSituations())
            {
                if (situation.GetAspects(true).AspectsEqual(aspectsinContext.AspectsInSituation))
                {
                    TheWorldHolder.SetLocalSituation(situation);

                    situaitionFound = true;
                    break;
                }
            }

            if (!situaitionFound)
            {
                Birdsong.Sing("Something strange happened. Cannot identify the current situation for requirements check.");
                return true;
            }

            foreach (KeyValuePair<Funcine<int>, Funcine<int>> req in reqs)
            {
                int leftValue = req.Key.result;
                int rightValue = req.Value.result;

                Birdsong.Sing("Reqs for {0}:\nLeft: {1}\nRight: {2}\n{3}: {4} - {5}", __instance.Id, req.Key, req.Value, leftValue, rightValue, (rightValue > 0 && leftValue < rightValue) || (rightValue <= 0 && leftValue >= Math.Abs(rightValue)) ? "@reqs aren't satisfied" : "@reqs are satisfied");

                if (rightValue >= 0 && leftValue < rightValue)
                {
                    __result = false;
                    return false;
                }
                if (rightValue <= 0 && leftValue >= Math.Abs(rightValue))
                {
                    __result = false;
                    return false;
                }
            }

            __result = true;
            return true;
        }

        private static bool AspectsEqual(this AspectsDictionary dictionary1, AspectsDictionary dictionary2)
        {
            if (dictionary1 == dictionary2) return true;
            if ((dictionary1 == null) || (dictionary2 == null)) return false;
            if (dictionary1.Count != dictionary2.Count) return false;

            foreach (string key in dictionary2.Keys)
                if (dictionary1.ContainsKey(key) == false || dictionary1[key] != dictionary2[key])
                    return false;

            return true;
        }

        private static void ExecuteEffectsWithReferences(RecipeCompletionEffectCommand __instance, Situation situation)
        {
            TheWorldHolder.ResetCache();
            TheWorldHolder.SetLocalSituation(situation);

            Sphere storage = situation.GetSingleSphereByCategory(SphereCategory.SituationStorage);

            RefMutations(__instance.Recipe, storage);
            RefEffects(__instance.Recipe, storage);
        }

        private static void RefEffects(Recipe recipe, Sphere storage)
        {
            Dictionary<string, Funcine<int>> effects = recipe.RetrieveProperty<Dictionary<string, Funcine<int>>>(refEffects);
            if (effects == null)
                return;

            foreach (string aspect in effects.Keys)
                storage.ModifyElementQuantity(aspect, effects[aspect].result, new Context(Context.ActionSource.SituationEffect));
        }

        private static void RefMutations(Recipe recipe, Sphere sphere)
        {
            Dictionary<Funcine<bool>, List<RefMutationEffect>> mutations = recipe.RetrieveProperty<Dictionary<Funcine<bool>, List<RefMutationEffect>>>(refMutations);
            if (mutations == null)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (Funcine<bool> filter in mutations.Keys)
            {
                List<Token> targets = tokens.FilterTokens(filter);

                if (targets.Count > 0)
                    foreach (RefMutationEffect mutationEffect in mutations[filter])
                    {
                        int level = mutationEffect.Level.result;
                        foreach (Token token in targets)
                            token.Payload.SetMutation(mutationEffect.Mutate, level, mutationEffect.Additive);
                    }
            }
        }
    }
}
