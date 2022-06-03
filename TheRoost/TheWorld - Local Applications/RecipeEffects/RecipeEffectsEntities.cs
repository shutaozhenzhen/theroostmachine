
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

using UnityEngine;

namespace Roost.World.Recipes.Entities
{
    public class GrandEffects : AbstractEntity<GrandEffects>
    {
        [FucineDict] public Dictionary<string, Funcine<int>> RootEffects { get; set; }
        [FucineDict] public Dictionary<FucinePath, TokenFilterSpec> Movements { get; set; }
        [FucineDict] public Dictionary<FucinePath, GrandEffects> DistantEffects { get; set; }
        [FucineDict] public Dictionary<Funcine<bool>, List<RefMutationEffect>> Mutations { get; set; }
        [FucineDict] public Dictionary<string, Funcine<int>> Aspects { get; set; }

        [FucineDict] public Dictionary<Funcine<bool>, List<RefMorphDetails>> XTriggers { get; set; }

        [FucineList] public List<string> DeckShuffles { get; set; }
        [FucineDict] public Dictionary<string, Funcine<int>> DeckEffects { get; set; }

        [FucineDict] public Dictionary<Funcine<bool>, Funcine<int>> Effects { get; set; }

        [FucineList] public List<TokenFilterSpec> Decays { get; set; }


        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX DeckEffectsVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX EffectsVFX { get; set; }
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

        public void Run(Situation situation, Sphere localSphere)
        {
            Roost.Twins.Crossroads.MarkLocalSphere(localSphere);

            RunRootEffects();
            RunMovements(localSphere);
            RunDistantEffects(situation);
            RunMutations(localSphere);
            RunCoreXTriggers(localSphere, situation, Aspects);
            RunTargetedXTriggers(localSphere, situation);
            RunDeckShuffles();
            RunDeckEffects(localSphere);
            RunEffects(localSphere);
            RunDecays(localSphere);
        }

        public void RunRootEffects()
        {
            if (RootEffects == null)
                return;

            Dictionary<string, int> scheduledMutations = new Dictionary<string, int>();

            foreach (string elementId in RootEffects.Keys)
                scheduledMutations.Add(elementId, RootEffects[elementId].value);

            foreach (string elementId in RootEffects.Keys)
                FucineRoot.Get().SetMutation(elementId, scheduledMutations[elementId], true);
        }

        public void RunMovements(Sphere fromSphere)
        {
            if (Movements == null)
                return;

            List<Token> tokens = fromSphere.GetElementTokens();

            foreach (FucinePath fucinePath in Movements.Keys)
            {
                List<Sphere> targetSpheres = Crossroads.GetSpheresByPath(fucinePath);
                if (targetSpheres.Count == 0)
                    continue;

                foreach (Token token in Movements[fucinePath].FilterTokens(tokens))
                    RecipeExecutionBuffer.ScheduleMovement(token, targetSpheres[UnityEngine.Random.Range(0, targetSpheres.Count)], MovementsVFX);
            }

            RecipeExecutionBuffer.ApplyMovements();
        }

        public void RunDistantEffects(Situation situation)
        {
            if (DistantEffects == null)
                return;

            foreach (KeyValuePair<FucinePath, GrandEffects> sphereEffect in DistantEffects)
            {
                List<Sphere> targetSpheres = Crossroads.GetSpheresByPath(sphereEffect.Key);
                foreach (Sphere sphere in targetSpheres)
                    sphereEffect.Value.Run(situation, sphere);
            }
        }

        public void RunMutations(Sphere sphere)
        {
            if (Mutations == null)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (Funcine<bool> filter in Mutations.Keys)
            {
                List<Token> targets = tokens.FilterTokens(filter);

                if (targets.Count > 0)
                    foreach (RefMutationEffect mutationEffect in Mutations[filter])
                        RecipeExecutionBuffer.ScheduleMutation(targets, mutationEffect.Mutate, mutationEffect.Level.value, mutationEffect.Additive, mutationEffect.VFX);
            }

            RecipeExecutionBuffer.ApplyMutations();
        }

        private static readonly AspectsDictionary allCatalystsInSphere = new AspectsDictionary();
        public static void RunCoreXTriggers(Sphere sphere, Situation situation, Dictionary<string, Funcine<int>> Aspects)
        {
            allCatalystsInSphere.Clear();

            if (Aspects != null)
                foreach (KeyValuePair<string, Funcine<int>> catalyst in Aspects)
                    allCatalystsInSphere[catalyst.Key] = catalyst.Value.value;
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
            RecipeExecutionBuffer.ApplyAll();
        }

