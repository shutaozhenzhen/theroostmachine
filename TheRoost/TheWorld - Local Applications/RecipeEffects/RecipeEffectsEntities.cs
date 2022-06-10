using System;
using System.Collections;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Entities;
using SecretHistories.Core;
using SecretHistories.Assets.Scripts.Application.Entities.NullEntities;

using Roost.Twins;
using Roost.Twins.Entities;
using SecretHistories.Spheres;

namespace Roost.World.Recipes.Entities
{
    public class GrandEffects : AbstractEntity<GrandEffects>
    {
        [FucinePathValue(DefaultValue = "")] public FucinePath Target { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> RootEffects { get; set; }
        [FucineDict] public Dictionary<TokenFilterSpec, List<RefMutationEffect>> Mutations { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> Aspects { get; set; }
        [FucineList] public List<string> DeckShuffles { get; set; }
        [FucineDict] public Dictionary<string, FucineExp<int>> DeckEffects { get; set; }
        [FucineDict] public Dictionary<FucineExp<bool>, FucineExp<int>> Effects { get; set; }
        [FucineList] public List<TokenFilterSpec> Decays { get; set; }
        [FucineDict] public List<GrandEffects> DistantEffects { get; set; }
        [FucineDict] public Dictionary<FucinePath, List<TokenFilterSpec>> Movements { get; set; }

        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX DeckEffectsVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX CreateVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardBurn)] public RetirementVFX DestroyVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardLight)] public RetirementVFX DecaysVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX MovementsVFX { get; set; }

        public GrandEffects() { }
        public GrandEffects(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            //reducing amount of entities
            foreach (CachedFucineProperty<GrandEffects> property in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
            {
                object value = property.GetViaFastInvoke(this);
                if ((value as ICollection)?.Count == 0)
                    property.SetViaFastInvoke(this, null);
            }
        }

        public static void RunGrandEffects(GrandEffects grandEffects, Situation situation, Sphere localSphere)
        {
            //even if there are no effects for the recipe, aspect xtriggering should happen
            if (grandEffects == null)
            {
                Crossroads.MarkLocalSphere(localSphere);
                RunXTriggers(null, localSphere, situation, true);
            }
            else
                grandEffects.Run(situation, localSphere, true);

            RecipeExecutionBuffer.ApplyVFX();
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
            RunDistantEffects(situation);
            RunMovements(localSphere);
        }

        private void RunRootEffects()
        {
            if (RootEffects == null)
                return;

            Dictionary<string, int> scheduledMutations = new Dictionary<string, int>();

            foreach (string elementId in RootEffects.Keys)
                scheduledMutations.Add(elementId, RootEffects[elementId].value);

            foreach (string elementId in RootEffects.Keys)
                FucineRoot.Get().SetMutation(elementId, scheduledMutations[elementId], true);
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
            allCatalystsInSphere.Clear();

            if (Aspects != null)
                foreach (KeyValuePair<string, FucineExp<int>> catalyst in Aspects)
                    allCatalystsInSphere[catalyst.Key] = catalyst.Value.value;
            if (catalystFromElements)
                allCatalystsInSphere.ApplyMutations(sphere.GetTotalAspects());

            if (allCatalystsInSphere.Count == 0)
                return;

            Dictionary<string, List<RefMorphDetails>> xtriggers;
            Compendium compendium = Watchman.Get<Compendium>();
            foreach (Token token in sphere.GetElementTokens())
            {
                if (token.IsValidElementStack() == false)
                    continue;

                xtriggers = compendium.GetEntityById<Element>(token.PayloadEntityId).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;
                if (xtriggers != null)
                    foreach (KeyValuePair<string, int> catalyst in allCatalystsInSphere)
                        if (xtriggers.ContainsKey(catalyst.Key)) foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                morphDetails.Execute(situation, token, token.PayloadEntityId, token.Quantity, catalyst.Value, true);

                AspectsDictionary tokenAspects = new AspectsDictionary(Machine.GetEntity<Element>(token.PayloadEntityId).Aspects);
                tokenAspects.ApplyMutations(token.GetCurrentMutations());

                foreach (KeyValuePair<string, int> aspect in tokenAspects)
                {
                    xtriggers = compendium.GetEntityById<Element>(aspect.Key).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;
                    if (xtriggers != null)
                        foreach (KeyValuePair<string, int> catalyst in allCatalystsInSphere)
                            if (xtriggers.ContainsKey(catalyst.Key)) foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                    morphDetails.Execute(situation, token, aspect.Key, aspect.Value, catalyst.Value, false);
                }
            }

            Crossroads.UnmarkLocalToken();
            RecipeExecutionBuffer.ApplyAllEffects();
        }

        private void RunDeckShuffles()
        {
            if (DeckShuffles == null)
                return;

            foreach (string deckId in DeckShuffles)
                Legerdemain.RenewDeck(deckId);
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

            List<Token> allTokens = sphere.GetElementTokens();
            foreach (FucineExp<bool> filter in Effects.Keys)
            {
                int level = Effects[filter].value;
                if (level < 0)
                {
                    List<Token> filteredTokens = allTokens.FilterTokens(filter);
                    while (level < 0 && filteredTokens.Count > 0)
                    {
                        RecipeExecutionBuffer.ScheduleQuantityChange(filteredTokens[UnityEngine.Random.Range(0, filteredTokens.Count)], -1, DestroyVFX);
                        level++;
                    }
                }
                else
                    RecipeExecutionBuffer.ScheduleCreation(sphere, filter.formula, level, CreateVFX);
            }

            RecipeExecutionBuffer.ApplyQuantityChanges();
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

        private void RunDistantEffects(Situation situation)
        {
            if (DistantEffects == null)
                return;

            foreach (GrandEffects sphereEffect in DistantEffects)
            {
                List<Sphere> targetSpheres = Crossroads.GetSpheresByPath(sphereEffect.Target);
                foreach (Sphere sphere in targetSpheres)
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
    }

    public class RefMutationEffect : AbstractEntity<RefMutationEffect>, IQuickSpecEntity, ICustomSpecEntity
    {
        [FucineEverValue(ValidateAsElementId = true, DefaultValue = null)] public string Mutate { get; set; }
        [FucineEverValue("1")] public FucineExp<int> Level { get; set; }
        [FucineEverValue(false)] public bool Additive { get; set; }
        [FucineEverValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX VFX { get; set; }

        public RefMutationEffect() { }
        public RefMutationEffect(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { this.SetId(Mutate); }

        public void CustomSpec(Hashtable data)
        {
            if (Mutate == null)
            {
                foreach (object key in UnknownProperties.Keys)
                {
                    this.Mutate = key.ToString();
                    this.Level = new FucineExp<int>(UnknownProperties[key].ToString());
                    break;
                }
                UnknownProperties.Remove(Mutate);
            }
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
    public class RefMorphDetails : AbstractEntity<RefMorphDetails>, IQuickSpecEntity, ICustomSpecEntity
    {
        [FucineValue(DefaultValue = MorphEffectsExtended.Transform)] public MorphEffectsExtended MorphEffect { get; set; }
        [FucineConstruct("1")] public FucineExp<int> Level { get; set; }
        [FucineConstruct("100")] public FucineExp<int> Chance { get; set; }

        [FucineEverValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX VFX { get; set; }
        [FucineValue(false)] public bool IgnoreAmount { get; set; }
        [FucineValue(false)] public bool IgnoreCatalystAmount { get; set; }

        [FucineSubEntity(typeof(LinkedRecipeDetails))] public LinkedRecipeDetails Induction { get; set; }

        public void QuickSpec(string value)
        {
            this.SetId(value);
            Chance = new FucineExp<int>("100");
            MorphEffect = MorphEffectsExtended.Transform;
            Level = new FucineExp<int>("1");
            IgnoreAmount = false;
            IgnoreCatalystAmount = false;
            Induction = null;
            VFX = RetirementVFX.CardTransformWhite;
        }

        public void CustomSpec(Hashtable data)
        {
            if (Id == null)
            {
                foreach (object key in UnknownProperties.Keys)
                {
                    this.SetId(key.ToString());
                    this.Level = new FucineExp<int>(UnknownProperties[key].ToString());
                    break;
                }
                UnknownProperties.Remove(this.Id);
            }
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
                    Watchman.Get<Compendium>().SupplyElementIdsForValidation(this.Id);
                    break;
                case MorphEffectsExtended.Link:
                case MorphEffectsExtended.Induce:
                    if (string.IsNullOrEmpty(Induction.Id))
                        Induction.SetId(this.Id);
                    break;
                case MorphEffectsExtended.DeckDraw:
                case MorphEffectsExtended.DeckShuffle:

                    if (Machine.GetEntity<Recipe>(this.Id) == null)
                        Birdsong.Tweet($"Unknown deck id '{this.Id}' in Morph Effects");
                    break;
                default:
                    break;
            }

            if (MorphEffect != MorphEffectsExtended.Induce)
                Induction = null;
        }

        public void Execute(Situation situation, Token targetToken, string targetElementId, int targetElementAmount, int catalystAmount, bool onToken)
        {
            Crossroads.MarkLocalToken(targetToken);

            if (Chance.value < UnityEngine.Random.Range(1, 101))
                return;

            if (IgnoreAmount)
                targetElementAmount = 1;
            if (IgnoreCatalystAmount)
                catalystAmount = 1;

            switch (MorphEffect)
            {
                case MorphEffectsExtended.Transform:
                    if (onToken)
                        RecipeExecutionBuffer.ScheduleTransformation(targetToken, this.Id, VFX);
                    else
                    {
                        RecipeExecutionBuffer.ScheduleMutation(targetToken, targetElementId, targetElementAmount, true, RetirementVFX.None);
                        RecipeExecutionBuffer.ScheduleMutation(targetToken, targetElementId, -targetElementAmount, true, VFX);
                    }
                    break;
                case MorphEffectsExtended.Spawn:
                    RecipeExecutionBuffer.ScheduleCreation(targetToken.Sphere, this.Id, targetElementAmount * Level.value * catalystAmount, VFX);
                    break;
                case MorphEffectsExtended.SetMutation:
                    RecipeExecutionBuffer.ScheduleMutation(targetToken, this.Id, Level.value * catalystAmount * targetElementAmount, false, VFX);
                    break;
                case MorphEffectsExtended.Mutate:
                    RecipeExecutionBuffer.ScheduleMutation(targetToken, this.Id, Level.value * catalystAmount * targetElementAmount, true, VFX);
                    break;
                case MorphEffectsExtended.DeckDraw:
                    Legerdemain.Deal(this.Id, targetToken.Sphere, Level.value * catalystAmount * targetElementAmount);
                    break;
                case MorphEffectsExtended.DeckShuffle:
                    RecipeExecutionBuffer.ScheduleDeckRenew(this.Id);
                    break;
                case MorphEffectsExtended.Destroy:
                    if (onToken)
                        RecipeExecutionBuffer.ScheduleRetirement(targetToken, VFX);
                    else
                        RecipeExecutionBuffer.ScheduleMutation(targetToken, targetElementId, -targetElementAmount, true, RetirementVFX.None);
                    break;
                case MorphEffectsExtended.Induce:
                    RecipeExecutionBuffer.ScheduleInduction(situation, Induction);
                    break;
                case MorphEffectsExtended.Link: Machine.PushXtriggerLink(this.Id, Level.value); break;
                default: Birdsong.Tweet($"Unknown trigger '{MorphEffect}' for element stack '{targetToken.PayloadEntityId}'"); break;
            }
        }
    }

    public class TokenFilterSpec : AbstractEntity<TokenFilterSpec>, IQuickSpecEntity
    {
        [FucineConstruct] public FucineExp<bool> Filter { get; set; }
        [FucineConstruct(FucineExp<int>.UNDEFINED)] public FucineExp<int> Limit { get; set; } //unlimited by default

        public TokenFilterSpec() { }
        public TokenFilterSpec(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public List<Token> GetTokens(List<Token> tokens)
        {
            //NB - intrusive, splits tokens
            return tokens.FilterTokens(Filter).ShuffleTokens().LimitTokens(Limit.value);
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
    }
}