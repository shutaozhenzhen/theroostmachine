using System;
using System.Collections.Generic;

using System.Reflection.Emit;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Logic;
using SecretHistories.Services;

using Roost.Twins;
using Roost.Twins.Entities;
using Roost.World.Recipes.Entities;

using HarmonyLib;

namespace Roost.World.Recipes
{
    public static class RecipeEffectsMaster
    {
        const string GRAND_EFFECTS = "grandeffects";

        internal static void Enact()
        {
            //reasonably should be somewhere else
            AtTimeOfPower.TabletopSceneInit.Schedule(ResetCrossroadsOnTabletopEnter, PatchType.Prefix);

            //grand effects
            Machine.ClaimProperty<Recipe, GrandEffects>(GRAND_EFFECTS);
            Dictionary<string, Type> allRecipeEffectsProperties = new Dictionary<string, Type>();
            foreach (CachedFucineProperty<GrandEffects> cachedProperty in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
                allRecipeEffectsProperties.Add(cachedProperty.LowerCaseName, cachedProperty.ThisPropInfo.PropertyType);
            //we don't want the first set of effects to have any target
            allRecipeEffectsProperties.Remove("target");
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
        }

        private static void ResetCrossroadsOnTabletopEnter()
        {
            Crossroads.ResetCache();
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

                FucineExp<int> draws = recipe.InternalDeck.RetrieveProperty<FucineExp<int>>("draws");
                if (recipe.HasCustomProperty("deckeffects") == false)
                    recipe.SetCustomProperty("deckeffects", new Dictionary<string, FucineExp<int>>());
                recipe.RetrieveProperty<Dictionary<string, FucineExp<int>>>("deckeffects").Add(recipe.InternalDeck.Id, draws);

                recipe.InternalDeck = new DeckSpec();
            }

            GrandEffects firstPassEffects = new GrandEffects(log);
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
            ///transpiler is very simple this time - we just wait until the native code does the actual object creation
            ///after it's done, we call InvokeGenericImporterForAbstractRootEntity() to modify the object as we please
            ///all other native transmutations are skipped
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(RecipeEffectsMaster.RefEffects))),
            };

            Vagabond.CodeInstructionMask mask = instruction => instruction.operand as System.Reflection.MethodInfo == typeof(RecipeCompletionEffectCommand).GetMethodInvariant("RunVerbManipulations");
            return instructions.ReplaceBeforeMask(mask, myCode, true);
        }

        private static void RefEffects(Situation situation)
        {
            //Birdsong.Sing(VerbosityLevel.SystemChatter, 0, $"EXECUTING: {command.Recipe.Id}");
            Crossroads.MarkLocalSituation(situation);

            GrandEffects grandEffects = situation.CurrentRecipe.RetrieveProperty<GrandEffects>(GRAND_EFFECTS);
            if (grandEffects == null)
                GrandEffects.RunElementTriggersOnly(situation, situation.GetSingleSphereByCategory(SphereCategory.SituationStorage));
            else
                grandEffects.RunGrandEffects(situation, situation.GetSingleSphereByCategory(SphereCategory.SituationStorage), true);

            Crossroads.ResetCache();
        }


        private static bool OverrideRecipeRefinement(string stringToRefine, AspectsDictionary ____aspectsInContext, ref string __result)
        {
            __result = Elegiast.Scribe.RefineString(stringToRefine, ____aspectsInContext);
            return false;
        }
    }
}