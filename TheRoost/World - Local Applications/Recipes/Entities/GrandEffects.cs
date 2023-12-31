﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Entities;
using SecretHistories.Core;
using SecretHistories.Spheres;
using SecretHistories.Infrastructure;
using SecretHistories;

using Roost.Twins;
using Roost.Twins.Entities;

namespace Roost.World.Recipes.Entities
{
    public class GrandEffects : AbstractEntity<GrandEffects>
    {
        [FucinePathValue(defaultValue: "~/sphere")] public FucinePath Target { get; set; }
        public Sphere GetTargetSpheres(Situation currentSituation) => Target == null
                ? currentSituation.GetSingleSphereByCategory(SphereCategory.SituationStorage)
                : Target.GetSpheresByPathAsSingleSphere();

        [FucineDict(ValidateKeysAs = typeof(Element))] public Dictionary<string, FucineExp<int>> RootEffects { get; set; }
        [FucineList] public List<RefMutationEffect> Mutations { get; set; }
        [FucineDict(ValidateKeysAs = typeof(Element))] public Dictionary<string, FucineExp<int>> Aspects { get; set; }
        [FucineDict(ValidateKeysAs = typeof(Element))] public Dictionary<string, FucineExp<int>> XPans { get; set; }
        //[FucineDict(ValidateValueAs = typeof(Element))] public Dictionary<TokenFilterSpec, List<string>> Triggers { get; set; }
        [FucineList(ValidateValueAs = typeof(DeckSpec))] public List<string> DeckShuffles { get; set; }
        [FucineDict(ValidateKeysAs = typeof(DeckSpec))] public Dictionary<string, FucineExp<int>> DeckEffects { get; set; }
        [FucineDict] public Dictionary<FucineExp<bool>, FucineExp<int>> Effects { get; set; }
        [FucineList] public List<TokenFilterSpec> Decays { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> HaltVerb { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> DeleteVerb { get; set; }
        [FucineList] public List<GrandEffects> Furthermore { get; set; }
        [FucineAutoValue] public Dictionary<FucinePath, List<TokenFilterSpec>> Movements { get; set; }

        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX DeckEffectsVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX CreateVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardBurn)] public RetirementVFX DestroyVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.Default)] public RetirementVFX DecaysVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX MovementsVFX { get; set; }

        public GrandEffects() { }
        public GrandEffects(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }

        public void StartGrandEffects(Situation situation)
        {
            Sphere targetSphere = GetTargetSpheres(situation);

            RunGrandEffects(targetSphere, true);

            RecipeExecutionBuffer.ApplyDecorativeEffects();
            RecipeExecutionBuffer.ApplyRecipeInductions();
            Crossroads.ClearRedirectSpheres();
        }

        //even if there are no effects for the recipe, aspect xtriggering should still happen
        public static void RunElementTriggersOnly(Situation situation)
        {
            RunVerbXTriggers(situation);
            RunElementXTriggers(situation);

            RecipeExecutionBuffer.ApplyDecorativeEffects();
            RecipeExecutionBuffer.ApplyRecipeInductions();
            Crossroads.ClearRedirectSpheres();
        }

        public void RunGrandEffects(Sphere localSphere, bool applyLocalXTriggers)
        {
            //shouldn't happen, but happened
            if (localSphere == null || !localSphere.IsValid())
                return;

            Crossroads.MarkLocalSphere(localSphere);

            RunRootEffects();
            RunMutations(localSphere);

            RunRecipeXTriggers(localSphere);
            //local xtriggers aren't applied on furthermores, and work only within interior situation spheres
            if (applyLocalXTriggers)
            {
                var currentSituation = RavensEye.currentSituation;
                RunVerbXTriggers(currentSituation); //NB - in vanilla, verb xtriggers actually react only to aspects the recipe started with
                //so here we have a little theoretical discrepancy
                RunElementXTriggers(currentSituation);
            }

            RunXPans(localSphere);
            //RunDirectTriggers(localSphere, situation);

            RunDeckShuffles();
            RunDeckEffects(localSphere);
            RunEffects(localSphere);
            RunDecays(localSphere);
            RunVerbManipulations();
            RunMovements(localSphere);
            RunFurthermores();
        }

        private static void RunVerbXTriggers(Situation situation)
        {
            AspectsDictionary aspectsPresent = situation.GetAspects(true);
            aspectsPresent.CombineAspects(situation.CurrentRecipe.Aspects);
            IDice dice = Watchman.Get<IDice>();

            Sphere singleSphereByCategory = situation.GetSingleSphereByCategory(SphereCategory.SituationStorage);
            var command = new SecretHistories.Commands.SituationXTriggerCommand(aspectsPresent, dice, singleSphereByCategory);
            situation.Token.ExecuteTokenEffectCommand(command);
        }

        private void RunRootEffects()
        {
            if (RootEffects == null)
                return;

            Dictionary<string, int> scheduledMutations = new Dictionary<string, int>();

            foreach (string elementId in RootEffects.Keys)
                scheduledMutations.Add(elementId, RootEffects[elementId].value);
            FucineRoot root = FucineRoot.Get();
            foreach (string elementId in RootEffects.Keys)
                RecipeExecutionBuffer.ScheduleMutation(root, elementId, scheduledMutations[elementId], true);

            RecipeExecutionBuffer.ApplyMutations();
        }

        private void RunMutations(Sphere sphere)
        {
            if (Mutations == null || !Mutations.Any())
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (RefMutationEffect mutationEffect in Mutations)
            {
                List<Token> targets = mutationEffect.Filter.GetTokens(tokens);

                if (targets.Count == 0)
                    continue;

                Crossroads.MarkAllLocalTokens(targets);
                foreach (Token token in targets)
                {
                    Crossroads.MarkLocalToken(token); //we want token values to be accessible
                    Crossroads.MarkLocalSphere(sphere); //but we still want "default" scope to be the sphere, not the token
                    RecipeExecutionBuffer.ScheduleMutation(token, mutationEffect.Mutate, mutationEffect.Level.value, mutationEffect.Additive, mutationEffect.VFX);
                    RecipeExecutionBuffer.ApplyMutations();
                }
                Crossroads.UnmarkAllLocalTokens();
            }
        }

        public void RunRecipeXTriggers(Sphere sphere)
        {
            if (Aspects == null || !Aspects.Any())
                return;

            List<Token> tokens = sphere.GetElementTokens();

            if (tokens.Count == 0)
                return;

            AspectsDictionary allCatalysts = new AspectsDictionary();
            foreach (KeyValuePair<string, FucineExp<int>> catalyst in Aspects)
                allCatalysts.ApplyMutation(catalyst.Key, catalyst.Value.value);

            if (!allCatalysts.Any())
                return;

            RunXTriggers(tokens, allCatalysts);

            RecipeExecutionBuffer.ApplyGameEffects();
        }


        public static void RunElementXTriggers(Situation situation)
        {
            List<Token> tokens = situation.GetElementTokensInSituation();
            if (tokens.Count == 0)
                return;

            AspectsDictionary allCatalysts = situation.GetAspects(true);
            RunXTriggers(tokens, allCatalysts);

            RecipeExecutionBuffer.ApplyGameEffects();
        }

        public void RunXPans(Sphere initialSphere)
        {
            if (XPans == null || !XPans.Any())
                return;

            AspectsDictionary allCatalysts = new AspectsDictionary();
            foreach (KeyValuePair<string, FucineExp<int>> catalyst in XPans)
                allCatalysts.ApplyMutation(catalyst.Key, catalyst.Value.value);

            foreach (Sphere sphere in Watchman.Get<HornedAxe>().GetExteriorSpheres())
            {
                List<Token> tokens = sphere.GetElementTokens();

                if (tokens.Count == 0)
                    continue;

                Crossroads.MarkLocalSphere(sphere);
                RunXTriggers(tokens, allCatalysts);
            }

            Crossroads.MarkLocalSphere(initialSphere);

            RecipeExecutionBuffer.ApplyGameEffects();
            RecipeExecutionBuffer.ApplyDeckRenews();
        }

        /*
        public void RunDirectTriggers(Sphere sphere, Situation situation)
        {
            if (Triggers == null || !Triggers.Any())
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (TokenFilterSpec filterSpec in Triggers.Keys)
            {
                List<Token> targets = filterSpec.GetTokens(tokens);

                if (targets.Count == 0)
                    continue;

                allCatalysts.Clear();
                foreach (string trigger in Triggers[filterSpec])
                    allCatalysts.Add(trigger, 1);

                foreach (Token token in targets)
                    RunXTriggers(token, situation, allCatalysts);
            }
        }*/

        public static void RunXTriggers(List<Token> tokens, Dictionary<string, int> catalysts)
        {
            foreach (Token token in tokens)
                RunXTriggers(token, catalysts);
        }

        public static void RunXTriggers(Token token, Dictionary<string, int> catalysts)
        {
            if (token.IsValidElementStack() == false)
                return;

            Compendium compendium = Watchman.Get<Compendium>();

            var allAspects = token.GetAspects(true);
            var orderedAspects = allAspects.Keys.ToList().OrderBy(key => key);

            foreach (string aspect in orderedAspects)
            {
                var xtriggers = compendium.GetEntityById<Element>(aspect).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;
                if (xtriggers != null)
                    foreach (KeyValuePair<string, int> catalyst in catalysts)
                        if (xtriggers.ContainsKey(catalyst.Key))
                            foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                if (morphDetails.Execute(token, aspect, allAspects[aspect], catalyst.Value))
                                    break;
            }
        }

        private void RunDeckShuffles()
        {
            if (DeckShuffles == null || !DeckShuffles.Any())
                return;

            foreach (string deckId in DeckShuffles)
            {
                if (deckId[deckId.Length - 1] == '*')
                {
                    foreach (DrawPile pile in Watchman.Get<DealersTable>().GetDrawPiles())
                        if (NoonExtensions.WildcardMatchId(pile.GetDeckSpecId(), deckId))
                            RecipeExecutionBuffer.ScheduleDeckRenew(deckId);
                }
                else
                    RecipeExecutionBuffer.ScheduleDeckRenew(deckId);
            }

            RecipeExecutionBuffer.ApplyDeckRenews();
        }

        private void RunDeckEffects(Sphere sphere)
        {
            if (DeckEffects == null || !DeckEffects.Any())
                return;

            foreach (string deckId in DeckEffects.Keys)
                Legerdemain.Deal(deckId, sphere, DeckEffects[deckId].value, DeckEffectsVFX);

            RecipeExecutionBuffer.ApplyMovements();
            RecipeExecutionBuffer.ApplyDeckRenews();
        }

        private void RunEffects(Sphere sphere)
        {
            if (Effects == null || !Effects.Any())
                return;

            List<Token> allTokens = sphere.Tokens;
            foreach (FucineExp<bool> effect in Effects.Keys)
            {
                int level = Effects[effect].value;
                if (level < 0)
                {
                    List<Token> affectedTokens = allTokens.FilterTokens(effect).SelectRandom(Math.Abs(level));
                    foreach (Token token in affectedTokens)
                        RecipeExecutionBuffer.ScheduleTransformation(token, string.Empty, DestroyVFX);
                }
                else
                    RecipeExecutionBuffer.ScheduleCreations(sphere, effect.targetElement, level, CreateVFX);
            }

            RecipeExecutionBuffer.ApplyTransformations();
            RecipeExecutionBuffer.ApplyCreations();
        }

        private void RunDecays(Sphere sphere)
        {
            if (Decays == null || !Decays.Any())
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (TokenFilterSpec tokenFilterSpec in Decays)
                foreach (Token token in tokenFilterSpec.GetTokens(tokens))
                    RecipeExecutionBuffer.ScheduleDecay(token, DecaysVFX);

            RecipeExecutionBuffer.ApplyTransformations();
        }

        private void RunVerbManipulations()
        {
            if (HaltVerb != null && HaltVerb.Any())
            {
                AspectsDictionary allCatalysts = new AspectsDictionary();

                foreach (KeyValuePair<string, FucineExp<int>> haltVerbEffect in HaltVerb)
                    allCatalysts.Add(haltVerbEffect.Key, haltVerbEffect.Value.value);

                foreach (KeyValuePair<string, int> haltVerbEffect in allCatalysts)
                    Watchman.Get<HornedAxe>().HaltSituation(haltVerbEffect.Key, haltVerbEffect.Value);
            }

            if (DeleteVerb != null && DeleteVerb.Any())
            {
                AspectsDictionary allCatalysts = new AspectsDictionary();

                foreach (KeyValuePair<string, FucineExp<int>> deleteVerbEffect in DeleteVerb)
                    allCatalysts.Add(deleteVerbEffect.Key, deleteVerbEffect.Value.value);

                foreach (KeyValuePair<string, int> deleteVerbEffect in allCatalysts)
                    Watchman.Get<HornedAxe>().HaltSituation(deleteVerbEffect.Key, deleteVerbEffect.Value);
            }
        }

        private void RunMovements(Sphere fromSphere)
        {
            if (Movements == null || !Movements.Any())
                return;

            List<Token> tokens = fromSphere.GetElementTokens();

            foreach (FucinePath fucinePath in Movements.Keys)
            {
                List<Sphere> targetSpheres = Crossroads.GetSpheresByPath(fucinePath);
                if (targetSpheres.Count == 0)
                    continue;

                foreach (TokenFilterSpec filter in Movements[fucinePath])
                    foreach (Token token in filter.GetTokens(tokens))
                        RecipeExecutionBuffer.ScheduleMovement(token, targetSpheres[UnityEngine.Random.Range(0, targetSpheres.Count)], MovementsVFX);
            }

            RecipeExecutionBuffer.ApplyMovements();
        }

        private void RunFurthermores()
        {
            if (Furthermore == null || !Furthermore.Any())
                return;

            foreach (GrandEffects furthermore in Furthermore)
            {
                //we don't want a local sphere from previous furthermores (or from the main grandeffects) to affect furthermore's target
                //so, by default, all furthermores are executed locally in situation
                Crossroads.UnmarkLocalSphere();

                var targetSpheresAsOne = furthermore.Target.GetSpheresByPathAsSingleSphere();
                furthermore.RunGrandEffects(targetSpheresAsOne, false);
            }
        }

        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            int i = 0;
            foreach (GrandEffects furthermore in Furthermore)
                furthermore.SetId($"{this.Id} furthermore #{i++}");

            /*
            //reducing amount of entities
            foreach (CachedFucineProperty<GrandEffects> property in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
            {
                object value = property.GetViaFastInvoke(this);
                if ((value as ICollection)?.Count == 0)
                    property.SetViaFastInvoke(this, null);
            }*/
        }
    }
}