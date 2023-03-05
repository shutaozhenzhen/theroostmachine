﻿using System;
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

using Roost.Twins;
using Roost.Twins.Entities;

namespace Roost.World.Recipes.Entities
{
    public class GrandEffects : AbstractEntity<GrandEffects>
    {
        [FucinePathValue(defaultValue: "~/sphere")] public FucinePath Target { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> RootEffects { get; set; }
        [FucineList] public List<RefMutationEffect> Mutations { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> Aspects { get; set; }
        [FucineList] public List<string> DeckShuffles { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> DeckEffects { get; set; }
        [FucineDict] public Dictionary<FucineExp<bool>, FucineExp<int>> Effects { get; set; }
        [FucineList] public List<TokenFilterSpec> Decays { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> HaltVerb { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> DeleteVerb { get; set; }
        [FucineList] public List<GrandEffects> DistantEffects { get; set; }
        [FucineAutoValue] public Dictionary<FucinePath, List<TokenFilterSpec>> Movements { get; set; }

        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX DeckEffectsVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX CreateVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardBurn)] public RetirementVFX DestroyVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardLight)] public RetirementVFX DecaysVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX MovementsVFX { get; set; }

        public GrandEffects() { }
        public GrandEffects(ContentImportLog log) : base(new EntityData(), log) { }
        public GrandEffects(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }

        public void RunGrandEffects(Situation situation, Sphere localSphere, bool localXtriggers)
        {
            //shouldn't happen, but happened
            if (localSphere == null)
                return;

            Crossroads.MarkLocalSphere(localSphere);

            RunVerbXTriggers(situation);

            RunRootEffects();
            RunMutations(localSphere);

            RunRecipeXTriggers(Aspects, localSphere, situation);
            if (localXtriggers)
                RunElementXTriggers(localSphere, situation);

            RunDeckShuffles();
            RunDeckEffects(localSphere);
            RunEffects(localSphere);
            RunDecays(localSphere);
            RunVerbManipulations();
            RunDistantEffects(situation, localSphere);
            RunMovements(localSphere);

            RecipeExecutionBuffer.ApplyVFX();
            RecipeExecutionBuffer.ApplyRecipeInductions();
        }

        public static void RunElementTriggersOnly(Situation situation, Sphere localSphere)
        {
            //even if there are no effects for the recipe, aspect xtriggering should still happen
            Crossroads.MarkLocalSphere(localSphere);
            RunElementXTriggers(localSphere, situation);

            RecipeExecutionBuffer.ApplyVFX();
            RecipeExecutionBuffer.ApplyRecipeInductions();
        }

        private void RunVerbXTriggers(Situation situation)
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
            if (Mutations == null)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (RefMutationEffect mutationEffect in Mutations)
            {
                List<Token> targets = mutationEffect.Filter.GetTokens(tokens);

                if (targets.Count > 0)
                {
                    RecipeExecutionBuffer.ScheduleMutation(targets, mutationEffect.Mutate, mutationEffect.Level.value, mutationEffect.Additive, mutationEffect.VFX);
                    RecipeExecutionBuffer.ApplyMutations();
                }
            }
        }

        private static readonly AspectsDictionary allCatalysts = new AspectsDictionary();
        public static void RunRecipeXTriggers(Dictionary<string, FucineExp<int>> Aspects, Sphere sphere, Situation situation)
        {
            if (Aspects == null || Aspects.Count == 0)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            if (tokens.Count == 0)
                return;

            allCatalysts.Clear();
            foreach (KeyValuePair<string, FucineExp<int>> catalyst in Aspects)
                allCatalysts.ApplyMutation(catalyst.Key, catalyst.Value.value);

            if (allCatalysts.Count == 0)
                return;

            foreach (Token token in tokens)
                RunXTriggers(token, situation, allCatalysts);

            RecipeExecutionBuffer.ApplyAllEffects();
        }


        public static void RunElementXTriggers(Sphere sphere, Situation situation)
        {
            List<Token> tokens = sphere.GetElementTokens();
            if (tokens.Count == 0)
                return;

            allCatalysts.Clear();
            foreach (Token token in tokens)
                allCatalysts.CombineAspects(token.GetAspects(true));

            foreach (Token token in tokens)
                RunXTriggers(token, situation, allCatalysts);

            RecipeExecutionBuffer.ApplyAllEffects();
        }

        public static void RunXTriggers(Token token, Situation situation, Dictionary<string, int> catalysts)
        {
            if (token.IsValidElementStack() == false)
                return;

            Compendium compendium = Watchman.Get<Compendium>();

            Dictionary<string, List<RefMorphDetails>> xtriggers = Machine.GetEntity<Element>(token.PayloadEntityId).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;

            if (xtriggers != null)
                foreach (KeyValuePair<string, int> catalyst in catalysts)
                    if (xtriggers.ContainsKey(catalyst.Key))
                        foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                            morphDetails.Execute(situation, token, token.PayloadEntityId, token.Quantity, catalyst.Value, false);

            AspectsDictionary tokenAspects = token.GetAspects(false);
            foreach (KeyValuePair<string, int> aspect in tokenAspects)
            {
                xtriggers = compendium.GetEntityById<Element>(aspect.Key).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;
                if (xtriggers != null)
                    foreach (KeyValuePair<string, int> catalyst in catalysts)
                        if (xtriggers.ContainsKey(catalyst.Key))
                            foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                morphDetails.Execute(situation, token, aspect.Key, aspect.Value, catalyst.Value, true);
            }
        }

