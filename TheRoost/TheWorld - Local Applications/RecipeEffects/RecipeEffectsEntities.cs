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
using SecretHistories.Assets.Scripts.Application.Entities.NullEntities;

using Roost.Twins;
using Roost.Twins.Entities;

namespace Roost.World.Recipes.Entities
{
    public class GrandEffects : AbstractEntity<GrandEffects>
    {
        [FucinePathValue] public FucinePath Target { get; set; }
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
            }
            else
                grandEffects.Run(situation, localSphere, true);

            RecipeExecutionBuffer.ApplyVFX();
            RecipeExecutionBuffer.ApplyInductions();
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
            RunInductions(situation);
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
                        if (xtriggers.ContainsKey(catalyst.Key))
                            foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                morphDetails.Execute(situation, token, token.PayloadEntityId, token.Quantity, catalyst.Value, true);

                AspectsDictionary tokenAspects = new AspectsDictionary(Machine.GetEntity<Element>(token.PayloadEntityId).Aspects);
                tokenAspects.ApplyMutations(token.GetCurrentMutations());

                foreach (KeyValuePair<string, int> aspect in tokenAspects)
                {
                    xtriggers = compendium.GetEntityById<Element>(aspect.Key).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;
                    if (xtriggers != null)
                        foreach (KeyValuePair<string, int> catalyst in allCatalystsInSphere)
                            if (xtriggers.ContainsKey(catalyst.Key))
                                foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                    morphDetails.Execute(situation, token, aspect.Key, aspect.Value, catalyst.Value, false);
                }
            }

            allCatalystsInSphere.Clear();
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
                    List<Token> affectedTokens = allTokens.FilterTokens(filter).SelectRandom(Math.Abs(level));
                    foreach (Token token in affectedTokens)
                        RecipeExecutionBuffer.ScheduleRetirement(token, DestroyVFX);
                }
                else
                    RecipeExecutionBuffer.ScheduleCreation(sphere, filter.formula, level, CreateVFX);
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

        private void RunInductions(Situation situation)
        {
            if (Induces == null)
                return;

            foreach (LinkedRecipeDetails link in Induces)
                RecipeExecutionBuffer.ScheduleInduction(situation, link);
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
                distantEffect.SetId(this.Id + "_distant_" + i++);
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
                    if (Watchman.Get<Compendium>().GetEntityById<Element>(key.ToString()) != null)
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
        [FucineValue(false)] public bool IgnoreAmount { get; set; }
        [FucineValue(false)] public bool IgnoreCatalystAmount { get; set; }

        private LinkedRecipeDetails Induction { get; set; }
        [FucinePathValue("")] public FucinePath ToPath { get; set; }
        [FucineSubEntity(typeof(Expulsion))] public Expulsion Expulsion { get; set; }

        public void QuickSpec(string value)
        {
            this.SetId(value);
            Chance = new FucineExp<int>("100");
            MorphEffect = MorphEffectsExtended.Transform;
            Level = new FucineExp<int>("1");
            IgnoreAmount = false;
            IgnoreCatalystAmount = false;
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
                        Induction.SetProperty("chance", new FucineExp<int>("100")); //xtriggers already have chance
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

        public void Execute(Situation situation, Token targetToken, string targetElementId, int targetElementAmount, int catalystAmount, bool onToken)
        {
            Crossroads.MarkLocalToken(targetToken);

            if (UnityEngine.Random.Range(1, 101) > Chance.value)
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