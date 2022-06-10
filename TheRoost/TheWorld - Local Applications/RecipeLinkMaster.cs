﻿using System;
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

            //there are xtrigger links
            Machine.Patch(
                original: typeof(RecipeConductor).GetMethodInvariant(nameof(RecipeConductor.GetLinkedRecipe)),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(CheckXTriggerLinks)));

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
        private static bool CheckXTriggerLinks(ref Recipe __result, AspectsInContext ____aspectsInContext)
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
                Birdsong.Tweet($"Trying to push non-existed recipe link '{recipeId}'");
            else
                PushXtriggerLink(recipe, orderShift);
        }
    }
}


