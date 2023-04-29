using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Entities;
using SecretHistories.Spheres;
using SecretHistories.Logic;

using Roost.Twins;
using Roost.Twins.Entities;

namespace Roost.World.Recipes.Entities
{
    public enum MorphEffectsExtended
    {
        Transform, Spawn, Mutate, Quantity, //vanilla
        SetMutation, DeckDraw, DeckShuffle,  //makes sense, right?
        Destroy, Decay, //destructive forces
        LeverFuture, LeverPast, TimeSpend, TimeSet, //exotique
        GrandEffects, //big boy
        Induce, Link //wot
    }

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

        public const string TRIGGER_MODE = "triggerMode";
        public static void Enact()
        {
            Machine.ClaimProperty<Element, Dictionary<string, List<RefMorphDetails>>>("xtriggers");

            Machine.ClaimProperty<Element, TriggerMode>(TRIGGER_MODE, false, TriggerMode.Default);
            AtTimeOfPower.OnPostImportElement.Schedule<Element, ContentImportLog, Compendium>(PostImportForTheNewXtriggers, PatchType.Postfix);

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
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium compendium)
        {
            switch (MorphEffect)
            {

                //mundane, id is a element
                case MorphEffectsExtended.Transform:
                case MorphEffectsExtended.Spawn:
                case MorphEffectsExtended.Mutate:
                case MorphEffectsExtended.SetMutation:
                    if (Id != null)
                        foreach (object key in UnknownProperties.Keys)
                            if (compendium.GetEntityById<Element>(key.ToString())?.IsValid() == true)
                            {
                                SetId(key.ToString());
                                Level = new FucineExp<int>(UnknownProperties[key].ToString());
                                break;
                            }

                    if (Id == null)
                        log.LogWarning($"ID FOR XTRIGGER '{this}' ISN'T SET");
                    else
                        compendium.SupplyIdForValidation(typeof(Element), Id);

                    break;

                //links, id is a recipe
                case MorphEffectsExtended.Link:
                case MorphEffectsExtended.Induce:
                    if (Id != null)
                        foreach (object key in UnknownProperties.Keys)
                            if (compendium.GetEntityById<Recipe>(key.ToString())?.IsValid() == true)
                            {
                                SetId(key.ToString());
                                Level = new FucineExp<int>(UnknownProperties[key].ToString());
                                break;
                            }

                    if (Id == null)
                    {
                        log.LogWarning($"ID FOR XTRIGGER '{this}' ISN'T SET");
                    }
                    else
                    {
                        compendium.SupplyIdForValidation(typeof(Recipe), Id);

                        if (MorphEffect == MorphEffectsExtended.Induce)
                        {
                            Induction = new LinkedRecipeDetails();
                            Induction.SetDefaultValues();
                            Induction.SetId(Id);
                            Induction.ToPath = ToPath;
                            Induction.Expulsion = Expulsion;
                        }
                    }

                    break;

                //id is a deck
                case MorphEffectsExtended.DeckDraw:
                case MorphEffectsExtended.DeckShuffle:
                    if (Id != null)
                        foreach (object key in UnknownProperties.Keys)
                            if (compendium.GetEntityById<DeckSpec>(key.ToString()) != null)
                            {
                                this.SetId(key.ToString());
                                this.Level = new FucineExp<int>(UnknownProperties[key].ToString());
                                break;
                            }

                    if (Id == null)
                        log.LogWarning($"ID FOR XTRIGGER '{this}' ISN'T SET");
                    else
                        compendium.SupplyIdForValidation(typeof(DeckSpec), Id);

                    break;

                //doesn't use Id
                case MorphEffectsExtended.Quantity:
                case MorphEffectsExtended.Decay:
                case MorphEffectsExtended.Destroy:
                case MorphEffectsExtended.GrandEffects:
                case MorphEffectsExtended.TimeSpend:
                case MorphEffectsExtended.TimeSet:
                case MorphEffectsExtended.LeverFuture:
                case MorphEffectsExtended.LeverPast:
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
        }

        private bool ShouldReactInThisMode(string reactingElementId, bool aspectReacts)
        {
            Element affectedElement = Watchman.Get<Compendium>().GetEntityById<Element>(reactingElementId);
            TriggerMode mode = affectedElement.RetrieveProperty<TriggerMode>(TRIGGER_MODE);

            switch (mode)
            {
                case TriggerMode.Always: return true;
                case TriggerMode.TokenOnly: return !aspectReacts;
                case TriggerMode.AspectOnly: return aspectReacts;
                case TriggerMode.Default: return aspectReacts ? affectedElement.IsAspect : true;
                default: return false;
            }
        }

        public void Execute(Situation situation, Token reactingToken, string reactingElementId, int reactingElementQuantity, int catalystQuantity, bool aspectReacts)
        {
            if (!ShouldReactInThisMode(reactingElementId, aspectReacts))
                return;

            Crossroads.MarkLocalToken(reactingToken);

            if (UnityEngine.Random.Range(1, 101) > Chance.value)
            {
                Crossroads.UnmarkLocalToken();
                return;
            }

            reactingElementQuantity = IgnoreTargetQuantity ? 1 : reactingElementQuantity;
            catalystQuantity = IgnoreCatalystQuantity ? 1 : catalystQuantity;

            switch (MorphEffect)
            {
                case MorphEffectsExtended.Transform:
                    if (!aspectReacts)
                        RecipeExecutionBuffer.ScheduleTransformation(reactingToken, this.Id, VFX);
                    else
                    {
                        //if there are several chanced transforms on a single aspect, we don't want mutations to happen several times
                        //(it is TRANSFORM, previous aspect must end up being a single thing, not multiply into several different things)
                        //thus we're making use of "mutation groups" - if one transform did trigger and registered a mutation, then other won't happen
                        RecipeExecutionBuffer.ScheduleUniqueMutation(reactingToken, reactingElementId, -reactingElementQuantity, true, RetirementVFX.None, "-" + reactingToken.PayloadEntityId + reactingElementId);
                        RecipeExecutionBuffer.ScheduleUniqueMutation(reactingToken, this.Id, reactingElementQuantity, true, VFX, reactingToken.PayloadEntityId + reactingElementId);
                    }
                    break;
                case MorphEffectsExtended.Spawn:
                    RecipeExecutionBuffer.ScheduleSpawn(reactingToken.Sphere, this.Id, reactingElementQuantity * Level.value * catalystQuantity, VFX);
                    break;
                case MorphEffectsExtended.SetMutation:
                    RecipeExecutionBuffer.ScheduleMutation(reactingToken, this.Id, Level.value * catalystQuantity * reactingElementQuantity, false, VFX);
                    break;
                case MorphEffectsExtended.Mutate:
                    RecipeExecutionBuffer.ScheduleMutation(reactingToken, this.Id, Level.value * catalystQuantity * reactingElementQuantity, true, VFX);
                    break;

                case MorphEffectsExtended.Quantity:
                    RecipeExecutionBuffer.ScheduleQuantityChange(reactingToken, Level.value * catalystQuantity, VFX);
                    break;

                case MorphEffectsExtended.DeckDraw:
                    Legerdemain.Deal(this.Id, reactingToken.Sphere, Level.value * catalystQuantity * reactingElementQuantity, VFX);
                    break;
                case MorphEffectsExtended.DeckShuffle:
                    RecipeExecutionBuffer.ScheduleDeckRenew(this.Id);
                    break;

                case MorphEffectsExtended.Destroy:
                    RecipeExecutionBuffer.ScheduleRetirement(reactingToken, VFX);
                    break;
                case MorphEffectsExtended.Decay:
                    RecipeExecutionBuffer.ScheduleDecay(reactingToken, VFX);
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
                            Crossroads.MarkLocalSphere(reactingToken.Sphere);
                        }
                        break;
                    }

                case MorphEffectsExtended.TimeSpend:
                    {
                        ElementStack stack = reactingToken.Payload as ElementStack;
                        Timeshadow timeshadow = stack.GetTimeshadow();

                        float timeInSeconds = Level.value / 1000;
                        timeshadow.SpendTime(timeInSeconds);

                        if (timeshadow.LifetimeRemaining <= 0)
                            RecipeExecutionBuffer.ScheduleDecay(reactingToken, VFX);
                        else
                            RecipeExecutionBuffer.ScheduleVFX(reactingToken, VFX);
                    }

                    break;

                case MorphEffectsExtended.TimeSet:
                    {
                        ElementStack stack = reactingToken.Payload as ElementStack;
                        Timeshadow timeshadow = stack.GetTimeshadow();

                        float timeInSeconds = Level.value / 1000;
                        timeshadow.SpendTime(timeshadow.LifetimeRemaining - timeInSeconds);

                        if (timeshadow.LifetimeRemaining <= 0)
                            RecipeExecutionBuffer.ScheduleDecay(reactingToken, VFX);
                        else
                            RecipeExecutionBuffer.ScheduleVFX(reactingToken, VFX);
                    }

                    break;

                case MorphEffectsExtended.LeverFuture:
                    Elegiast.Scribe.SetLeverForNextPlaythrough(this.Id, reactingToken.PayloadEntityId);
                    break;

                case MorphEffectsExtended.LeverPast:
                    Elegiast.Scribe.SetLeverForCurrentPlaythrough(this.Id, reactingToken.PayloadEntityId);
                    break;

                default:
                    Birdsong.TweetLoud($"Unknown trigger '{MorphEffect}' for element stack '{reactingToken.PayloadEntityId}'");
                    break;
            }

            Crossroads.UnmarkLocalToken();
        }

        public override string ToString()
        {
            return $"{_container} {MorphEffect} {Id}";
        }

        //Element.OnPostImport
        private static void PostImportForTheNewXtriggers(Element __instance, ContentImportLog log, Compendium populatedCompendium)
        {
            Dictionary<string, List<RefMorphDetails>> xtriggers = __instance.RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;
            if (xtriggers != null)
            {
                foreach (string catalyst in xtriggers.Keys)
                    foreach (RefMorphDetails morphDetails in xtriggers[catalyst])
                        morphDetails.OnPostImport(log, populatedCompendium);
            }
        }
    }
}