using System;

using SecretHistories.Entities;
using SecretHistories.Commands;
using SecretHistories.States;

namespace Roost.World.Recipes
{
    public static class RecipeNotesMaster
    {
        const string PREVIEW = "previewDescription";
        const string PREVIEW_LABEL = "previewLabel";

        internal static void Enact()
        {
            //a preview property that displays before recipe is started
            Machine.ClaimProperty<Recipe, string>(PREVIEW);
            Machine.ClaimProperty<Recipe, string>(PREVIEW_LABEL);
            Machine.Patch(
                original: typeof(RecipeNote).GetMethodInvariant(nameof(RecipeNote.StartDescription)),
                postfix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(DisplayPreview)));

            Machine.Patch(
                original: typeof(UnstartedState).GetMethodInvariant(nameof(UnstartedState.Exit)),
                postfix: typeof(RecipeLinkMaster).GetMethodInvariant(nameof(DisplayStartDescription)));
        }

        static bool dontDisplayPreview = false;
        private static readonly Action<RecipeNote, string> predictionTitleSet = typeof(RecipeNote).GetPropertyInvariant(nameof(RecipeNote.Title)).GetSetMethod(true).
            CreateAction<RecipeNote, string>();
        private static readonly Action<RecipeNote, string> predictionDescriptionSet = typeof(RecipeNote).GetPropertyInvariant(nameof(RecipeNote.Description)).GetSetMethod(true).
            CreateAction<RecipeNote, string>();
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