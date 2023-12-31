﻿using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Collections.Generic;
using SecretHistories.UI;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.States;

using HarmonyLib;

namespace Roost.World.Recipes
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
    static class RecipeCallbacksMaster
    {
        const string ADD_CALLBACKS = "addCallbacks";
        const string CLEAR_CALLBACKS = "clearcallbacks";
        const string RESET_CALLBACKS = "resetcallbacks";
        const string USE_CALLBACK = "useCallback";

        static Func<object, object> getCachedRecipesList = typeof(LinkedRecipeDetails).GetFieldInvariant("_possibleMatchesRecipes").GetValue;

        internal static void Enact()
        {
            Machine.ClaimProperty<Recipe, Dictionary<string, string>>(ADD_CALLBACKS);
            Machine.ClaimProperty<Recipe, List<string>>(CLEAR_CALLBACKS);
            Machine.ClaimProperty<Recipe, bool>(RESET_CALLBACKS, defaultValue: false);
            Machine.ClaimProperty<LinkedRecipeDetails, string>(USE_CALLBACK);

            Machine.Patch(
                original: typeof(LinkedRecipeDetails).GetMethodInvariant(nameof(LinkedRecipeDetails.GetRecipeWhichCanExecuteInContext)),
                transpiler: typeof(RecipeCallbacksMaster).GetMethodInvariant(nameof(UpdateMatchingRecipes)));

            // Patch: clear callbacks when recipe chain ends
            Machine.Patch(
                original: typeof(CompleteState).GetMethodInvariant(nameof(CompleteState.Enter)),
                postfix: typeof(RecipeCallbacksMaster).GetMethodInvariant(nameof(ClearAllCallbacksForSituation)));

            Machine.Patch(
                original: Machine.GetMethod<LinkedRecipeDetails>("OnPostImportForSpecificEntity"),
                prefix: typeof(RecipeCallbacksMaster).GetMethodInvariant(nameof(SetDummyId)));

            // Patch: store callback ids
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(RecipeCallbackOperations, PatchType.Postfix);
        }

        private static void SetDummyId(LinkedRecipeDetails __instance, Compendium populatedCompendium)
        {
            if (__instance.RetrieveProperty<string>(USE_CALLBACK) != null)
            {
                Recipe anyRecipe = populatedCompendium.GetEntitiesAsList<Recipe>()[0];
                //we're setting literally any id to it so it won't fail a validation
                //the real links are cleared each time we evaluate the callback-link
                __instance.SetId(anyRecipe.Id);
            }
        }

        //LinkedRecipeDetails.GetRecipeWhichCanExecuteInContext()
        private static IEnumerable<CodeInstruction> UpdateMatchingRecipes(IEnumerable<CodeInstruction> instructions)
        {
            //every time link is supposed to return a recipe, we evaluate callbacks and assign valid recipes based on that
            //doing that after the chance is already evaluated to avoid overhead - and just before _possibleMatchesRecipes are accessed
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Call, typeof(RecipeCallbacksMaster).GetMethodInvariant(nameof(EvaluateCallbacks))),
                new CodeInstruction(OpCodes.Ldarg_0),
            };

#pragma warning disable
            Vagabond.CodeInstructionMask mask = instruction => instruction.operand == typeof(LinkedRecipeDetails).GetFieldInvariant("_possibleMatchesRecipes");
#pragma warning restore 
            return instructions.InsertBefore(mask, myCode, 0);
        }

        private static void EvaluateCallbacks(LinkedRecipeDetails linkDetails)
        {
            string callbackId = linkDetails.RetrieveProperty<string>(USE_CALLBACK);

            if (callbackId == null)
                return;

            var fullCallbackId = CompleteCallbackId(RavensEye.currentSituation, callbackId);
            var callbackRecipeId = Machine.GetLeverForCurrentPlaythrough(fullCallbackId);
            if (callbackRecipeId == null)
            {
                Birdsong.TweetLoud($"Trying to use the callback '{callbackId}' in '{RavensEye.currentSituation.RecipeId}', but the callback is not set");

                List<Recipe> cachedRecipes = getCachedRecipesList(linkDetails) as List<Recipe>;
                cachedRecipes.Clear();

                return;
            }

            //if the recipe id is wrong - or null, in case callback isn't set - default logger will display a message

            if (linkDetails.Id != callbackRecipeId)
            {
                linkDetails.SetId(callbackRecipeId);
                List<Recipe> cachedRecipes = getCachedRecipesList(linkDetails) as List<Recipe>;
                cachedRecipes.Clear();
                cachedRecipes.AddRange(Watchman.Get<Compendium>().GetEntitiesAsList<Recipe>().Where(r => r.WildcardMatchId(callbackRecipeId)));

                if (cachedRecipes.Count == 0)
                    Birdsong.TweetLoud($"No matching recipes for callback id '{callbackId}'");
            }
        }


        public static string CompleteCallbackId(Situation situation, string callback)
        {
            return situation.Id + ".callbacks." + callback.ToLower();
        }



        public static void ClearAllCallbacksForSituation(Situation situation)
        {
            var storedValues = Machine.GetLeversForCurrentPlaythrough();
            string situationCallbacks = CompleteCallbackId(situation, string.Empty);

            foreach (KeyValuePair<string, string> lever in storedValues)
            {
                if (lever.Key.StartsWith(situationCallbacks, StringComparison.InvariantCultureIgnoreCase))
                    Machine.RemoveLeverForCurrentPlaythrough(lever.Key);
            }
        }

        private static void RecipeCallbackOperations(Situation situation)
        {
            var callbacksToSet = situation.CurrentRecipe.RetrieveProperty<Dictionary<string, string>>(ADD_CALLBACKS);
            if (callbacksToSet != null)
            {
                foreach (KeyValuePair<string, string> callback in callbacksToSet)
                {
                    string callbackFullId = CompleteCallbackId(situation, callback.Key);
                    Machine.SetLeverForCurrentPlaythrough(callbackFullId, callback.Value);
                    //Birdsong.Sing("Set new callback:", CompleteCallbackId(situation, callback.Key), callback.Value);
                }
            }

            var callbacksToClear = situation.CurrentRecipe.RetrieveProperty<List<string>>(CLEAR_CALLBACKS);
            if (callbacksToClear != null)
            {
                foreach (string callbackId in callbacksToClear)
                {
                    string callbackFullId = CompleteCallbackId(situation, callbackId);
                    Machine.RemoveLeverForCurrentPlaythrough(callbackFullId);
                    //Birdsong.Sing("Clearing callback", situation.Id, callbackId, CompleteCallbackId(situation, callbackId));
                }
            }

            if (situation.CurrentRecipe.RetrieveProperty<bool>(RESET_CALLBACKS))
                ClearAllCallbacksForSituation(situation);
        }
    }
}
