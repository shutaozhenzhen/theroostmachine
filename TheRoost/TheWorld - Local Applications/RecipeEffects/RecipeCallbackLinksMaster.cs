using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.States;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Roost.World
{
    /**
     * What this does/patches:
     * - adds the "callbacks" custom property to LinkedRecipeDetails instances
     * - adds the new "useCallback" custom property to LinkedRecipeDetails instances
     * - patches (prefix) RecipeConductor.GetLinkedRecipe to check for the presence of useCallback and replace them on the fly
     * - patches (prefix) some method to set the callbacks in the Situation Illumination map
     * - patches some method to clear the callbacks in the Situation Illumination map
     * - patches to store the original recipe linked array, and one to put it back
     * Bonus for later: - adds the clearCallbacks custom property to recipes so they can clear callbacks before routing
     */
    class RecipeCallbackLinksMaster
    {
        static Situation currentSituation = null;
        static List<LinkedRecipeDetails> originalLinkedList = null;

        public static void Enact()
        {
            Machine.ClaimProperty<Recipe, Dictionary<string, string>>("callbacks");
            Machine.ClaimProperty<Recipe, List<string>>("clearCallbacks");
            Machine.ClaimProperty<LinkedRecipeDetails, string>("useCallback");
            

            // Patch: store current situation
            Machine.Patch(
                original: typeof(RequiresExecutionState).GetMethodInvariant(nameof(RequiresExecutionState.Continue)),
                prefix: typeof(RecipeCallbackLinksMaster).GetMethodInvariant(nameof(StoreCurrentSituation)));

            // Patch: store the original linked list and set as argument the newly parsed one
            Machine.Patch(
                original: typeof(RecipeConductor).GetMethodInvariant(nameof(RecipeConductor.GetLinkedRecipe)),
                prefix: typeof(RecipeCallbackLinksMaster).GetMethodInvariant(nameof(EvaluateCallbackLinks)));

            // Patch: put the original recipe back
            Machine.Patch(
                original: typeof(RecipeConductor).GetMethodInvariant(nameof(RecipeConductor.GetLinkedRecipe)),
                postfix: typeof(RecipeCallbackLinksMaster).GetMethodInvariant(nameof(PutTheOriginalLinkedListBack)));

            // Patch: clear callbacks if we stop there (no valid next recipe found)
            Machine.Patch(
                original: typeof(RecipeConductor).GetMethodInvariant(nameof(RecipeConductor.GetLinkedRecipe)),
                postfix: typeof(RecipeCallbackLinksMaster).GetMethodInvariant(nameof(CheckForChainEnd)));

            // Patch: store callback ids
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(CheckForCallbacksToStore, PatchType.Postfix);

            // Patch: clean callback ids property
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(CheckForCallbacksToClear, PatchType.Postfix);
        }

        public static string CompleteCallbackId(Situation situation, string callback)
        {
            return situation.Id + ".callbacks." + callback.ToLower();
        }

        public static string CompleteCallbackId(string callback)
        {
            return CompleteCallbackId(currentSituation, callback);
        }

        public static void StoreCurrentSituation(Situation situation)
        {
            currentSituation = situation;
        }

        //RecipeConductor.GetLinkedRecipe() prefix
        private static void EvaluateCallbackLinks(ref Recipe currentRecipe)
        {
            originalLinkedList = currentRecipe.Linked;
            List<LinkedRecipeDetails> parsedList = new List<LinkedRecipeDetails>();
            foreach (LinkedRecipeDetails recipeDetail in originalLinkedList) {
                string callbackId = recipeDetail.RetrieveProperty<string>("useCallback");
                if (callbackId == null)
                {
                    parsedList.Add(recipeDetail);
                    continue;
                }
                
                var callbackRecipeId = Machine.GetLeverForCurrentPlaythrough(CompleteCallbackId(callbackId));
                if(callbackRecipeId == null)
                {
                    continue;
                }

                Recipe actualRecipe = Machine.GetEntity<Recipe>(callbackRecipeId);
                
                if(actualRecipe == null)
                {
                    continue;
                }
                
                var lr = LinkedRecipeDetails.AsCurrentRecipe(actualRecipe);
                lr.Chance = recipeDetail.Chance;
                lr.Challenges = recipeDetail.Challenges;
                
                parsedList.Add(lr);
            }
            currentRecipe.Linked = parsedList;
        }

        public static void PutTheOriginalLinkedListBack(ref Recipe currentRecipe)
        {
            currentRecipe.Linked = originalLinkedList;
        }

        public static void CheckForChainEnd(ref Recipe __result)
        {
            if(__result == null)
            {
                var storedValues = Machine.GetLeversForCurrentPlaythrough();
                foreach (KeyValuePair<string, string> pair in storedValues)
                {
                    if (pair.Key.StartsWith(currentSituation.Id+".callbacks.")) Machine.ClearLeverForCurrentPlaythrough(pair.Key);
                }
            }
        }

        public static void CheckForCallbacksToStore(Situation situation)
        {
            Recipe recipe = situation.Recipe;
            var callbacksToSet = recipe.RetrieveProperty<Dictionary<string, string>>("callbacks");
            if (callbacksToSet == null) return;

            foreach(KeyValuePair<string, string> pair in callbacksToSet)
            {
                Birdsong.Sing("Set new callback:", CompleteCallbackId(situation, pair.Key), pair.Value);
                Machine.SetLeverForCurrentPlaythrough(CompleteCallbackId(situation, pair.Key), pair.Value);
            }
        }

        public static void CheckForCallbacksToClear(Situation situation)
        {
            Recipe recipe = situation.Recipe;
            var callbacksToClear = recipe.RetrieveProperty<List<string>>("clearCallbacks");
            if (callbacksToClear == null) return;
            foreach (string callbackId in callbacksToClear)
            {
                Birdsong.Sing("Clearing callback", situation.Id, callbackId, CompleteCallbackId(situation, callbackId));
                Machine.ClearLeverForCurrentPlaythrough(CompleteCallbackId(situation, callbackId));
            }
        }
    }
}
