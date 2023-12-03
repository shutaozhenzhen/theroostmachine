using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Entities;
using SecretHistories.Logic;
using SecretHistories.Meta;

using Random = UnityEngine.Random;

using Roost.Twins;
using Roost.Twins.Entities;


namespace Roost.World.Recipes.Entities
{
    public enum MorphEffectsExtended
    {
        Transform, Spawn, Mutate, Quantity, //vanilla
        SetMutation, DeckDraw, DeckShuffle, Recipe, //makes sense, right?
        Destroy, Decay, //destructive forces
        LeverFuture, LeverPast, TimeSpend, TimeSet, Trigger, Redirect, //exotique
        Induce, Link, Move, //wot
        Apply, Break, CustomOp //wotter
    }

    public class RefMorphDetails : AbstractEntity<RefMorphDetails>, IQuickSpecEntity
    {
        [FucineValue(DefaultValue = MorphEffectsExtended.Transform)] public MorphEffectsExtended MorphEffect { get; set; }
        [FucineConstruct("1")] public FucineExp<int> Level { get; set; }
        [FucineConstruct("100")] public FucineExp<int> Chance { get; set; }

        [FucineEverValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX VFX { get; set; }

        [FucineValue(false)] public bool UseMyQuantity { get; set; }
        [FucineValue(false)] public bool UseCatalystQuantity { get; set; }
        [FucineNullable] public bool? UseTokenId { get; set; }

        private LinkedRecipeDetails Induction { get; set; }
        [FucinePathValue(DefaultValue = null)] public FucinePath ToPath { get; set; }
        [FucineSubEntity] public Expulsion Expulsion { get; set; }

        public const string TRIGGER_MODE = "triggerMode";
        public static void Enact()
        {
            Machine.ClaimProperty<Element, Dictionary<string, List<RefMorphDetails>>>("xtriggers");
            AtTimeOfPower.OnPostImportElement.Schedule<Element, ContentImportLog, Compendium>(PostImportForTheNewXtriggers, PatchType.Postfix);

            //there are several properties that won't be used by the majority of the triggers
            //since the xtriggers themselves are relatively numerous entities, I don't want each one to have a bunch of unused properties
            //so these properties exist as an optional "claimed" ones
            //it adds an overhead when executing, but should go much easier on the memory

            //but actually I'll do that later!!

            Machine.Patch<ElementsMalleary>(nameof(ElementsMalleary.CrossTrigger),
                prefix: typeof(RefMorphDetails).GetMethodInvariant(nameof(RefMorphDetails.CrossTriggerInMalleary)));
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
                case MorphEffectsExtended.Trigger:
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
                case MorphEffectsExtended.Recipe:
                    if (MorphEffect == MorphEffectsExtended.Recipe)
                        if (UnknownProperties.ContainsKey("recipe")
                        && (UnknownProperties["recipe"] is EntityData recipeData))
                        {
                            Recipe recipe = new Recipe(recipeData, log);
                            int n = 0;
                            do
                            {
                                Id = $"recipe.{n}.{this._container}";
                                recipe.SetId(Id);
                                n++;
                            }
                            while (!compendium.TryAddEntity(recipe));

                            UnknownProperties.Remove("recipe");
                        }

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

                case MorphEffectsExtended.Redirect:
                    UseTokenId = UseTokenId ?? false;
                    if (Id == null)
                        log.LogWarning($"ID FOR XTRIGGER '{this}' ISN'T SET");

                    Dictionary<string, List<RefMorphDetails>> xtriggers = Machine.GetEntity<Element>(this._container.Id).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;
                    if (!xtriggers.ContainsKey(this.Id))
                        log.LogWarning($"FINAL CATALYST FOR A REDIRECTING XTRIGGER {this} ISN'T SET");

                    break;

                //doesn't use Id
                case MorphEffectsExtended.Quantity:
                case MorphEffectsExtended.Decay:
                case MorphEffectsExtended.Destroy:
                case MorphEffectsExtended.TimeSpend:
                case MorphEffectsExtended.TimeSet:
                case MorphEffectsExtended.LeverFuture:
                case MorphEffectsExtended.LeverPast:
                case MorphEffectsExtended.Apply:
                case MorphEffectsExtended.Break:
                default:
                    break;
            }

            UnknownProperties.Remove(this.Id);

            if (MorphEffect != MorphEffectsExtended.Induce)
                Induction = null;

            //even if these properties are needed, they are safely wrapped inside the Induction by now
            Expulsion = null;

            if (MorphEffect != MorphEffectsExtended.Move)
                ToPath = null;
        }

