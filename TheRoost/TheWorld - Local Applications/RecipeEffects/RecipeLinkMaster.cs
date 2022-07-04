using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Commands;
using SecretHistories.Commands.SituationCommands;
using SecretHistories.Core;
using SecretHistories.Fucine;
using Roost.Twins.Entities;

using HarmonyLib;

namespace Roost.World.Recipes
{
    public static class RecipeLinkMaster
    {
        private readonly static Action<Situation, Recipe, Expulsion, FucinePath> SpawnSituation = Delegate.CreateDelegate(typeof(Action<Situation, Recipe, Expulsion, FucinePath>), typeof(Situation).GetMethodInvariant("AdditionalRecipeSpawnToken")) as Action<Situation, Recipe, Expulsion, FucinePath>;

        const string CHANCE = "chance";
        const string LIMIT = "limit";
        const string FILTER = "filter";
        const string PREVIEW = "preview";
        const string PREVIEW_LABEL = "previewLabel";

        internal static void Enact()
        {
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

            //expulsions use expressions
            Machine.ClaimProperty<Expulsion, FucineExp<bool>>(FILTER, false);
            Machine.ClaimProperty<Expulsion, FucineExp<int>>(LIMIT, false);
            Machine.AddImportMolding<Expulsion>(Entities.MoldingsStorage.ConvertExpulsionFilters);
            Machine.Patch(
                original: typeof(Situation).GetMethodInvariant("AdditionalRecipeSpawnToken"),
                transpiler: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(UseNewExpulsion)));

            Machine.ClaimProperty<Recipe, string>(PREVIEW);
            Machine.ClaimProperty<Recipe, string>(PREVIEW_LABEL);
            Machine.Patch(
                original: typeof(Situation).GetMethodInvariant(nameof(Situation.ReactToLatestRecipePrediction)),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(DisplayPreview)));
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

        private static IEnumerable<CodeInstruction> UseNewExpulsion(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, typeof(RecipeLinkMaster).GetMethodInvariant(nameof(RecipeLinkMaster.FilterTokensWithExpulsion))),
                new CodeInstruction(OpCodes.Stloc_2), //for some reason the code doesn't want me to load the data directly into the loc_0
                new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Stloc_0),
            };

            Vagabond.CodeInstructionMask mask = instruction => instruction.opcode == OpCodes.Bgt_S;
            return instructions.ReplaceBeforeMask(mask, myCode, true);
        }

        private static List<Token> FilterTokensWithExpulsion(Situation situation, Expulsion expulsion)
        {
            FucineExp<bool> filter = expulsion.RetrieveProperty<FucineExp<bool>>(FILTER);
            if (filter.isUndefined)
                return new List<Token>();

            Twins.Crossroads.MarkLocalSituation(situation);
            List<Token> tokens = situation.GetElementTokensInSituation().FilterTokens(filter);

            FucineExp<int> limit = expulsion.RetrieveProperty<FucineExp<int>>(LIMIT);
            if (!limit.isUndefined)
                tokens = tokens.SelectRandom(limit.value);

            Twins.Crossroads.ResetCache();

            return tokens;
        }

        private static readonly Action<RecipePrediction, string> predictionTitleSet = typeof(RecipePrediction).GetPropertyInvariant(nameof(RecipePrediction.Title)).GetSetMethod(true).
            CreateDelegate(typeof(Action<RecipePrediction, string>)) as Action<RecipePrediction, string>;
        private static readonly Action<RecipePrediction, string> predictionDescriptionSet = typeof(RecipePrediction).GetPropertyInvariant(nameof(RecipePrediction.Description)).GetSetMethod(true).
            CreateDelegate(typeof(Action<RecipePrediction, string>)) as Action<RecipePrediction, string>;
        private static void DisplayPreview(RecipePrediction newRecipePrediction, Situation __instance)
        {
            if (__instance.State.Identifier != SecretHistories.Enums.StateEnum.Unstarted)
                return;
            Recipe recipe = Machine.GetEntity<Recipe>(newRecipePrediction.RecipeId);

            string previewLabel = recipe.RetrieveProperty<string>(PREVIEW_LABEL);
            if (previewLabel != null)
                predictionTitleSet(newRecipePrediction, previewLabel);

            string previewDescription = recipe.RetrieveProperty<string>(PREVIEW);
            if (previewDescription != null)
                predictionDescriptionSet(newRecipePrediction, previewDescription);
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


