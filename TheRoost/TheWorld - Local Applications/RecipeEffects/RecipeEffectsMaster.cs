using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

using System.Reflection.Emit;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

using Roost.Twins;
using Roost.Twins.Entities;
using Roost.World.Recipes.Entities;

using HarmonyLib;

namespace Roost.World.Recipes
{
    public static class RecipeEffectsMaster
    {
        const string REF_REQS = "grandreqs";
        const string GRAND_EFFECTS = "grandeffects";

        internal static void Enact()
        {
            Machine.ClaimProperty<Element, Dictionary<string, List<RefMorphDetails>>>("xtriggers");
            //DeckSpec.Draws is only used for recipe internal decks; to allow them to use expressions, this
            Machine.ClaimProperty<DeckSpec, Funcine<int>>("draws", false, "1");

            Machine.ClaimProperty<Recipe, Dictionary<Funcine<int>, Funcine<int>>>(REF_REQS);
            Machine.ClaimProperty<Recipe, GrandEffects>(GRAND_EFFECTS);

            Dictionary<string, Type> allRecipeEffectsProperties = new Dictionary<string, Type>();
            foreach (CachedFucineProperty<GrandEffects> cachedProperty in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
                allRecipeEffectsProperties.Add(cachedProperty.LowerCaseName, cachedProperty.ThisPropInfo.PropertyType);
            Machine.ClaimProperties<Recipe>(allRecipeEffectsProperties);

            AtTimeOfPower.OnPostImportRecipe.Schedule<Recipe, Compendium>(WrapAndFlushFirstPassEffects, PatchType.Prefix);

            AtTimeOfPower.RecipeRequirementsCheck.Schedule<Recipe, AspectsInContext>(RefReqs);

            Machine.Patch(
                original: typeof(RecipeCompletionEffectCommand).GetMethodInvariant(nameof(RecipeCompletionEffectCommand.Execute)),
                transpiler: typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(RunRefEffectsTranspiler)));

            Machine.Patch(
                 original: typeof(Beachcomber.Usurper).GetMethodInvariant("InvokeGenericImporterForAbstractRootEntity"),
                 prefix: typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(ConvertLegacyMutationDefinitions)));
        }

