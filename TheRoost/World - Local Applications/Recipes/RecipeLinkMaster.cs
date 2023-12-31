﻿using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Commands;
using SecretHistories.Logic;
using SecretHistories.States;
using SecretHistories.Fucine;

using Roost.Twins.Entities;
using Roost.Twins;

using HarmonyLib;

namespace Roost.World.Recipes
{
    public static class RecipeLinkMaster
    {

        const string CHANCE = "chance";
        const string LIMIT = "limit";
        const string FILTER = "filter";

        internal static void Enact()
        {
            //chance is an expression
            Machine.ClaimProperty<LinkedRecipeDetails, FucineExp<int>>(CHANCE, false, "100");
            Machine.Patch(
                original: typeof(LinkedRecipeDetails).GetPropertyInvariant(nameof(LinkedRecipeDetails.Chance)).GetGetMethod(),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(GetRefRecipeChance)));
            Machine.Patch(
                original: typeof(LinkedRecipeDetails).GetMethodInvariant(nameof(LinkedRecipeDetails.ShouldAlwaysSucceed)),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(ShouldAlwaysSucceed)));

            //expulsions use expressions
            Machine.ClaimProperty<Expulsion, FucineExp<bool>>(FILTER, false);
            Machine.ClaimProperty<Expulsion, FucineExp<int>>(LIMIT, false);

            Machine.Patch(
                original: Machine.GetMethod<Situation>("AdditionalRecipeSpawnToken"),
                transpiler: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(UseNewExpulsion)));

            //xtrigger can add temporary links
            Machine.Patch(
                original: typeof(RequiresExecutionState).GetMethodInvariant(nameof(RequiresExecutionState.GetNextValidLink)),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(EvaluateTempLinks)));

            AtTimeOfPower.OnPostImportExpulsion.Schedule<Expulsion>(MarkLimitPresence, PatchType.Prefix);
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

        //Situation.AdditionalRecipeSpawnToken()
        private static IEnumerable<CodeInstruction> UseNewExpulsion(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, typeof(RecipeLinkMaster).GetMethodInvariant(nameof(RecipeLinkMaster.FilterTokensWithExpulsion))),
            };

            Vagabond.CodeInstructionMask startMask = instruction => instruction.Calls(Machine.GetMethod<Situation>(nameof(Situation.GetElementTokens)));
            Vagabond.CodeInstructionMask endMask = instruction => instruction.opcode == OpCodes.Stloc_1;

            return instructions.ReplaceSegment(startMask, endMask, myCode, true, false, -2);
        }

        private static List<Token> FilterTokensWithExpulsion(Situation situation, Expulsion expulsion)
        {
            //Situation context was already set for links evaluation
            //so no need to resetcontext/mark situation here

            FucineExp<bool> filter = expulsion.RetrieveProperty<FucineExp<bool>>(FILTER);
            if (filter.isUndefined)
                return new List<Token>();

            List<Token> tokens = situation.GetElementTokensInSituation().FilterTokens(filter);

            FucineExp<int> limit = expulsion.RetrieveProperty<FucineExp<int>>(LIMIT);
            if (!limit.isUndefined)
                tokens = tokens.SelectRandom(limit.value);

            return tokens;
        }

        private static void MarkLimitPresence(Expulsion __instance)
        {
            FucineExp<int> limit = __instance.RetrieveProperty<FucineExp<int>>(LIMIT);
            if (!limit.isUndefined)
                __instance.Limit = 1;
        }


        public static readonly SortedList<int, Recipe> temporaryLinks = new SortedList<int, Recipe>(new DuplicateKeyComparer<int>());
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

        //RecipeConductor.GetLinkedRecipe() prefix
        private static bool EvaluateTempLinks(ref Recipe __result, Situation situation)
        {
            AspectsInContext aspectsInContext = Watchman.Get<HornedAxe>().GetAspectsInContext(situation);
            foreach (Recipe recipe in temporaryLinks.Values)
                if (recipe.RequirementsSatisfiedBy(aspectsInContext))
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
            if (recipe.IsNullEntity())
                Birdsong.TweetLoud($"Trying to push non-existed recipe link '{recipeId}'");
            else
                PushTemporaryRecipeLink(recipe, priority);
        }
    }
}


