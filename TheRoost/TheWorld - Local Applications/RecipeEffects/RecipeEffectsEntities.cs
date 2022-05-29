using System.Collections;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Entities;
using SecretHistories.Core;

using Roost.Twins;
using Roost.Twins.Entities;
using SecretHistories.Spheres;

using UnityEngine;

namespace Roost.World.Recipes.Entities
{
    public class GrandEffects : AbstractEntity<GrandEffects>
    {
        [FucineValue] public string comments { get; set; }

        [FucineDict] public Dictionary<Funcine<bool>, List<RefMutationEffect>> Mutations { get; set; }
        [FucineDict] public Dictionary<string, Funcine<int>> Aspects { get; set; }

        [FucineList] public List<string> DeckShuffles { get; set; }
        [FucineDict] public Dictionary<string, List<string>> DeckForbids { get; set; }
        [FucineDict] public Dictionary<string, List<string>> DeckAllows { get; set; }
        [FucineDict] public Dictionary<string, Funcine<int>> DeckEffects { get; set; }
        [FucineDict] public Dictionary<string, List<string>> DeckAdds { get; set; }
        [FucineDict] public Dictionary<string, List<Funcine<bool>>> DeckTakeOuts { get; set; }
        [FucineDict] public Dictionary<string, List<Funcine<bool>>> DeckInserts { get; set; }

        [FucineDict] public Dictionary<Funcine<bool>, Funcine<int>> Effects { get; set; }
        [FucineDict] public Dictionary<Funcine<bool>, List<Funcine<int>>> Decays { get; set; }

