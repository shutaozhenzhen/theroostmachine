using System;
using System.Collections.Generic;

using System.Reflection.Emit;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Spheres;

using Roost.Twins;
using Roost.Twins.Entities;
using Roost.World.Recipes.Entities;

using HarmonyLib;

namespace Roost.World.Recipes
{
    public static class RecipeEffectsMaster
    {
        const string GRAND_REQS = "grandreqs";
        const string GRAND_EFFECTS = "grandeffects";

        internal static void Enact()
        {
            Machine.ClaimProperty<Element, Dictionary<string, List<RefMorphDetails>>>("xtriggers");

            Machine.ClaimProperty<Recipe, Dictionary<FucineExp<int>, FucineExp<int>>>(GRAND_REQS);
            AtTimeOfPower.RecipeRequirementsCheck.Schedule<Recipe, AspectsInContext>(RefReqs);

            Machine.ClaimProperty<Recipe, GrandEffects>(GRAND_EFFECTS);
            Dictionary<string, Type> allRecipeEffectsProperties = new Dictionary<string, Type>();
            foreach (CachedFucineProperty<GrandEffects> cachedProperty in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
                allRecipeEffectsProperties.Add(cachedProperty.LowerCaseName, cachedProperty.ThisPropInfo.PropertyType);
            //we don't want the first set of effects to have any target
            allRecipeEffectsProperties.Remove("target");
            Machine.ClaimProperties<Recipe>(allRecipeEffectsProperties);

            Machine.AddImportMolding<Recipe>(MoldingsStorage.ConvertLegacyMutations);

            AtTimeOfPower.OnPostImportRecipe.Schedule<Recipe, ContentImportLog, Compendium>(WrapAndFlushFirstPassEffects, PatchType.Prefix);
            AtTimeOfPower.OnPostImportElement.Schedule<Element, ContentImportLog, Compendium>(PostImportForTheNewXtriggers, PatchType.Postfix);

            Machine.Patch(
                original: typeof(RecipeCompletionEffectCommand).GetMethodInvariant(nameof(RecipeCompletionEffectCommand.Execute), typeof(Situation)),
                transpiler: typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(RunRefEffectsTranspiler)));

            AtTimeOfPower.NewGame.Schedule(CatchNewGame, PatchType.Prefix);
            AtTimeOfPower.TabletopSceneInit.Schedule(TabletopEnter, PatchType.Postfix);

            Machine.Patch(
                original: typeof(SituationStorageSphere).GetPropertyInvariant("AllowStackMerge").GetGetMethod(),
                prefix: typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(AllowStackMerge)));


        }


        public static bool newGameStarted = false;
        private static void CatchNewGame()
        {
            newGameStarted = true;
        }
        private static void TabletopEnter()
        {
            Crossroads.defaultSphereContainer.Add(Watchman.Get<HornedAxe>().GetDefaultSphere(OccupiesSpaceAs.Intangible));
            Legerdemain.InitNewGame();
            newGameStarted = false;
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
                }