        private void RunDeckShuffles()
        {
            if (DeckShuffles == null)
                return;

            foreach (string deckId in DeckShuffles)
            {
                if (deckId[deckId.Length - 1] == '*')
                {
                    string wildDeckId = deckId.Remove(deckId.Length - 1);
                    foreach (DrawPile pile in Watchman.Get<DealersTable>().GetDrawPiles())
                        if (pile.GetDeckSpecId().StartsWith(wildDeckId))
                            Legerdemain.RenewDeck(deckId);
                }
                else
                    Legerdemain.RenewDeck(deckId);
            }
        }

        private void RunDeckEffects(Sphere sphere)
        {
            if (DeckEffects == null)
                return;

            foreach (string deckId in DeckEffects.Keys)
                Legerdemain.Deal(deckId, sphere, DeckEffects[deckId].value, DeckEffectsVFX);

            RecipeExecutionBuffer.ApplyMovements();
            RecipeExecutionBuffer.ApplyDeckRenews();
        }

        private void RunEffects(Sphere sphere)
        {
            if (Effects == null)
                return;

            List<Token> allTokens = sphere.Tokens;
            foreach (FucineExp<bool> effect in Effects.Keys)
            {
                int level = Effects[effect].value;
                if (level < 0)
                {
                    List<Token> affectedTokens = allTokens.FilterTokens(effect).SelectRandom(Math.Abs(level));
                    foreach (Token token in affectedTokens)
                        RecipeExecutionBuffer.ScheduleRetirement(token, DestroyVFX);
                }
                else
                    RecipeExecutionBuffer.ScheduleSpawn(sphere, effect.targetElement, level, CreateVFX);
            }

            RecipeExecutionBuffer.ApplyRetirements();
            RecipeExecutionBuffer.ApplyCreations();
        }

        private void RunDecays(Sphere sphere)
        {
            if (Decays == null)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (TokenFilterSpec tokenFilterSpec in Decays)
                foreach (Token token in tokenFilterSpec.GetTokens(tokens))
                    RecipeExecutionBuffer.ScheduleDecay(token, DecaysVFX);

            RecipeExecutionBuffer.ApplyRetirements();
            RecipeExecutionBuffer.ApplyTransformations();
        }

        private void RunVerbManipulations()
        {
            if (HaltVerb != null)
            {
                foreach (KeyValuePair<string, FucineExp<int>> haltVerbEffect in HaltVerb)
                    allCatalysts.Add(haltVerbEffect.Key, haltVerbEffect.Value.value);

                foreach (KeyValuePair<string, int> haltVerbEffect in allCatalysts)
                    Watchman.Get<HornedAxe>().HaltSituation(haltVerbEffect.Key, haltVerbEffect.Value);

                allCatalysts.Clear();
            }

            if (DeleteVerb != null)
            {
                foreach (KeyValuePair<string, FucineExp<int>> deleteVerbEffect in DeleteVerb)
                    allCatalysts.Add(deleteVerbEffect.Key, deleteVerbEffect.Value.value);

                foreach (KeyValuePair<string, int> deleteVerbEffect in allCatalysts)
                    Watchman.Get<HornedAxe>().HaltSituation(deleteVerbEffect.Key, deleteVerbEffect.Value);

                allCatalysts.Clear();
            }
        }

        private void RunDistantEffects(Situation situation, Sphere localSphere)
        {
            if (DistantEffects == null)
                return;

            foreach (GrandEffects distantEffect in DistantEffects)
                foreach (Sphere sphere in distantEffect.Target.GetSpheresByPath())
                {
                    distantEffect.RunGrandEffects(situation, sphere, false);
                    Crossroads.MarkLocalSphere(localSphere);
                }
        }

        private void RunMovements(Sphere fromSphere)
        {
            if (Movements == null)
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

        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            ContentImportLog subLog = new ContentImportLog();
            foreach (RefMutationEffect mutation in Mutations)
            {
                mutation.OnPostImport(subLog, populatedCompendium);
                mutation.Filter.OnPostImport(subLog, populatedCompendium);
            }

            foreach (string deckId in DeckShuffles)
                if (populatedCompendium.GetEntityById<DeckSpec>(deckId) == null)
                    log.LogWarning($"UNKNOWN DECK ID '{deckId}' TO SHUFFLE IN RECIPE EFFECTS '{Id}'");

            foreach (string deckId in DeckEffects.Keys)
                if (populatedCompendium.GetEntityById<DeckSpec>(deckId) == null)
                    log.LogWarning($"UNKNOWN DECK ID '{deckId}' TO DRAW FROM IN RECIPE EFFECTS '{Id}'");

            foreach (TokenFilterSpec filter in Decays)
                filter.OnPostImport(subLog, populatedCompendium);

            foreach (List<TokenFilterSpec> filters in Movements.Values)
                foreach (TokenFilterSpec filter in filters)
                    filter.OnPostImport(subLog, populatedCompendium);

            foreach (ILogMessage message in subLog.GetMessages())
                Birdsong.TweetLoud($"PROBLEM IN RECIPE '{this.Id}' - {message.Description}'");

            int i = 0;
            foreach (GrandEffects distantEffect in DistantEffects)
            {
                distantEffect.SetId(this.Id + " distant effect #" + i++);
                distantEffect.OnPostImport(log, populatedCompendium);
            }

            //reducing amount of entities
            foreach (CachedFucineProperty<GrandEffects> property in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
            {
                object value = property.GetViaFastInvoke(this);
                if ((value as ICollection)?.Count == 0)
                    property.SetViaFastInvoke(this, null);
            }
        }
    }
}