        [FucineDict] public Dictionary<FucinePath, List<GrandEffects>> SphereEffects { get; set; }



        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX DeckEffectsVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX EffectsVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardLight)] public RetirementVFX DecaysVFX { get; set; }

        public GrandEffects() { }
        public GrandEffects(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public void Run(Situation situation, Sphere onSphere)
        {
            TokenContextAccessors.SetLocalSituation(situation);

            RunRefMutations(onSphere);
            RecipeExecutionBuffer.ApplyMutations();

            RunXTriggers(onSphere, situation);
            RecipeExecutionBuffer.ApplyAll();

            Legerdemain.RunExtendedDeckEffects(this, onSphere);
            RecipeExecutionBuffer.ApplyMovements();
            RecipeExecutionBuffer.ApplyRenews();

            RunRefEffects(onSphere);
            RecipeExecutionBuffer.ApplyRetirements();
            RecipeExecutionBuffer.ApplyCreations();

            RunRefDecays(onSphere);
            RecipeExecutionBuffer.ApplyRetirements();
            RecipeExecutionBuffer.ApplyTransformations();

            RunSphereEffects(situation);

        }

        public void RunRefMutations(Sphere sphere)
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
        }

        private static readonly AspectsDictionary allCatalystsInSphere = new AspectsDictionary();
        public void RunXTriggers(Sphere sphere, Situation situation)
        {
            allCatalystsInSphere.Clear();
            if (Aspects != null)
                foreach (KeyValuePair<string, Funcine<int>> catalyst in Aspects)
                    allCatalystsInSphere[catalyst.Key] = catalyst.Value.value;
            allCatalystsInSphere.ApplyMutations(sphere.GetTotalAspects());

            if (allCatalystsInSphere.Count == 0)
                return;

            Dictionary<string, List<RefMorphDetails>> xtriggers;
            foreach (Token token in sphere.GetElementTokens())
            {
                if (token.IsValidElementStack() == false)
                    continue;

                xtriggers = Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;

                if (xtriggers != null)
                    foreach (KeyValuePair<string, int> catalyst in allCatalystsInSphere)
                        if (xtriggers.ContainsKey(catalyst.Key)) foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                morphDetails.Execute(situation, token, token.PayloadEntityId, 1, catalyst.Value, true);

                AspectsDictionary tokenAspects = new AspectsDictionary(Machine.GetEntity<Element>(token.PayloadEntityId).Aspects);
                tokenAspects.ApplyMutations(token.GetCurrentMutations());

                foreach (KeyValuePair<string, int> aspect in tokenAspects)
                {
                    xtriggers = Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId).RetrieveProperty("xtriggers") as Dictionary<string, List<RefMorphDetails>>;
                    if (xtriggers != null)
                        foreach (KeyValuePair<string, int> catalyst in allCatalystsInSphere)
                            if (xtriggers.ContainsKey(catalyst.Key)) foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                    morphDetails.Execute(situation, token, aspect.Key, aspect.Value, catalyst.Value, false);
                }
            }

            TokenContextAccessors.ResetLocalToken();
        }

        public void RunRefEffects(Sphere storage)
        {
            if (Effects == null)
                return;

            List<Token> allTokens = storage.GetElementTokens();
            foreach (Funcine<bool> filter in Effects.Keys)
            {
                int level = Effects[filter].value;
                if (level < 0)
                {
                    List<Token> filteredTokens = allTokens.FilterTokens(filter);
                    while (level < 0 && filteredTokens.Count > 0)
                    {
                        RecipeExecutionBuffer.ScheduleRetirement(filteredTokens[Random.Range(0, filteredTokens.Count)], EffectsVFX);
                        level++;
                    }
                }
                else
                    RecipeExecutionBuffer.ScheduleCreation(storage, filter.formula, level, EffectsVFX);
            }
        }

        public void RunRefDecays(Sphere sphere)
        {
            if (Decays == null)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (Funcine<bool> filter in Decays.Keys)
            {
                List<Token> targets = tokens.FilterTokens(filter);
                if (targets.Count > 0)
                    foreach (Token token in targets)
                        RecipeExecutionBuffer.ScheduleDecay(token, DecaysVFX);
            }
        }

        public void RunSphereEffects(Situation situation)
        {
            if (SphereEffects != null)
                foreach (KeyValuePair<FucinePath, List<GrandEffects>> sphereEffect in SphereEffects)
                {
                    HashSet<Sphere> targetSpheres = TokenContextAccessors.GetSpheresByPath(sphereEffect.Key);
                    foreach (GrandEffects effectGroup in sphereEffect.Value)
                        foreach (Sphere sphere in targetSpheres)
                            effectGroup.Run(situation, sphere);
                }
        }
    }

    public class RefMutationEffect : AbstractEntity<RefMutationEffect>, ICustomSpecEntity
    {
        [FucineEverValue(ValidateAsElementId = true, DefaultValue = null)]
        public string Mutate { get; set; }
        [FucineEverValue("1")]
        public Funcine<int> Level { get; set; }
        [FucineEverValue(false)]
        public bool Additive { get; set; }
        [FucineEverValue(DefaultValue = RetirementVFX.CardLight)]
        public RetirementVFX VFX { get; set; }

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
        [FucineEverValue(DefaultValue = MorphEffectsExtended.Transform)]
        public MorphEffectsExtended MorphEffect { get; set; }
        [FucineEverValue(DefaultValue = 1)]
        public Funcine<int> Level { get; set; }
        [FucineEverValue(DefaultValue = 100)]
        public Funcine<int> Chance { get; set; }
        [FucineEverValue]
        public Expulsion Expulsion { get; set; }
        [FucineEverValue(DefaultValue = RetirementVFX.CardBurn)]
        public RetirementVFX VFX { get; set; }

        public void QuickSpec(string value)
        {
            this.SetId(value);
            this.Chance = new Funcine<int>("100");
            this.MorphEffect = MorphEffectsExtended.Transform;
            this.Level = new Funcine<int>("1");
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
            if (MorphEffect == MorphEffectsExtended.Link || MorphEffect == MorphEffectsExtended.Induce)
            {
                if (Machine.GetEntity<Recipe>(this.Id) == null)
                    Birdsong.Sing("Unknown recipe id '{0}'", this.Id);
                return;
            }
            if (MorphEffect == MorphEffectsExtended.Transform ||
                MorphEffect == MorphEffectsExtended.Spawn ||
                MorphEffect == MorphEffectsExtended.MutateSet ||
                MorphEffect == MorphEffectsExtended.Mutate)
            {
                if (Machine.GetEntity<Element>(this.Id) == null)
                    Birdsong.Sing("Unknown element id '{0}'", this.Id);
            }
        }

        public void Execute(Situation situation, Token token, string elementId, int aspectAmount, int catalystAmount, bool onToken)
        {
            TokenContextAccessors.SetLocalToken(token);

            if (Chance.value < Random.Range(1, 101))
                return;

            switch (MorphEffect)
            {
                case MorphEffectsExtended.Transform:
                    if (onToken)
                        RecipeExecutionBuffer.ScheduleTransformation(token, this.Id, VFX);
                    else
                    {
                        RecipeExecutionBuffer.ScheduleMutation(token, elementId, aspectAmount, true, RetirementVFX.None);
                        RecipeExecutionBuffer.ScheduleMutation(token, elementId, -aspectAmount, true, VFX);
                    }
                    break;
                case MorphEffectsExtended.Spawn:
                    RecipeExecutionBuffer.ScheduleCreation(token.Sphere, this.Id, token.Quantity * Level.value * catalystAmount, VFX);
                    break;
                case MorphEffectsExtended.MutateSet:
                    RecipeExecutionBuffer.ScheduleMutation(token, this.Id, Level.value * catalystAmount * aspectAmount, false, VFX);
                    break;
                case MorphEffectsExtended.Mutate:
                    RecipeExecutionBuffer.ScheduleMutation(token, this.Id, Level.value * catalystAmount * aspectAmount, true, VFX);
                    break;
                case MorphEffectsExtended.DeckDraw:
                    Legerdemain.Deal(this.Id, token.Sphere, Level.value * catalystAmount * aspectAmount);
                    break;
                case MorphEffectsExtended.Destroy:
                    if (onToken)
                        RecipeExecutionBuffer.ScheduleRetirement(token, VFX);
                    else
                        RecipeExecutionBuffer.ScheduleMutation(token, elementId, -aspectAmount, true, RetirementVFX.None);
                    break;
                case MorphEffectsExtended.Induce:
                    Recipe recipeToInduce = Watchman.Get<Compendium>().GetEntityById<Recipe>(this.Id);
                    for (int i = Level.value; i > 0; i--)
                        Roost.World.Recipes.RecipeLinkMaster.SpawnNewSituation(situation, recipeToInduce, Expulsion, FucinePath.Current());
                    break;
                case MorphEffectsExtended.Link: Machine.PushXtriggerLink(this.Id, Level.value); break;
                default: Birdsong.Sing("Unknown trigger '{0}' for element stack '{1}'", MorphEffect, token.PayloadEntityId); break;
            }
        }
    }
}
