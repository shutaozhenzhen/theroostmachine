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
        public readonly static Action<Situation, Recipe, Expulsion, FucinePath> SpawnNewSituation = Delegate.CreateDelegate(typeof(Action<Situation, Recipe, Expulsion, FucinePath>), typeof(Situation).GetMethodInvariant("SpawnNewSituation")) as Action<Situation, Recipe, Expulsion, FucinePath>;
        private static readonly List<Recipe> xtriggerLinks = new List<Recipe>();
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

            //xtrigger links are evaluated
            Machine.Patch(
                original: typeof(RecipeConductor).GetMethodInvariant(nameof(RecipeConductor.GetLinkedRecipe)),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(CheckXtriggerLinks)));

            Machine.ClaimProperty<LinkedRecipeDetails, Funcine<int>>(CHANCE, false, "100");
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
            __result = __instance.RetrieveProperty<Funcine<int>>(CHANCE).value;
            return false;
        }

        private static bool ShouldAlwaysSucceed(LinkedRecipeDetails __instance, ref bool __result)
        {
            const string maxChance = "100";
            __result = __instance.Challenges.Count == 0 && __instance.RetrieveProperty<Funcine<int>>(CHANCE).formula == maxChance;
            return false;
        }

        //AttemptAspectInductionCommand.PerformAspectInduction()
        private static bool PerformAspectInduction(Element aspectElement, Situation situation)
        {
            AspectsInContext aspectsInContext = Watchman.Get<HornedAxe>().GetAspectsInContext(situation.GetAspects(true), null);
            foreach (LinkedRecipeDetails linkedRecipeDetails in aspectElement.Induces)
                if (linkedRecipeDetails.Chance < UnityEngine.Random.Range(1, 101))
                {
                    Recipe recipe = Watchman.Get<Compendium>().GetEntityById<Recipe>(linkedRecipeDetails.Id);
                    if (recipe.RequirementsSatisfiedBy(aspectsInContext))
                        SpawnNewSituation(situation, recipe, linkedRecipeDetails.Expulsion, FucinePath.Current());
                }

            return false;
        }

        //RecipeConductor.GetLinkedRecipe() prefix
        private static bool CheckXtriggerLinks(ref Recipe __result, AspectsInContext ____aspectsInContext)
        {
            foreach (Recipe recipe in xtriggerLinks)
                if (recipe.RequirementsSatisfiedBy(____aspectsInContext))
                {
                    __result = recipe;
                    break;
                }

            xtriggerLinks.Clear();
            return __result == null;
        }

        internal static void PushXtriggerLink(Recipe recipe, int orderShift)
        {
            xtriggerLinks.Insert(Math.Min(xtriggerLinks.Count, xtriggerLinks.Count + orderShift), recipe);
        }
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static void PushXtriggerLink(Recipe recipe, int orderShift = 0)
        {
            Roost.World.Recipes.RecipeLinkMaster.PushXtriggerLink(recipe, orderShift);
        }

        public static void PushXtriggerLink(string recipeId, int orderShift = 0)
        {
            Recipe recipe = Machine.GetEntity<Recipe>(recipeId);
            if (recipeId == null)
                Birdsong.Sing($"Trying to push non-existed recipe link '{recipeId}'");
            else
                PushXtriggerLink(recipe, orderShift);
        }
    }
}