        //Recipe.OnPostImportForSpecificEntity()
        private static void WrapAndFlushFirstPassEffects(Recipe __instance, Compendium populatedCompendium)
        {
            //internal deck is added to deckeffects manually; we need to do the same
            Recipe recipe = __instance;
            if (recipe.InternalDeck.Spec.Count > 0 || string.IsNullOrWhiteSpace(recipe.InternalDeck.DefaultCard)==false)
            {
                recipe.InternalDeck.SetId("deck." + recipe.Id);

                populatedCompendium.TryAddEntity(recipe.InternalDeck);

                Funcine<int> draws = recipe.InternalDeck.RetrieveProperty<Funcine<int>>("draws");
                if (recipe.HasCustomProperty("deckeffects") == false)
                    recipe.SetProperty("deckeffects", new Dictionary<string, Funcine<int>>());
                recipe.RetrieveProperty<Dictionary<string, Funcine<int>>>("deckeffects").Add(recipe.InternalDeck.Id, draws);


                recipe.InternalDeck = new DeckSpec();
            }

            GrandEffects firstPassEffects = new GrandEffects();
            bool atLeastOneEffect = false;
            foreach (CachedFucineProperty<GrandEffects> cachedProperty in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
                if (recipe.HasCustomProperty(cachedProperty.LowerCaseName))
                {
                    atLeastOneEffect = true;
                    cachedProperty.SetViaFastInvoke(firstPassEffects, recipe.RetrieveProperty(cachedProperty.LowerCaseName));
                }


            if (atLeastOneEffect)
            {
                recipe.SetProperty(GRAND_EFFECTS, firstPassEffects);

                //to keep the correct (well, somewhat) deck preview, we reassign deck effects to the main recipe
                if (firstPassEffects.DeckEffects != null)
                    foreach (string deckId in firstPassEffects.DeckEffects.Keys)
                        recipe.DeckEffects.Add(deckId, 1);
            }
        }

        //Usurper.InvokeGenericImporterForAbstractRootEntity()
        private static void ConvertLegacyMutationDefinitions(IEntityWithId entity, EntityData entityData, ContentImportLog log)
        {
            try
            {
                if (entity is Recipe)
                    if (entityData.ValuesTable.ContainsKey("mutations") && entityData.ValuesTable["mutations"] is ArrayList)
                    {
                        ArrayList oldMutations = entityData.ValuesTable["mutations"] as ArrayList;
                        EntityData newMutations = new EntityData();

                        foreach (EntityData data in oldMutations)
                        {
                            string key = data.ValuesTable["filter"].ToString();
                            if (newMutations.ValuesTable.ContainsKey(key) == false)
                                newMutations.ValuesTable[key] = new ArrayList();

                            (newMutations.ValuesTable[key] as ArrayList).Add(data);
                            data.ValuesTable.Remove("filter");
                        }

                        entityData.ValuesTable["mutations"] = newMutations;
                    }
            }
            catch (Exception ex)
            {
                log.LogProblem($"Failed to convert legacy mutation:\n{ex.FormatException()}");
            }
        }

        private static IEnumerable<CodeInstruction> RunRefEffectsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            ///transpiler is very simple this time - we just wait until the native code does the actual object creation
            ///after it's done, we call InvokeGenericImporterForAbstractRootEntity() to modify the object as we please
            ///all other native transmutations are skipped
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, typeof(RecipeEffectsMaster).GetMethodInvariant("RefEffects")),
            };

            Vagabond.CodeInstructionMask mask = instruction => instruction.operand as System.Reflection.MethodInfo == typeof(RecipeCompletionEffectCommand).GetMethodInvariant("RunRecipeEffects");
            return instructions.ReplaceBeforeMask(mask, myCode, false);
        }

        private static void RefEffects(RecipeCompletionEffectCommand command, Situation situation)
        {
            Birdsong.Sing(VerbosityLevel.SystemChatter, 0, $"EXECUTING: {command.Recipe.Id}");

            situation.Recipe = command.Recipe;
            GrandEffects recipeEffects = situation.Recipe.RetrieveProperty<GrandEffects>(GRAND_EFFECTS);
            if (recipeEffects != null)
            {
                recipeEffects.Run(situation, situation.GetSingleSphereByCategory(SphereCategory.SituationStorage));
                TokenContextAccessors.ResetCache();
            }
        }

        private static bool RefReqs(Recipe __instance, AspectsInContext aspectsinContext)
        {
            Dictionary<Funcine<int>, Funcine<int>> reqs = __instance.RetrieveProperty(REF_REQS) as Dictionary<Funcine<int>, Funcine<int>>;
            if (reqs == null)
                return true;

            //what I am about to do here should be illegal (and will be at some point of time in the bright future of humankind)
            //but I really need to know a *situation* instead of just aspects; and there is no easier way to go about it
            bool situationFound = false;
            foreach (Situation situation in Watchman.Get<HornedAxe>().GetRegisteredSituations())
                if (situation.GetAspects(true).AspectsEqual(aspectsinContext.AspectsInSituation))
                {
                    TokenContextAccessors.SetLocalSituation(situation);
                    situationFound = true;
                    break;
                }
            if (!situationFound)
                throw Birdsong.Cack("Something strange happened. Cannot identify the current situation for requirements check.");

            //Birdsong.Sing("Checking _reqs for {0}", __instance.Id);
            foreach (KeyValuePair<Funcine<int>, Funcine<int>> req in reqs)
            {
                int presentValue = req.Key.value;
                int requiredValue = req.Value.value;

                //Birdsong.Sing("'{0}': '{1}' ---> '{2}': '{3}', {4}", req.Key.formula, req.Value.formula, presentValue, requiredValue, (requiredValue <= -1 && presentValue >= -requiredValue) || (requiredValue > -1 && presentValue < requiredValue) ? "not satisfied" : "satisfied");

                if (requiredValue <= -1)
                {
                    if (presentValue >= -requiredValue)
                        return false;
                }
                else
                {
                    if (presentValue < requiredValue)
                        return false;
                }
            }

            return true;
        }

        private static bool AspectsEqual(this AspectsDictionary dictionary1, AspectsDictionary dictionary2)
        {
            if (dictionary1 == dictionary2) return true;
            if ((dictionary1 == null) || (dictionary2 == null)) return false;
            if (dictionary1.Count != dictionary2.Count) return false;

            foreach (string key in dictionary2.Keys)
                if (dictionary1.ContainsKey(key) == false || dictionary1[key] != dictionary2[key])
                    return false;

            return true;
        }
    }
}