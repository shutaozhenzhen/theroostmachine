using System;
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

        [FucineAutoValue]
        public Dictionary<FucinePath, List<TokenFilterSpec>> Movements { get; set; }

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

    public class RefMutationEffect : AbstractEntity<RefMutationEffect>, IQuickSpecEntity
    {
        [FucineValue(DefaultValue = null)] public string Mutate { get; set; }
        [FucineConstruct("0")] public FucineExp<int> Level { get; set; }
        [FucineValue(false)] public bool Additive { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX VFX { get; set; }
        [FucineSubEntity] public TokenFilterSpec Filter { get; set; }

        protected override Type ValidateIdAs => typeof(Element);

        public RefMutationEffect() { }
        public RefMutationEffect(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            string LIMIT = nameof(Filter.Limit).ToLower();

            if (UnknownProperties.ContainsKey(LIMIT))
            {
                try
                {
                    Filter.Limit = new FucineExp<int>(UnknownProperties[LIMIT].ToString());
                }
                catch (Exception ex)
                {
                    log.LogProblem($"Malformed limit {UnknownProperties[LIMIT]}: {ex.FormatException()}");
                }

                UnknownProperties.Remove(LIMIT);
            }


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
                UnknownProperties.Remove(Mutate);
            }

            this.SetId(Mutate);
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

    public enum MorphEffectsExtended { Transform, Spawn, Mutate, SetMutation, DeckDraw, DeckShuffle, Destroy, Decay, Induce, Link, GrandEffects }
    public class RefMorphDetails : AbstractEntity<RefMorphDetails>, IQuickSpecEntity
    {
        public enum TriggerMode { Default, TokenOnly, AspectOnly, Always }
        [FucineValue(DefaultValue = MorphEffectsExtended.Transform)] public MorphEffectsExtended MorphEffect { get; set; }
        [FucineConstruct("1")] public FucineExp<int> Level { get; set; }
        [FucineConstruct("100")] public FucineExp<int> Chance { get; set; }

        [FucineEverValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX VFX { get; set; }
        [FucineValue(false)] public bool IgnoreTargetQuantity { get; set; }
        [FucineValue(false)] public bool IgnoreCatalystQuantity { get; set; }

        private LinkedRecipeDetails Induction { get; set; }
        [FucinePathValue] public FucinePath ToPath { get; set; }
        [FucineSubEntity] public Expulsion Expulsion { get; set; }

        [FucineSubEntity] public GrandEffects GrandEffects { get; set; }

        public static void ClaimOptionalProperties()
        {
            //there are several properties that won't be used by the majority of the triggers
            //since the xtriggers themselves are relatively numerous entities, I don't want each one to have a bunch of unused properties
            //so these properties exist as an optional "claimed" ones
            //it adds an overhead when executing, but should go much easier on the memory

            //but actually I'll do that later!!
        }

        public void QuickSpec(string value)
        {
            this.SetId(value);
            SetDefaultValues();
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

            if (MorphEffect != MorphEffectsExtended.GrandEffects)
                GrandEffects = null;

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

        private bool ShouldTriggerInThisMode(TriggerMode mode, bool onAspect, Element element)
        {
            switch (mode)
            {
                case TriggerMode.Always: return true;
                case TriggerMode.TokenOnly: return !onAspect;
                case TriggerMode.AspectOnly: return onAspect;
                default:
                    return onAspect ? element.IsAspect : true;
            }
        }

        public void Execute(Situation situation, Token targetToken, string targetElementId, int targetQuantity, int catalystAmount, bool onAspect)
        {
            Element element = Watchman.Get<Compendium>().GetEntityById<Element>(targetElementId);
            TriggerMode mode = element.RetrieveProperty<TriggerMode>("triggerMode");

            if (!ShouldTriggerInThisMode(mode, onAspect, element))
                return;

            Crossroads.MarkLocalToken(targetToken);

            if (UnityEngine.Random.Range(1, 101) > Chance.value)
                return;

            Sphere coreSphere = targetToken.Sphere;

            if (IgnoreTargetQuantity)
                targetQuantity = 1;
            if (IgnoreCatalystQuantity)
                catalystAmount = 1;

            switch (MorphEffect)
            {
                case MorphEffectsExtended.Transform:
                    if (!onAspect)
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
                    RecipeExecutionBuffer.ScheduleCreation(coreSphere, this.Id, targetQuantity * Level.value * catalystAmount, VFX);
                    break;
                case MorphEffectsExtended.SetMutation:
                    RecipeExecutionBuffer.ScheduleMutation(targetToken, this.Id, Level.value * catalystAmount * targetQuantity, false, VFX);
                    break;
                case MorphEffectsExtended.Mutate:
                    RecipeExecutionBuffer.ScheduleMutation(targetToken, this.Id, Level.value * catalystAmount * targetQuantity, true, VFX);
                    break;
                case MorphEffectsExtended.DeckDraw:
                    Legerdemain.Deal(this.Id, coreSphere, Level.value * catalystAmount * targetQuantity, VFX);
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
                case MorphEffectsExtended.GrandEffects:
                    {
                        foreach (Sphere sphere in GrandEffects.Target.GetSpheresByPath())
                        {
                            GrandEffects.RunGrandEffects(situation, sphere, false);
                            Crossroads.MarkLocalSphere(targetToken.Sphere);
                        }
                        break;
                    }
                default: Birdsong.TweetLoud($"Unknown trigger '{MorphEffect}' for element stack '{targetToken.PayloadEntityId}'"); break;
            }

            Crossroads.UnmarkLocalToken();
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
            List<Token> filteredTokens = tokens.FilterTokens(Filter);

            if (!Limit.isUndefined)
                return filteredTokens.SelectRandom(Limit.value);

            return filteredTokens;
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