            if (atLeastOneEffect)
            {
                firstPassEffects.SetId(recipe.Id);
                firstPassEffects.OnPostImport(log, populatedCompendium);
                recipe.SetCustomProperty(GRAND_EFFECTS, firstPassEffects);

                //to keep the deck preview correct (well, somewhat), we reassign deck effects to the main recipe
                if (firstPassEffects.DeckEffects != null)
                    foreach (string deckId in firstPassEffects.DeckEffects.Keys)
                        recipe.DeckEffects.Add(deckId, 1);
                //to keep the inductions from recipe aspects correct, we reassign aspects to the main
                //(it's also used in TokenValueRef's ValueArea.Recipe)
                if (firstPassEffects.Aspects != null)
                    foreach (string aspectId in firstPassEffects.Aspects.Keys)
                        recipe.Aspects.Add(aspectId, 1);
            }
        }

        private static void PostImportForTheNewXtriggers(Element __instance, ContentImportLog log, Compendium populatedCompendium)
        {
            Dictionary<string, List<RefMorphDetails>> xtriggers = __instance.RetrieveProperty<Dictionary<string, List<RefMorphDetails>>>("xtriggers");
            if (xtriggers != null)
            {
                ContentImportLog subLog = new ContentImportLog();
                foreach (string catalyst in xtriggers.Keys)
                    foreach (RefMorphDetails morphDetails in xtriggers[catalyst])
                        morphDetails.OnPostImport(subLog, populatedCompendium);

                foreach (ILogMessage message in subLog.GetMessages())
                    Birdsong.Tweet(VerbosityLevel.Essential, message.MessageLevel, $"PROBLEM IN XTRIGGERS FOR ELEMENT '{__instance.Id}' - {message.Description}'");
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
                new CodeInstruction(OpCodes.Call, typeof(RecipeEffectsMaster).GetMethodInvariant(nameof(RecipeEffectsMaster.RefEffects))),

            };
            Vagabond.CodeInstructionMask mask = instruction => instruction.operand as System.Reflection.MethodInfo == typeof(RecipeCompletionEffectCommand).GetMethodInvariant("RunVerbManipulations");
            return instructions.ReplaceBeforeMask(mask, myCode, true);
        }

        private static void RefEffects(RecipeCompletionEffectCommand command, Situation situation)
        {
            //Birdsong.Sing(VerbosityLevel.SystemChatter, 0, $"EXECUTING: {command.Recipe.Id}");

            situation.Recipe = command.Recipe;
            Twins.Crossroads.MarkLocalSituation(situation);
            GrandEffects.RunGrandEffects(situation.Recipe.RetrieveProperty<GrandEffects>(GRAND_EFFECTS), situation, situation.GetSingleSphereByCategory(SphereCategory.SituationStorage));
            ManageDirtySpheres();
            Twins.Crossroads.ResetCache();
        }

        public static bool CheckGrandReqs(Dictionary<FucineExp<int>, FucineExp<int>> grandreqs)
        {
            //Birdsong.Sing($"Checking GrandReqs for {__instance.Id} in {situation.VerbId} verb");
            foreach (KeyValuePair<FucineExp<int>, FucineExp<int>> req in grandreqs)
            {
                int presentValue = req.Key.value;
                int requiredValue = req.Value.value;

                //Birdsong.Sing($"{req.Key.formula}: {req.Value.formula} --> {presentValue}: {requiredValue}");

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

        private static bool RefReqs(Recipe __instance, AspectsInContext aspectsinContext)
        {
            Dictionary<FucineExp<int>, FucineExp<int>> grandreqs = __instance.RetrieveProperty(GRAND_REQS) as Dictionary<FucineExp<int>, FucineExp<int>>;
            if (grandreqs == null || grandreqs.Count == 0)
                return true;

            //what I am about to do here should be illegal (and will be at some point of time in the bright future of humankind)
            //but I really need to know a *situation* instead of just aspects; and there is no easier way to go about it
            foreach (Situation situation in Watchman.Get<HornedAxe>().GetRegisteredSituations())
                if (situation.GetAspects(true).AspectsEqual(aspectsinContext.AspectsInSituation))
                {
                    Crossroads.MarkLocalSituation(situation);
                    bool result = CheckGrandReqs(grandreqs);
                    Crossroads.ResetCache();
                    return result;
                }
            throw Birdsong.Cack($"Something strange happened. Cannot identify a situation for requirements check in the recipe {__instance}.");
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

        public static GrandEffects GetGrandEffects(this Recipe recipe)
        {
            return recipe.RetrieveProperty<GrandEffects>(GRAND_EFFECTS);
        }

        private static void ManageDirtySpheres()
        {
            HashSet<Sphere> affectedSpheres = RecipeExecutionBuffer.FlushDirtySpheres();
            foreach (Sphere sphere in affectedSpheres)
                ManageDirtySphere(sphere);
        }

        //separating into its own method for possible future patching
        private static void ManageDirtySphere(Sphere sphere)
        {
            if (sphere.SphereCategory == SphereCategory.SituationStorage)
            {
                StackAllTokens(sphere);
                SituationWindowMaster.UpdateSituationWindowDisplay(sphere);
            }
        }

        private static void StackAllTokens(Sphere sphere)
        {
            List<Token> tokens = sphere.Tokens;

            for (int n = tokens.Count - 1; n >= 0; n--)
                for (int m = n - 1; m >= 0; m--)
                    if (tokens[n].CanMergeWithToken(tokens[m]) && tokens[n].Shrouded() == tokens[m].Shrouded())
                    {
                        tokens[n].Payload.SetQuantity(tokens[n].Quantity + tokens[m].Quantity, RecipeExecutionBuffer.situationEffectContext);
                        tokens[m].Retire();
                    }
        }

        //Allowing stack merge for SituationStorage
        private static bool AllowStackMerge(ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}