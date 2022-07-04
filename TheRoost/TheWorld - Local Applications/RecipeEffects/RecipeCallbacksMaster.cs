﻿using System;
using System.Collections;
using System.Collections.Generic;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.States;

namespace Roost.World
{
    /**
     * What this does/patches:
     * - adds the "callbacks" custom property to Recipe instances
     * - adds the new "useCallback" custom property to LinkedRecipeDetails instances
     * - patches (prefix) RecipeConductor.GetLinkedRecipe to check for the presence of useCallback and replace them on the fly
     * - patches (prefix) some method to set the callbacks in the levers map
     * - patches (postfix) RecipeConductor.GetLinkedRecipe to check if we need to clear the callbacks (no next recipe selected)
     * - patches to store the original recipe linked array, and one to put it back
     * - adds the clearCallbacks custom property to recipes so they can clear callbacks before routing
     */
    class RecipeCallbacksMaster
    {
        static Situation currentSituation = null;

        const string ADD_CALLBACKS = "addCallbacks";
        const string CLEAR_CALLBACKS = "clearcallbacks";
        const string RESET_CALLBACKS = "resetcallbacks";
        const string USE_CALLBACK = "useCallback";
        internal static void Enact()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            Machine.ClaimProperty<Recipe, Dictionary<string, string>>(ADD_CALLBACKS);
            Machine.ClaimProperty<Recipe, List<string>>(CLEAR_CALLBACKS);
            Machine.ClaimProperty<Recipe, bool>(RESET_CALLBACKS, defaultValue: false);
            Machine.ClaimProperty<LinkedRecipeDetails, string>(USE_CALLBACK);

            // Patch: store current situation
            Machine.Patch(
                original: typeof(RequiresExecutionState).GetMethodInvariant(nameof(RequiresExecutionState.Continue)),
                prefix: typeof(RecipeCallbacksMaster).GetMethodInvariant(nameof(StoreCurrentSituation)));

            // Patch: evaluate alts and set their Ids to callback values (if set)
            Machine.Patch(
                original: typeof(RecipeConductor).GetMethodInvariant(nameof(RecipeConductor.GetAlternateRecipes)),
                prefix: typeof(RecipeCallbacksMaster).GetMethodInvariant(nameof(EvaluateCallbacksForAlts)));

            // Patch: evaluate links and set their Ids to callback values (if set)
            Machine.Patch(
                original: typeof(RecipeConductor).GetMethodInvariant(nameof(RecipeConductor.GetLinkedRecipe)),
                prefix: typeof(RecipeCallbacksMaster).GetMethodInvariant(nameof(EvaluateCallbacksForLinks)));

            // Patch: clear callbacks when recipe chain ends
            Machine.Patch(
                original: typeof(CompleteState).GetMethodInvariant(nameof(CompleteState.Enter)),
                postfix: typeof(RecipeCallbacksMaster).GetMethodInvariant(nameof(ClearAllCallbacksForSituation)));

            // Patch: store callback ids
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(RecipeCallbackOperations, PatchType.Postfix);
        }

        public static string CompleteCallbackId(Situation situation, string callback)
        {
            return situation.Id + ".callbacks." + callback.ToLower();
        }

        private static void StoreCurrentSituation(Situation situation)
        {
            currentSituation = situation;
        }

        private static void EvaluateCallbacks(List<LinkedRecipeDetails> links)
        {
            foreach (LinkedRecipeDetails linkDetails in links)
            {
                string callbackId = linkDetails.RetrieveProperty<string>(USE_CALLBACK);

                if (callbackId == null)
                {
                    continue;
                }

                var callbackRecipeId = Machine.GetLeverForCurrentPlaythrough(CompleteCallbackId(currentSituation, callbackId));
                if (callbackRecipeId == null)
                    Birdsong.Tweet(VerbosityLevel.Essential, 0,$"Trying to use the callback '{callbackId}' in '{currentSituation.RecipeId}', but the callback is not set");

                //if the recipe id is wrong - or null, in case callback isn't set - default logger will display a message
                linkDetails.SetId(callbackRecipeId);
            }
        }

        //RecipeConductor.GetLinkedRecipe() prefix
        private static void EvaluateCallbacksForLinks(Recipe currentRecipe)
        {
            EvaluateCallbacks(currentRecipe.Linked);
        }

        //RecipeConductor.GetAlternateRecipes() prefix
        private static void EvaluateCallbacksForAlts(Recipe recipe)
        {
            EvaluateCallbacks(recipe.Alt);
        }

        public static void ClearAllCallbacksForSituation(Situation situation)
        {
            var storedValues = Machine.GetLeversForCurrentPlaythrough();
            string situationCallbacks = situation.Id + ".callbacks.";

            foreach (KeyValuePair<string, string> lever in storedValues)
            {
                if (lever.Key.StartsWith(situationCallbacks))
                    Machine.RemoveLeverForCurrentPlaythrough(lever.Key);
            }
        }

        private static void RecipeCallbackOperations(Situation situation)
        {
            var callbacksToSet = situation.Recipe.RetrieveProperty<Dictionary<string, string>>(ADD_CALLBACKS);
            if (callbacksToSet != null)
            {
                foreach (KeyValuePair<string, string> callback in callbacksToSet)
                {
                    //Birdsong.Sing("Set new callback:", CompleteCallbackId(situation, callback.Key), callback.Value);
                    Machine.SetLeverForCurrentPlaythrough(CompleteCallbackId(situation, callback.Key), callback.Value);
                }
            }

            var callbacksToClear = situation.Recipe.RetrieveProperty<List<string>>(CLEAR_CALLBACKS);
            if (callbacksToClear != null)
            {
                foreach (string callbackId in callbacksToClear)
                {
                    //Birdsong.Sing("Clearing callback", situation.Id, callbackId, CompleteCallbackId(situation, callbackId));
                    Machine.RemoveLeverForCurrentPlaythrough(CompleteCallbackId(situation, callbackId));
                }
            }

            if (situation.Recipe.RetrieveProperty<bool>(RESET_CALLBACKS))
                ClearAllCallbacksForSituation(situation);
        }
    }
}