        public void RunTargetedXTriggers(Sphere sphere, Situation situation)
        {
            if (XTriggers == null)
                return;

            List<Token> allTokens = sphere.GetElementTokens();
            foreach (Funcine<bool> filter in XTriggers.Keys)
            {
                List<Token> filteredTokens = allTokens.FilterTokens(filter);

                foreach (Token token in filteredTokens)
                    foreach (RefMorphDetails morphDetails in XTriggers[filter])
                        morphDetails.Execute(situation, token, token.PayloadEntityId, 1, 1, true);
            }

            Crossroads.UnmarkLocalToken();
            RecipeExecutionBuffer.ApplyAll();
        }

        public void RunDeckShuffles()
        {
            if (DeckShuffles == null)
                return;

            foreach (string deckId in DeckShuffles)
                Legerdemain.RenewDeck(deckId);
        }

        private void RunDeckEffects(Sphere toSphere)
        {
            if (DeckEffects == null)
                return;

            foreach (string deckId in DeckEffects.Keys)
                Legerdemain.Deal(deckId, toSphere, DeckEffects[deckId].value);

            RecipeExecutionBuffer.ApplyMovements();
            RecipeExecutionBuffer.ApplyRenews();
        }

        public void RunEffects(Sphere sphere)
        {
            if (Effects == null)
                return;

            List<Token> allTokens = sphere.GetElementTokens();
            foreach (Funcine<bool> filter in Effects.Keys)
            {
                int level = Effects[filter].value;
                if (level < 0)
                {
                    List<Token> filteredTokens = allTokens.FilterTokens(filter);
                    while (level < 0 && filteredTokens.Count > 0)
                    {
                        RecipeExecutionBuffer.ScheduleRetirement(filteredTokens[UnityEngine.Random.Range(0, filteredTokens.Count)], EffectsVFX);
                        level++;
                    }
                }
                else
                    RecipeExecutionBuffer.ScheduleCreation(sphere, filter.formula, level, EffectsVFX);
            }

            RecipeExecutionBuffer.ApplyRetirements();
            RecipeExecutionBuffer.ApplyCreations();
        }

        public void RunDecays(Sphere sphere)
        {
            if (Decays == null)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (TokenFilterSpec tokenFilterSpec in Decays)
                foreach (Token token in tokenFilterSpec.FilterTokens(tokens))
                    RecipeExecutionBuffer.ScheduleDecay(token, DecaysVFX);

            RecipeExecutionBuffer.ApplyRetirements();
            RecipeExecutionBuffer.ApplyTransformations();
        }
    }

    public class RefMutationEffect : AbstractEntity<RefMutationEffect>, ICustomSpecEntity
    {
        [FucineEverValue(ValidateAsElementId = true, DefaultValue = null)] public string Mutate { get; set; }
        [FucineEverValue("1")] public Funcine<int> Level { get; set; }
        [FucineEverValue(false)] public bool Additive { get; set; }
        [FucineEverValue(DefaultValue = RetirementVFX.CardLight)] public RetirementVFX VFX { get; set; }

        public RefMutationEffect(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public void CustomSpec(Hashtable data)
        {
            if (Mutate == null)
            {
                foreach (object key in UnknownProperties.Keys)
                {
                    this.Mutate = key.ToString();
                    this.Level = new Funcine<int>(UnknownProperties[key].ToString());
                    break;
                }
                UnknownProperties.Remove(Mutate);
            }
        }
    }

    public enum MorphEffectsExtended { Transform, Spawn, MutateSet, Mutate, DeckDraw, DeckShuffle, Destroy, Decay, Induce, Link }
    public class RefMorphDetails : AbstractEntity<RefMorphDetails>, IQuickSpecEntity, ICustomSpecEntity
    {
        [FucineValue(DefaultValue = MorphEffectsExtended.Transform)] public MorphEffectsExtended MorphEffect { get; set; }
        [FucineConstruct("1")] public Funcine<int> Level { get; set; }
        [FucineConstruct("100")] public Funcine<int> Chance { get; set; }
        [FucineValue(false)] public bool IgnoreAmount { get; set; }
        [FucineValue(false)] public bool IgnoreCatalystAmount { get; set; }

