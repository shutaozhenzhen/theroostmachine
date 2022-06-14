using System;
using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Commands.SituationCommands;
using SecretHistories.Core;
using SecretHistories.Fucine;

using Roost.Twins.Entities;

namespace Roost.World.Recipes
{
    public static class RecipeLinkMaster
    {
        private readonly static Action<Situation, Recipe, Expulsion, FucinePath> SpawnSituation = Delegate.CreateDelegate(typeof(Action<Situation, Recipe, Expulsion, FucinePath>), typeof(Situation).GetMethodInvariant("AdditionalRecipeSpawnToken")) as Action<Situation, Recipe, Expulsion, FucinePath>;
        public static readonly SortedList<int, Recipe> temporaryLinks = new SortedList<int, Recipe>(new DuplicateKeyComparer<int>());
        const string CHANCE = "chance";
        internal static void Enact()
        {
            //instant recipes are instant
            Machine.Patch(
                original: typeof(Situation).GetMethodInvariant(nameof(Situation.TransitionToState)),
                postfix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(ProceedWithInstantRecipe)));

            //inductions obey reqs and uses explusions
            Machine.Patch(
                original: typeof(AttemptAspectInductionCommand).GetMethodInvariant("PerformAspectInduction"),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(PerformAspectInduction)));

            //there are xtrigger links
            Machine.Patch(
                original: typeof(RecipeConductor).GetMethodInvariant(nameof(RecipeConductor.GetLinkedRecipe)),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(EvaluateTempLinks)));

            //chance is an expression
            Machine.ClaimProperty<LinkedRecipeDetails, FucineExp<int>>(CHANCE, false, "100");
            Machine.Patch(
                original: typeof(LinkedRecipeDetails).GetPropertyInvariant(nameof(LinkedRecipeDetails.Chance)).GetGetMethod(),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(GetRefRecipeChance)));
            Machine.Patch(
                original: typeof(LinkedRecipeDetails).GetMethodInvariant(nameof(LinkedRecipeDetails.ShouldAlwaysSucceed)),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(ShouldAlwaysSucceed)));
        }

        private static void ProceedWithInstantRecipe(Situation __instance)
        {
            if (__instance.TimeRemaining <= 0f && __instance.StateIdentifier != SecretHistories.Enums.StateEnum.Complete)
                __instance.State.Continue(__instance);
        }

        private static bool GetRefRecipeChance(LinkedRecipeDetails __instance, ref int __result)
        {
            __result = __instance.RetrieveProperty<FucineExp<int>>(CHANCE).value;
            return false;
        }

        private static bool ShouldAlwaysSucceed(LinkedRecipeDetails __instance, ref bool __result)
        {
            if (__instance.Challenges.Count == 0)
                if (int.TryParse(__instance.RetrieveProperty<FucineExp<int>>(CHANCE).formula, out int chance))
                    __result = chance >= 100;

            return false;
        }

        //AttemptAspectInductionCommand.PerformAspectInduction()
        private static bool PerformAspectInduction(Element aspectElement, Situation situation)
        {
            AspectsInContext aspectsInContext = Watchman.Get<HornedAxe>().GetAspectsInContext(situation.GetAspects(true), null);

            foreach (LinkedRecipeDetails linkedRecipeDetails in aspectElement.Induces)
                TrySpawnSituation(situation, linkedRecipeDetails, aspectsInContext);

            return false;
        }

        public static void TrySpawnSituation(Situation situation, LinkedRecipeDetails linkedRecipeDetails, AspectsInContext aspectsInContext)
        {
            if (linkedRecipeDetails.ShouldAlwaysSucceed() || UnityEngine.Random.Range(1, 101) <= linkedRecipeDetails.Chance)
            {
                Recipe recipe = Watchman.Get<Compendium>().GetEntityById<Recipe>(linkedRecipeDetails.Id);
                if (recipe.RequirementsSatisfiedBy(aspectsInContext))
                    SpawnSituation(situation, recipe, linkedRecipeDetails.Expulsion, FucinePath.Current());
            }
        }

        //RecipeConductor.GetLinkedRecipe() prefix
        private static bool EvaluateTempLinks(ref Recipe __result, AspectsInContext ____aspectsInContext)
        {
            foreach (Recipe recipe in temporaryLinks.Values)
                if (recipe.RequirementsSatisfiedBy(____aspectsInContext))
                {
                    __result = recipe;
                    break;
                }

            temporaryLinks.Clear();
            return __result == null;
        }

        internal static void PushTemporaryRecipeLink(Recipe recipe, int priority)
        {
            temporaryLinks.Add(priority, recipe);
        }

        private class DuplicateKeyComparer<TKey> : IComparer<TKey> where TKey : IComparable
        {
            public int Compare(TKey x, TKey y)
            {
                int result = x.CompareTo(y);

                if (result == 0)
                    return 1;   // Handle equality as beeing greater
                else
                    return result;
            }
        }
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static void PushTemporaryRecipeLink(Recipe recipe, int priority = 0)
        {
            Roost.World.Recipes.RecipeLinkMaster.PushTemporaryRecipeLink(recipe, priority);
        }

        public static void PushTemporaryRecipeLink(string recipeId, int priority = 0)
        {
            Recipe recipe = Machine.GetEntity<Recipe>(recipeId);
            if (recipeId == null)
                Birdsong.Tweet(VerbosityLevel.Essential, 1, $"Trying to push non-existed recipe link '{recipeId}'");
            else
                PushTemporaryRecipeLink(recipe, priority);
        }
    }
}


