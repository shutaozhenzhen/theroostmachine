using System;
using System.Collections.Generic;

using System.Reflection.Emit;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Services;
using SecretHistories.States;
using SecretHistories.Commands;

using Roost.Twins;
using Roost.Twins.Entities;
using Roost.World.Recipes.Entities;

using HarmonyLib;

namespace Roost.World.Recipes
{
    public static class RecipeEffectsMaster
    {
        public const string GRAND_EFFECTS = nameof(GrandEffects);

        internal static void Enact()
        {
            //grand effects
            Machine.ClaimProperty<Recipe, GrandEffects>(GRAND_EFFECTS);
            Dictionary<string, Type> allRecipeEffectsProperties = new Dictionary<string, Type>();
            foreach (CachedFucineProperty<GrandEffects> cachedProperty in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
                allRecipeEffectsProperties.Add(cachedProperty.LowerCaseName, cachedProperty.ThisPropInfo.PropertyType);
            Machine.ClaimProperties<Recipe>(allRecipeEffectsProperties);

            AtTimeOfPower.OnPostImportRecipe.Schedule<Recipe, ContentImportLog, Compendium>(WrapAndFlushFirstPassEffects, PatchType.Prefix);

            Machine.Patch(
                original: typeof(RecipeCompletionEffectCommand).GetMethodInvariant(nameof(RecipeCompletionEffectCommand.Execute), typeof(Situation)),
                transpiler: typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(RunRefEffectsTranspiler)));

            Machine.Patch(
                original: typeof(Token).GetMethodInvariant(nameof(Token.CalveToken)),
                postfix: typeof(RecipeExecutionBuffer).GetMethodInvariant(nameof(RecipeExecutionBuffer.OnTokenCalved)));


            Legerdemain.Enact();

            RefMorphDetails.Enact();
            RefMutationEffect.Enact();
            TokenFilterSpec.Enact();

            Machine.Patch(
                original: typeof(TextRefiner).GetMethodInvariant(nameof(TextRefiner.RefineString)),
                prefix: typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(OverrideRecipeRefinement)));

            Machine.Patch(
                original: Machine.GetMethod<OngoingState>(nameof(OngoingState.UpdateRecipePrediction)),
                prefix: typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(MidRecipeVisualUpdate)));

            Machine.Patch(
                original: Machine.GetMethod<SituationWindow>(nameof(SituationWindow.Attach), typeof(Situation)),
                prefix: typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(RememberWindowForSituation)));

            Machine.Patch(
                original: Machine.GetMethod<Situation>(nameof(Situation.Retire)),
                prefix: typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(ForgetWindowForSituatuin)));
        }

        static Action<Situation> TryOverrideVerbIcon = typeof(Situation).GetMethodInvariant(nameof(TryOverrideVerbIcon)).CreateAction<Situation>();
        static Action<Token> UpdateVisuals = typeof(Token).GetMethodInvariant(nameof(UpdateVisuals)).CreateAction<Token>();
        public static Dictionary<Situation, SituationWindow> situationsWindows = new Dictionary<Situation, SituationWindow>();
        private static void MidRecipeVisualUpdate(Situation situation)
        {
            TryOverrideVerbIcon(situation);
            UpdateVisuals(situation.GetToken());
            if (situationsWindows.ContainsKey(situation))
                situationsWindows[situation].DisplayIcon(situation.Icon);
        }

        private static void RememberWindowForSituation(Situation newSituation, SituationWindow __instance)
        {
            situationsWindows.Add(newSituation, __instance);
        }
        private static void ForgetWindowForSituatuin(Situation __instance)
        {
            situationsWindows.Remove(__instance);
        }

        //Recipe.OnPostImportForSpecificEntity()
        private static void WrapAndFlushFirstPassEffects(Recipe __instance, ContentImportLog log, Compendium populatedCompendium)
        {
            //internal deck is added to deckeffects manually; we need to do the same
            Recipe recipe = __instance;
            if (recipe.InternalDeck.Spec.Count > 0 || string.IsNullOrWhiteSpace(recipe.InternalDeck.DefaultCard) == false)
            {
                recipe.InternalDeck.SetId("deck." + recipe.Id);

                populatedCompendium.TryAddEntity(recipe.InternalDeck);

                FucineExp<int> draws = recipe.InternalDeck.RetrieveProperty<FucineExp<int>>(nameof(DeckSpec.Draws));
                if (recipe.HasCustomProperty(nameof(Recipe.DeckEffects)) == false)
                    recipe.SetCustomProperty(nameof(Recipe.DeckEffects), new Dictionary<string, FucineExp<int>>());
                recipe.RetrieveProperty<Dictionary<string, FucineExp<int>>>(nameof(Recipe.DeckEffects)).Add(recipe.InternalDeck.Id, draws);

                recipe.InternalDeck = new DeckSpec();
            }

            GrandEffects firstPassEffects = new GrandEffects();
            firstPassEffects.SetDefaultValues();
            firstPassEffects.Target = null;

            bool atLeastOneEffect = false;
            foreach (CachedFucineProperty<GrandEffects> cachedProperty in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
                if (recipe.HasCustomProperty(cachedProperty.LowerCaseName))
                {
                    atLeastOneEffect = true;
                    cachedProperty.SetViaFastInvoke(firstPassEffects, recipe.RetrieveProperty(cachedProperty.LowerCaseName));
                    recipe.RemoveProperty(cachedProperty.LowerCaseName);
                }

            if (atLeastOneEffect)
            {
                firstPassEffects.SetId(recipe.Id);
                firstPassEffects.SetContainer(recipe);
                firstPassEffects.OnPostImport(log, populatedCompendium);
                recipe.SetCustomProperty(GRAND_EFFECTS, firstPassEffects);

                //to keep the deck preview correct (well, somewhat), we reassign deck effects to the main recipe
                if (firstPassEffects.DeckEffects != null)
                    foreach (string deckId in firstPassEffects.DeckEffects.Keys)
                        recipe.DeckEffects.Add(deckId, "1");

                //to keep the inductions from recipe aspects correct, we reassign aspects to the main
                if (firstPassEffects.Aspects != null)
                    foreach (string aspectId in firstPassEffects.Aspects.Keys)
                        recipe.Aspects.Add(aspectId, 1);
            }
        }

        private static IEnumerable<CodeInstruction> RunRefEffectsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(RefEffects))),
            };

            Vagabond.CodeInstructionMask mask = instruction => instruction.operand as System.Reflection.MethodInfo == typeof(RecipeCompletionEffectCommand).GetMethodInvariant("RunVerbManipulations");
            return instructions.ReplaceBeforeMask(mask, myCode, true);
        }

        private static void RefEffects(Situation situation)
        {
            Crossroads.ResetCache();
            Crossroads.MarkLocalSituation(situation);
            GrandReqsMaster.situationIsFresh = false;

            GrandEffects grandEffects = situation.CurrentRecipe.RetrieveProperty<GrandEffects>(GRAND_EFFECTS);
            if (grandEffects == null)
                GrandEffects.RunElementTriggersOnly(situation);
            else
                grandEffects.StartGrandEffects(situation);

            GrandReqsMaster.situationIsFresh = true;
        }

        private static bool OverrideRecipeRefinement(string stringToRefine, AspectsDictionary ____aspectsInContext, ref string __result)
        {
            __result = Elegiast.Scribe.RefineString(stringToRefine, ____aspectsInContext);
            return false;
        }
    }
}