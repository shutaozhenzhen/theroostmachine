using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Commands;
using SecretHistories.Logic;
using SecretHistories.States;
using SecretHistories.Fucine;

using Roost.Twins.Entities;

using HarmonyLib;

namespace Roost.World.Recipes
{
    public static class RecipeLinkMaster
    {

        const string CHANCE = "chance";
        const string LIMIT = "limit";
        const string FILTER = "filter";
        const string PREVIEW = "preview";
        const string PREVIEW_LABEL = "previewLabel";

        internal static void Enact()
        {
            //there are xtrigger links
            Machine.Patch(
                original: typeof(RequiresExecutionState).GetMethodInvariant(nameof(RequiresExecutionState.GetNextValidLink)),
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
                original: typeof(Situation).GetMethodInvariant("AdditionalRecipeSpawnToken", new Type[] { typeof(Recipe), typeof(Expulsion), typeof(FucinePath) }),
                transpiler: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(UseNewExpulsion)));

            Machine.ClaimProperty<Recipe, string>(PREVIEW);
            Machine.ClaimProperty<Recipe, string>(PREVIEW_LABEL);
            Machine.Patch(
                original: typeof(RecipeNote).GetMethodInvariant(nameof(RecipeNote.StartDescription)),
                postfix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(DisplayPreview)));

            Machine.Patch(
                original: typeof(UnstartedState).GetMethodInvariant(nameof(UnstartedState.Exit)),
                postfix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(DisplayStartDescription)));
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
            AspectsInContext aspectsInContext = situation.GetAspectsInContext(true);
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

        private static IEnumerable<CodeInstruction> UseNewExpulsion(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, typeof(RecipeLinkMaster).GetMethodInvariant(nameof(RecipeLinkMaster.FilterTokensWithExpulsion))),
            };

            Vagabond.CodeInstructionMask startMask = instruction => instruction.opcode == OpCodes.Ldnull;
            Vagabond.CodeInstructionMask endMask = instruction => instruction.Calls(typeof(TokenSelector).GetMethodInvariant(nameof(TokenSelector.SelectRandomTokens)));

            return instructions.ReplaceSegment(startMask, endMask, myCode, true, true);
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

        static bool dontDisplayPreview = false;
        private static readonly Action<RecipeNote, string> predictionTitleSet = typeof(RecipeNote).GetPropertyInvariant(nameof(RecipeNote.Title)).GetSetMethod(true).
            CreateDelegate(typeof(Action<RecipeNote, string>)) as Action<RecipeNote, string>;
        private static readonly Action<RecipeNote, string> predictionDescriptionSet = typeof(RecipeNote).GetPropertyInvariant(nameof(RecipeNote.Description)).GetSetMethod(true).
            CreateDelegate(typeof(Action<RecipeNote, string>)) as Action<RecipeNote, string>;
        private static void DisplayPreview(Recipe recipe, Situation situation, ref RecipeNote __result)
        {
            if (dontDisplayPreview || situation.State.Identifier != SecretHistories.Enums.StateEnum.Unstarted)
                return;

            string previewLabel = recipe.RetrieveProperty<string>(PREVIEW_LABEL);
            if (previewLabel != null)
                predictionTitleSet(__result, previewLabel);

            string previewDescription = recipe.RetrieveProperty<string>(PREVIEW);
            if (previewDescription != null)
                predictionDescriptionSet(__result, previewDescription);
        }

        private static void DisplayStartDescription(Situation situation)
        {
            Recipe recipe = situation.CurrentRecipe;
            if (!recipe.HasCustomProperty(PREVIEW) && !recipe.HasCustomProperty(PREVIEW_LABEL))
                return;

            dontDisplayPreview = true;
            RecipeNote notification = RecipeNote.StartDescription(recipe, situation, true);
            situation.ReceiveNote(notification, Context.Metafictional());
            dontDisplayPreview = false;
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
                Birdsong.Tweet(VerbosityLevel.Essential, 1, $"Trying to push non-existed recipe link '{recipeId}'");
            else
                PushTemporaryRecipeLink(recipe, priority);
        }
    }
}