        [FucineSubEntity(typeof(Expulsion))] public Expulsion Expulsion { get; set; }
        [FucineEverValue(DefaultValue = RetirementVFX.CardBurn)] public RetirementVFX VFX { get; set; }

        public void QuickSpec(string value)
        {
            this.SetId(value);
            this.Chance = new Funcine<int>("100");
            this.MorphEffect = MorphEffectsExtended.Transform;
            this.Level = new Funcine<int>("1");
            this.IgnoreAmount = false;
            this.IgnoreCatalystAmount = false;
            Expulsion = null;
            VFX = RetirementVFX.CardTransformWhite;
        }

        public void CustomSpec(Hashtable data)
        {
            if (Id == null)
            {
                foreach (object key in UnknownProperties.Keys)
                {
                    this.SetId(key.ToString());
                    this.Level = new Funcine<int>(UnknownProperties[key].ToString());
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
                case MorphEffectsExtended.MutateSet:
                    Watchman.Get<Compendium>().SupplyElementIdsForValidation(this.Id);
                    break;
                case MorphEffectsExtended.Link:
                case MorphEffectsExtended.Induce:
                    if (Machine.GetEntity<Recipe>(this.Id) == null)
                        Birdsong.Tweet($"Unknown recipe id '{this.Id}' in Morph Effects");
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
                Expulsion = null;
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
                case MorphEffectsExtended.MutateSet:
                    RecipeExecutionBuffer.ScheduleMutation(targetToken, this.Id, Level.value * catalystAmount * targetElementAmount, false, VFX);
                    break;
                case MorphEffectsExtended.Mutate:
                    RecipeExecutionBuffer.ScheduleMutation(targetToken, this.Id, Level.value * catalystAmount * targetElementAmount, true, VFX);
                    break;
                case MorphEffectsExtended.DeckDraw:
                    Legerdemain.Deal(this.Id, targetToken.Sphere, Level.value * catalystAmount * targetElementAmount);
                    break;
                case MorphEffectsExtended.Destroy:
                    if (onToken)
                        RecipeExecutionBuffer.ScheduleRetirement(targetToken, VFX);
                    else
                        RecipeExecutionBuffer.ScheduleMutation(targetToken, targetElementId, -targetElementAmount, true, RetirementVFX.None);
                    break;
                case MorphEffectsExtended.Induce:
                    Recipe recipeToInduce = Watchman.Get<Compendium>().GetEntityById<Recipe>(this.Id);
                    for (int i = Level.value; i > 0; i--)
                        Roost.World.Recipes.RecipeLinkMaster.SpawnNewSituation(situation, recipeToInduce, Expulsion, FucinePath.Current());
                    break;
                case MorphEffectsExtended.Link: Machine.PushXtriggerLink(this.Id, Level.value); break;
                default: Birdsong.Tweet($"Unknown trigger '{MorphEffect}' for element stack '{targetToken.PayloadEntityId}'"); break;
            }
        }
    }

    public class TokenFilterSpec : AbstractEntity<TokenFilterSpec>, IQuickSpecEntity
    {
        [FucineList] public Funcine<bool> Filter { get; set; }
        [FucineConstruct(Funcine<int>.undefined)] public Funcine<int> Limit { get; set; } //unlimited by default

        public TokenFilterSpec() { }
        public TokenFilterSpec(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public List<Token> FilterTokens(List<Token> tokens)
        {
            //NB - intrusive, splits tokens
            List<Token> filteredTokens = tokens.FilterTokens(Filter);
            if (filteredTokens.Count == 0)
                return new List<Token>();

            int tokensToMove = Limit.isUndefined ? int.MaxValue : Limit.value;
            List<Token> result = new List<Token>();
            foreach (Token token in filteredTokens)
            {
                if (token.Quantity > tokensToMove)
                {
                    token.CalveToken(token.Quantity - tokensToMove, RecipeExecutionBuffer.situationEffectContext);
                    tokensToMove = 0;
                }
                else
                    tokensToMove -= token.Quantity;

                result.Add(token);
                if (tokensToMove <= 0)
                    break;
            }

            return result;
        }

        public void QuickSpec(string data)
        {
            try
            {
                Filter = new Funcine<bool>(data);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}