        public bool Execute(Token reactingToken, string reactingElementId, int reactingElementQuantity, int catalystQuantity)
        {
            Crossroads.MarkLocalToken(reactingToken);
            Crossroads.MarkSource(reactingToken);

            if (Random.Range(1, 101) > Chance.value)
            {
                Crossroads.UnmarkLocalToken();
                return false;
            }

            reactingElementQuantity = UseMyQuantity ? reactingElementQuantity : 1;
            catalystQuantity = UseCatalystQuantity ? catalystQuantity : 1;

            switch (MorphEffect)
            {
                case MorphEffectsExtended.Transform:
                    int initialQuantity = reactingToken.Quantity;
                    RecipeExecutionBuffer.ScheduleTransformation(reactingToken, this.Id, VFX);
                    var resultingQuantity = initialQuantity * Level.value * reactingElementQuantity * catalystQuantity;
                    var needChange = resultingQuantity - reactingToken.Quantity;
                    RecipeExecutionBuffer.ScheduleQuantityChange(reactingToken, needChange, RetirementVFX.None);
                    break;
                case MorphEffectsExtended.Spawn:
                    RecipeExecutionBuffer.ScheduleSpawn(reactingToken.Sphere, this.Id, Level.value * catalystQuantity * reactingElementQuantity, VFX);
                    break;
                case MorphEffectsExtended.SetMutation:
                    RecipeExecutionBuffer.ScheduleMutation(reactingToken, this.Id, Level.value * catalystQuantity * reactingElementQuantity, false, VFX);
                    break;
                case MorphEffectsExtended.Mutate:
                    RecipeExecutionBuffer.ScheduleMutation(reactingToken, this.Id, Level.value * catalystQuantity * reactingElementQuantity, true, VFX);
                    break;

                case MorphEffectsExtended.Quantity:
                    RecipeExecutionBuffer.ScheduleQuantityChange(reactingToken, Level.value * catalystQuantity * reactingElementQuantity, VFX);
                    break;

                case MorphEffectsExtended.DeckDraw:
                    Legerdemain.Deal(this.Id, reactingToken.Sphere, Level.value * catalystQuantity * reactingElementQuantity, VFX);
                    break;
                case MorphEffectsExtended.DeckShuffle:
                    RecipeExecutionBuffer.ScheduleDeckRenew(this.Id);
                    break;

                case MorphEffectsExtended.Destroy:
                    RecipeExecutionBuffer.ScheduleTransformation(reactingToken, string.Empty, VFX);
                    break;
                case MorphEffectsExtended.Decay:
                    RecipeExecutionBuffer.ScheduleDecay(reactingToken, VFX);
                    break;

                case MorphEffectsExtended.Induce:
                    RecipeExecutionBuffer.ScheduleRecipeInduction(RavensEye.currentSituation, Induction);
                    break;
                case MorphEffectsExtended.Link:
                    Machine.PushTemporaryRecipeLink(this.Id, Level.value);
                    break;

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
                    Elegiast.Scribe.SetLeverForNextPlaythrough(this.Id, ActingId());
                    break;

                case MorphEffectsExtended.LeverPast:
                    Elegiast.Scribe.SetLeverForCurrentPlaythrough(this.Id, ActingId());
                    break;

                case MorphEffectsExtended.Trigger:
                    var triggerQuantity = Level.value * catalystQuantity * reactingElementQuantity;
                    GrandEffects.RunXTriggers(reactingToken, new Dictionary<string, int> { { Id, triggerQuantity } });
                    break;

                case MorphEffectsExtended.Redirect:
                    Dictionary<string, List<RefMorphDetails>> xtriggers = Machine.GetEntity<Element>(ActingId()).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;
                    if (xtriggers.TryGetValue(this.Id, out List<RefMorphDetails> redirects))
                        foreach (RefMorphDetails effect in redirects)
                            if (effect.Execute(reactingToken, reactingElementId, reactingElementQuantity, catalystQuantity))
                                break;

                    break;

                case MorphEffectsExtended.Recipe:
                    var initialSphere = Crossroads.GetLocalSphere();

                    var situation = RavensEye.currentSituation;
                    var hornedAxe = Watchman.Get<HornedAxe>();
                    Character character = Watchman.Get<Stable>().Protag();

                    var recipeToExecute = Watchman.Get<Compendium>().GetEntityById<Recipe>(Id);
                    while (recipeToExecute.IsValid())
                    {
                        var grandEffects = recipeToExecute.RetrieveProperty<GrandEffects>(RecipeEffectsMaster.GRAND_EFFECTS);
                        var target = grandEffects.GetTargetSpheres(situation);
                        grandEffects.RunGrandEffects(target, false);

                        recipeToExecute = NullRecipe.Create();
                        foreach (LinkedRecipeDetails linkedRecipeDetails in recipeToExecute.Linked)
                        {
                            AspectsInContext aspectsInContext = hornedAxe.GetAspectsInContext(situation);
                            recipeToExecute = linkedRecipeDetails.GetRecipeWhichCanExecuteInContext(aspectsInContext, character);
                            if (recipeToExecute.IsValid())
                                break;
                        }
                    }

                    Crossroads.MarkLocalSphere(initialSphere);
                    return false;

                case MorphEffectsExtended.Apply:
                    RecipeExecutionBuffer.ApplyAllEffects();
                    break;

                case MorphEffectsExtended.Break:
                    RecipeExecutionBuffer.ScheduleVFX(reactingToken, VFX);
                    Crossroads.UnmarkLocalToken();
                    Crossroads.UnmarkSource();
                    return true;

                case MorphEffectsExtended.Move:
                    var targetSpheres = ToPath.GetSpheresByPath();
                    if (targetSpheres.Count > 0)
                        RecipeExecutionBuffer.ScheduleMovement(reactingToken, targetSpheres[Random.Range(0, targetSpheres.Count)], VFX);
                    break;

                default:
                    Birdsong.TweetLoud($"Unknown trigger '{MorphEffect}' for element stack '{reactingToken.PayloadEntityId}'");
                    break;
            }

            Crossroads.UnmarkLocalToken();
            Crossroads.UnmarkSource();

            return false;

            string ActingId() => UseTokenId == false ? reactingElementId : reactingToken.PayloadEntityId;
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
                    {
                        morphDetails.SetContainer(__instance);
                        morphDetails.OnPostImport(log, populatedCompendium);
                    }
            }
        }

        static bool CrossTriggerInMalleary(AutoCompletingInput ___input, DrydockSphere ____elementDrydock)
        {
            var catalyst = new Dictionary<string, int>() { { ___input.text.Trim(), 1 } };
            GrandEffects.RunXTriggers(____elementDrydock.Tokens, catalyst);
            RecipeExecutionBuffer.ApplyAllEffects();
            RecipeExecutionBuffer.ApplyVFX();
            return false;
        }
    }
}