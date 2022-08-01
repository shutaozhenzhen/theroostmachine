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
using SecretHistories.Assets.Scripts.Application.Entities.NullEntities;
using SecretHistories.Infrastructure;

using Roost.Twins;
using Roost.Twins.Entities;

namespace Roost.World.Recipes.Entities
{
    public class GrandEffects : AbstractEntity<GrandEffects>
    {
        [FucinePathValue(defaultValue: "~/local")] public FucinePath Target { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> RootEffects { get; set; }
        [FucineDict] public Dictionary<TokenFilterSpec, List<RefMutationEffect>> Mutations { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> Aspects { get; set; }
        [FucineList] public List<string> DeckShuffles { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> DeckEffects { get; set; }
        [FucineDict] public Dictionary<FucineExp<bool>, FucineExp<int>> Effects { get; set; }
        [FucineList] public List<TokenFilterSpec> Decays { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> HaltVerb { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> DeleteVerb { get; set; }
        [FucineDict] public List<GrandEffects> DistantEffects { get; set; }

        [FucineCustomDict(KeyImporter: typeof(FucinePathPanImporter), ValueImporter: typeof(ListPanImporter))]
        public Dictionary<FucinePath, List<TokenFilterSpec>> Movements { get; set; }

        [FucineDict] public List<LinkedRecipeDetails> Induces { get; set; }


        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX DeckEffectsVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX CreateVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardBurn)] public RetirementVFX DestroyVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardLight)] public RetirementVFX DecaysVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX MovementsVFX { get; set; }

        public GrandEffects() { }
        public GrandEffects(ContentImportLog log) : base(new EntityData(), log) { }
        public GrandEffects(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }

        public static void RunGrandEffects(GrandEffects grandEffects, Situation situation, Sphere localSphere)
        {
            //even if there are no effects for the recipe, aspect xtriggering should happen
            if (grandEffects == null)
            {
                Crossroads.MarkLocalSphere(localSphere);
                RunXTriggers(null, localSphere, situation, true);

                RecipeExecutionBuffer.ApplyVFX();
                RecipeExecutionBuffer.ApplyRecipeInductions();
            }
            else
            {
                grandEffects.Run(situation, localSphere, true);

                RecipeExecutionBuffer.ApplyVFX();
                RecipeExecutionBuffer.ApplyRecipeInductions();
            }
        }

        private void Run(Situation situation, Sphere localSphere, bool localXtriggers)
        {
            Crossroads.MarkLocalSphere(localSphere);

            RunRootEffects();
            RunMutations(localSphere);
            RunXTriggers(Aspects, localSphere, situation, localXtriggers);
            RunDeckShuffles();
            RunDeckEffects(localSphere);
            RunEffects(localSphere);
            RunDecays(localSphere);
            RunVerbManipulations();
            RunDistantEffects(situation);
            RunMovements(localSphere);
            RunRecipeInductions(situation);
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

            foreach (TokenFilterSpec filter in Mutations.Keys)
            {
                List<Token> targets = filter.GetTokens(tokens);

                if (targets.Count > 0)
                    foreach (RefMutationEffect mutationEffect in Mutations[filter])
                        RecipeExecutionBuffer.ScheduleMutation(targets, mutationEffect.Mutate, mutationEffect.Level.value, mutationEffect.Additive, mutationEffect.VFX);
            }

            RecipeExecutionBuffer.ApplyMutations();
        }

        private static readonly AspectsDictionary allCatalystsInSphere = new AspectsDictionary();
        public static void RunXTriggers(Dictionary<string, FucineExp<int>> Aspects, Sphere sphere, Situation situation, bool catalystFromElements)
        {
            if (Aspects != null)
                foreach (KeyValuePair<string, FucineExp<int>> catalyst in Aspects)
                    allCatalystsInSphere[catalyst.Key] = catalyst.Value.value;
            if (catalystFromElements)
                allCatalystsInSphere.ApplyMutations(sphere.GetTotalAspects(true));

            if (allCatalystsInSphere.Count == 0)
                return;

            foreach (Token token in sphere.GetElementTokens())
                RunXTriggersOnToken(token, situation, allCatalystsInSphere);

            allCatalystsInSphere.Clear();
            Crossroads.UnmarkLocalToken();
            RecipeExecutionBuffer.ApplyAllEffects();
        }

        public static void RunXTriggersOnToken(Token token, Situation situation, Dictionary<string, int> catalysts)
        {
            if (token.IsValidElementStack() == false)
                return;

            Compendium compendium = Watchman.Get<Compendium>();
            Dictionary<string, List<RefMorphDetails>> xtriggers = Machine.GetEntity<Element>(token.PayloadEntityId).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;

            if (xtriggers != null)
                foreach (KeyValuePair<string, int> catalyst in catalysts)
                    if (xtriggers.ContainsKey(catalyst.Key))
                        foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                            morphDetails.Execute(situation, token, token.PayloadEntityId, token.Quantity, catalyst.Value, true);

            AspectsDictionary tokenAspects = token.GetAspects(false);
            foreach (KeyValuePair<string, int> aspect in tokenAspects)
            {
                xtriggers = compendium.GetEntityById<Element>(aspect.Key).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;
                if (xtriggers != null)
                    foreach (KeyValuePair<string, int> catalyst in catalysts)
                        if (xtriggers.ContainsKey(catalyst.Key))
                            foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                morphDetails.Execute(situation, token, aspect.Key, aspect.Value, catalyst.Value, false);
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
                Legerdemain.Deal(deckId, sphere, DeckEffects[deckId].value);

            RecipeExecutionBuffer.ApplyMovements();
            RecipeExecutionBuffer.ApplyRenews();
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
                    RecipeExecutionBuffer.ScheduleCreation(sphere, effect.targetElement, level, CreateVFX);
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
                    allCatalystsInSphere.Add(haltVerbEffect.Key, haltVerbEffect.Value.value);

                foreach (KeyValuePair<string, int> haltVerbEffect in allCatalystsInSphere)
                    Watchman.Get<HornedAxe>().HaltSituation(haltVerbEffect.Key, haltVerbEffect.Value);

                allCatalystsInSphere.Clear();
            }

            if (DeleteVerb != null)
            {
                foreach (KeyValuePair<string, FucineExp<int>> deleteVerbEffect in DeleteVerb)
                    allCatalystsInSphere.Add(deleteVerbEffect.Key, deleteVerbEffect.Value.value);

                foreach (KeyValuePair<string, int> deleteVerbEffect in allCatalystsInSphere)
                    Watchman.Get<HornedAxe>().HaltSituation(deleteVerbEffect.Key, deleteVerbEffect.Value);

                allCatalystsInSphere.Clear();
            }
        }

        private void RunDistantEffects(Situation situation)
        {
            if (DistantEffects == null)
                return;

            foreach (GrandEffects sphereEffect in DistantEffects)
            {
                foreach (Sphere sphere in new List<Sphere>(Crossroads.GetSpheresByPath(sphereEffect.Target)))
                    sphereEffect.Run(situation, sphere, false);
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

        private void RunRecipeInductions(Situation situation)
        {
            if (Induces == null)
                return;

            foreach (LinkedRecipeDetails link in Induces)
                RecipeExecutionBuffer.ScheduleRecipeInduction(situation, link);
        }

        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            ContentImportLog subLog = new ContentImportLog();
            foreach (TokenFilterSpec filter in Mutations.Keys)
            {
                filter.OnPostImport(subLog, populatedCompendium);
                foreach (RefMutationEffect mutation in Mutations[filter])
                    mutation.OnPostImport(subLog, populatedCompendium);
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
                Birdsong.Tweet(VerbosityLevel.Essential, message.MessageLevel, $"PROBLEM IN RECIPE '{this.Id}' - {message.Description}'");

            int i = 0;
            foreach (GrandEffects distantEffect in DistantEffects)
            {
                distantEffect.SetId(this.Id + " distant effect #" + i++);
                distantEffect.OnPostImport(log, populatedCompendium);
            }

            foreach (LinkedRecipeDetails link in Induces)
                link.OnPostImport(log, populatedCompendium);

            //reducing amount of entities
            foreach (CachedFucineProperty<GrandEffects> property in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
            {
                object value = property.GetViaFastInvoke(this);
                if ((value as ICollection)?.Count == 0)
                    property.SetViaFastInvoke(this, null);
            }
        }
    }

    public class RefMutationEffect : AbstractEntity<RefMutationEffect>, IQuickSpecEntity
    {
        [FucineEverValue(DefaultValue = null)] public string Mutate { get; set; }
        [FucineEverValue("1")] public FucineExp<int> Level { get; set; }
        [FucineEverValue(false)] public bool Additive { get; set; }
        [FucineEverValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX VFX { get; set; }

        public RefMutationEffect() { }
        public RefMutationEffect(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            if (Mutate == null)
            {
                foreach (object key in UnknownProperties.Keys)
                    if (populatedCompendium.GetEntityById<Element>(key.ToString()) != null)
                    {
                        this.Mutate = key.ToString();
                        this.Level = new FucineExp<int>(UnknownProperties[key].ToString());
                        break;
                    }

                if (Mutate == null)
                {
                    log.LogWarning("MUTATION LACKS 'MUTATE' PROPERTY");
                    return;
                }
                else
                    UnknownProperties.Remove(Mutate);
            }

            this.SetId(Mutate);
            populatedCompendium.SupplyElementIdsForValidation(this.Id);
        }

        public void QuickSpec(string value)
        {
            SetId(value);
            Mutate = value;
            Level = new FucineExp<int>("1");
            Additive = false;
            VFX = RetirementVFX.CardTransformWhite;
        }
    }

    public enum MorphEffectsExtended { Transform, Spawn, Mutate, SetMutation, DeckDraw, DeckShuffle, Destroy, Decay, Induce, Link }
    public class RefMorphDetails : AbstractEntity<RefMorphDetails>, IQuickSpecEntity
    {
        [FucineValue(DefaultValue = MorphEffectsExtended.Transform)] public MorphEffectsExtended MorphEffect { get; set; }
        [FucineConstruct("1")] public FucineExp<int> Level { get; set; }
        [FucineConstruct("100")] public FucineExp<int> Chance { get; set; }

        [FucineEverValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX VFX { get; set; }
        [FucineValue(false)] public bool IgnoreTargetQuantity { get; set; }
        [FucineValue(false)] public bool IgnoreCatalystQuantity { get; set; }

        private LinkedRecipeDetails Induction { get; set; }
        [FucinePathValue] public FucinePath ToPath { get; set; }
        [FucineSubEntity(typeof(Expulsion))] public Expulsion Expulsion { get; set; }

        public void QuickSpec(string value)
        {
            this.SetId(value);
            Chance = new FucineExp<int>("100");
            MorphEffect = MorphEffectsExtended.Transform;
            Level = new FucineExp<int>("1");
            IgnoreTargetQuantity = false;
            IgnoreCatalystQuantity = false;
            Expulsion = null;
            VFX = RetirementVFX.CardTransformWhite;
        }

        public RefMorphDetails() { }
        public RefMorphDetails(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            switch (MorphEffect)
            {
                case MorphEffectsExtended.Transform:
                case MorphEffectsExtended.Spawn:
                case MorphEffectsExtended.Mutate:
                case MorphEffectsExtended.SetMutation:
                    if (Id == null)
                        foreach (object key in UnknownProperties.Keys)
                            if (populatedCompendium.GetEntityById<Element>(key.ToString()) != null)
                            {
                                this.SetId(key.ToString());
                                this.Level = new FucineExp<int>(UnknownProperties[key].ToString());
                                break;
                            }

                    if (Id == null)
                        goto NO_ID;

                    populatedCompendium.SupplyElementIdsForValidation(this.Id);
                    break;
                case MorphEffectsExtended.Link:
                case MorphEffectsExtended.Induce:
                    if (Id == null)
                        foreach (object key in UnknownProperties.Keys)
                            if (populatedCompendium.GetEntityById<Recipe>(key.ToString()) != null)
                            {
                                this.SetId(key.ToString());
                                this.Level = new FucineExp<int>(UnknownProperties[key].ToString());
                                break;
                            }

                    if (Id == null)
                        goto NO_ID;

                    Recipe linkedRecipe = populatedCompendium.GetEntityById<Recipe>(this.Id);
                    if (linkedRecipe == null)
                        log.LogWarning($"unknown recipe id '{this.Id}'");

                    if (MorphEffect == MorphEffectsExtended.Induce)
                    {
                        Induction = LinkedRecipeDetails.AsCurrentRecipe(linkedRecipe); //no other way to construct it normally
                        Induction.SetCustomProperty("chance", new FucineExp<int>("100")); //xtriggers already have chance
                        Induction.ToPath = this.ToPath;
                        Induction.Expulsion = this.Expulsion;
                    }
                    break;
                case MorphEffectsExtended.DeckDraw:
                case MorphEffectsExtended.DeckShuffle:
                    if (Id == null)
                        foreach (object key in UnknownProperties.Keys)
                            if (populatedCompendium.GetEntityById<DeckSpec>(key.ToString()) != null)
                            {
                                this.SetId(key.ToString());
                                this.Level = new FucineExp<int>(UnknownProperties[key].ToString());
                                break;
                            }

                    if (Id == null)
                        goto NO_ID;

                    if (populatedCompendium.GetEntityById<DeckSpec>(this.Id) == null)
                        log.LogWarning($"UNKNOWN DECK ID '{this.Id}' IN XTRIGGERS");
                    break;
                default:
                    break;
            }

            UnknownProperties.Remove(this.Id);
            if (MorphEffect != MorphEffectsExtended.Induce)
                Induction = null;
            //even if these properties are needed, they are safely wrapped inside the Induction by now
            Expulsion = null;
            ToPath = null;
            return;

        NO_ID:
            log.LogWarning("XTRIGGER ID ISN'T SET");
            Expulsion = null;
            ToPath = null;
        }

        public void Execute(Situation situation, Token targetToken, string targetElementId, int targetQuantity, int catalystAmount, bool onToken)
        {
            Crossroads.MarkLocalToken(targetToken);

            if (UnityEngine.Random.Range(1, 101) > Chance.value)
                return;

            if (IgnoreTargetQuantity)
                targetQuantity = 1;
            if (IgnoreCatalystQuantity)
                catalystAmount = 1;

            switch (MorphEffect)
            {
                case MorphEffectsExtended.Transform:
                    if (onToken)
                        RecipeExecutionBuffer.ScheduleTransformation(targetToken, this.Id, VFX);
                    else
                    {
                        //if there are several chanced transforms on a single aspect, we don't want mutations to happen several times
                        //(it is TRANSFORM, previous aspect must end up being a single thing, not multiply into several different things)
                        //thus we're making use of "mutation groups" - if one transform did trigger and registered a mutation, then other won't happen
                        RecipeExecutionBuffer.ScheduleUniqueMutation(targetToken, targetElementId, -targetQuantity, true, RetirementVFX.None, "-" + targetToken.PayloadEntityId + targetElementId);
                        RecipeExecutionBuffer.ScheduleUniqueMutation(targetToken, this.Id, targetQuantity, true, VFX, targetToken.PayloadEntityId + targetElementId);
                    }
                    break;
                case MorphEffectsExtended.Spawn:
                    RecipeExecutionBuffer.ScheduleCreation(targetToken.Sphere, this.Id, targetQuantity * Level.value * catalystAmount, VFX);
                    break;
                case MorphEffectsExtended.SetMutation:
                    RecipeExecutionBuffer.ScheduleMutation(targetToken, this.Id, Level.value * catalystAmount * targetQuantity, false, VFX);
                    break;
                case MorphEffectsExtended.Mutate:
                    RecipeExecutionBuffer.ScheduleMutation(targetToken, this.Id, Level.value * catalystAmount * targetQuantity, true, VFX);
                    break;
                case MorphEffectsExtended.DeckDraw:
                    Legerdemain.Deal(this.Id, targetToken.Sphere, Level.value * catalystAmount * targetQuantity);
                    break;
                case MorphEffectsExtended.DeckShuffle:
                    RecipeExecutionBuffer.ScheduleDeckRenew(this.Id);
                    break;
                case MorphEffectsExtended.Destroy:
                    RecipeExecutionBuffer.ScheduleRetirement(targetToken, VFX);
                    break;
                case MorphEffectsExtended.Induce:
                    RecipeExecutionBuffer.ScheduleRecipeInduction(situation, Induction);
                    break;
                case MorphEffectsExtended.Link:
                    Machine.PushTemporaryRecipeLink(this.Id, Level.value);
                    break;
                default: Birdsong.Tweet($"Unknown trigger '{MorphEffect}' for element stack '{targetToken.PayloadEntityId}'"); break;
            }
        }
    }

    public class TokenFilterSpec : AbstractEntity<TokenFilterSpec>, IQuickSpecEntity, IMalleable
    {
        [FucineConstruct(FucineExp<int>.UNDEFINED)] public FucineExp<bool> Filter { get; set; }
        [FucineConstruct(FucineExp<int>.UNDEFINED)] public FucineExp<int> Limit { get; set; } //unlimited by default

        public TokenFilterSpec() { }
        public TokenFilterSpec(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            if (Filter.isUndefined)
            {
                foreach (object key in UnknownProperties.Keys)
                {
                    this.Filter = new FucineExp<bool>(key.ToString());
                    this.Limit = new FucineExp<int>(UnknownProperties[key].ToString());
                    break;
                }

                if (Filter.isUndefined)
                    log.LogWarning("FILTER IS UNDEFINED");
                else
                    UnknownProperties.Remove(this.Filter.formula);
            }
        }

        public List<Token> GetTokens(List<Token> tokens)
        {
            if (Limit.isUndefined)
                return tokens.FilterTokens(Filter);

            return tokens.FilterTokens(Filter).SelectRandom(Limit.value);
        }

        public void QuickSpec(string data)
        {
            try
            {
                Filter = new FucineExp<bool>(data);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void Mold(EntityData data, ContentImportLog log)
        {
            try
            {
                MoldingsStorage.ConvertExpulsionFilters(data);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}