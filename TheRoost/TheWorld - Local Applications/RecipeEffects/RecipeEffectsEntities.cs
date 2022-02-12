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

        [FucineValue] public Dictionary<Funcine<bool>, List<RefMutationEffect>> mutations { get; set; }
        [FucineValue] public Dictionary<string, Funcine<int>> aspects { get; set; }

        [FucineValue] public List<string> deckShuffles { get; set; }
        [FucineValue] public Dictionary<string, List<string>> deckForbids { get; set; }
        [FucineValue] public Dictionary<string, List<string>> deckAllows { get; set; }
        [FucineValue] public Dictionary<string, Funcine<int>> deckDraws { get; set; }
        [FucineValue] public Dictionary<string, List<string>> deckAdds { get; set; }
        [FucineValue] public Dictionary<string, List<Funcine<bool>>> deckTakeOuts { get; set; }
        [FucineValue] public Dictionary<string, List<Funcine<bool>>> deckInserts { get; set; }

        [FucineValue] public Dictionary<Funcine<bool>, Funcine<int>> effects { get; set; }
        [FucineValue] public Dictionary<Funcine<bool>, Funcine<int>> decays { get; set; }

        [FucineValue] public Dictionary<SphereRef, GrandEffects> grandEffects { get; set; }

        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX DeckEffectsVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.None)] public RetirementVFX EffectsVFX { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardLight)] public RetirementVFX DecaysVFX { get; set; }

        public GrandEffects() { }
        public GrandEffects(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public void Run(Situation situation, Sphere onSphere)
        {
            RunRefMutations(onSphere);
            RunXTriggers(situation, onSphere);
            Legerdemain.RunExtendedDeckEffects(this, onSphere);
            RunRefEffects(onSphere);
            RunRefDecays(onSphere);

            if (grandEffects != null)
                foreach (SphereRef sphere in grandEffects.Keys)
                    grandEffects[sphere].Run(situation, sphere.referencedSphere);
        }

        public void RunRefMutations(Sphere sphere)
        {
            if (mutations == null)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (Funcine<bool> filter in mutations.Keys)
            {
                List<Token> targets = tokens.FilterTokens(filter);

                if (targets.Count > 0)
                    foreach (RefMutationEffect mutationEffect in mutations[filter])
                        RecipeExecutionBuffer.ScheduleMutation(targets, mutationEffect.Mutate, mutationEffect.Level.value, mutationEffect.Additive, mutationEffect.VFX);
            }
        }

        private static readonly AspectsDictionary allAspectsInSphereCached = new AspectsDictionary();
        private void RunXTriggers(Situation situation, Sphere storage)
        {
            if (aspects == null)
                return;

            allAspectsInSphereCached.Clear();
            foreach (KeyValuePair<string, Funcine<int>> catalyst in aspects)
                allAspectsInSphereCached[catalyst.Key] = catalyst.Value.value;
            allAspectsInSphereCached.ApplyMutations(storage.GetTotalAspects());

            Dictionary<string, List<RefMorphDetails>> xtriggers;
            foreach (Token token in storage.GetElementTokens())
            {
                if (token.IsValidElementStack() == false)
                    continue;

                if (RecipeEffectsExtension.TryGetRefXTriggers(token.PayloadEntityId, out xtriggers))
                    foreach (KeyValuePair<string, int> catalyst in allAspectsInSphereCached)
                        if (xtriggers.ContainsKey(catalyst.Key))
                            foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                morphDetails.ExecuteOnStack(situation, token, catalyst.Value);

                foreach (string aspectId in token.GetCurrentMutations().Keys)
                    if (RecipeEffectsExtension.TryGetRefXTriggers(token.PayloadEntityId, out xtriggers))
                        foreach (KeyValuePair<string, int> catalyst in allAspectsInSphereCached)
                            if (xtriggers.ContainsKey(catalyst.Key))
                                foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                    morphDetails.ExecuteOnMutation(situation, token, aspectId, catalyst.Value);
            }

            TokenContextAccessors.ResetLocalToken();
        }

        public void RunRefEffects(Sphere storage)
        {
            if (effects == null)
                return;

            List<Token> allTokens = storage.GetElementTokens();
            foreach (Funcine<bool> filter in effects.Keys)
            {
                int level = effects[filter].value;
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
                    RecipeExecutionBuffer.ScheduleCreation(storage, filter.formula, level, EffectsVFX);
            }
        }

        public void RunRefDecays(Sphere sphere)
        {
            if (decays == null)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (Funcine<bool> filter in decays.Keys)
            {
                List<Token> targets = tokens.FilterTokens(filter);
                if (targets.Count > 0)
                    foreach (Token token in targets)
                        RecipeExecutionBuffer.ScheduleDecay(token, DecaysVFX);
            }
        }
    }

    public class RefMutationEffect : AbstractEntity<RefMutationEffect>, IWeirdSpecEntity
    {
        [FucineValue(ValidateAsElementId = true, DefaultValue = null)]
        public string Mutate { get; set; }
        [FucineStruct("1")]
        public Funcine<int> Level { get; set; }
        [FucineValue(false)]
        public bool Additive { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardLight)]
        public RetirementVFX VFX { get; set; }

        public RefMutationEffect(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public void WeirdSpec(Hashtable data)
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

    public enum MorphEffectsExtended { Transform, Spawn, Mutate, MutateAdd, Destroy, Decay, Induce }

    public class RefMorphDetails : AbstractEntity<RefMorphDetails>, IQuickSpecEntity
    {
        [FucineValue(DefaultValue = MorphEffectsExtended.Transform)]
        public MorphEffectsExtended MorphEffect { get; set; }
        [FucineValue(DefaultValue = 1)]
        public Funcine<int> Level { get; set; }
        [FucineValue(DefaultValue = 100)]
        public Funcine<int> Chance { get; set; }
        [FucineValue]
        public Expulsion Expulsion { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardBurn)]
        public RetirementVFX VFX { get; set; }

        public void QuickSpec(string value)
        {
            this.SetId(value);
            this.Chance = new Funcine<int>("100");
            this.MorphEffect = MorphEffectsExtended.Transform;
            this.Level = new Funcine<int>("1");
        }

        public RefMorphDetails() { }
        public RefMorphDetails(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public void ExecuteOnStack(Situation situation, Token token, int catalystAmount)
        {
            TokenContextAccessors.SetLocalToken(token);

            if (Chance.value < Random.Range(1, 101))
                return;

            switch (MorphEffect)
            {
                case MorphEffectsExtended.Transform: RecipeExecutionBuffer.ScheduleTransformation(token, Id, VFX); break;
                case MorphEffectsExtended.Spawn:
                    RecipeExecutionBuffer.ScheduleCreation(token.Sphere, Id, token.Quantity * Level.value * catalystAmount, VFX); break;
                case MorphEffectsExtended.Mutate: RecipeExecutionBuffer.ScheduleMutation(token, Id, Level.value * catalystAmount, false, VFX); break;
                case MorphEffectsExtended.MutateAdd: RecipeExecutionBuffer.ScheduleMutation(token, Id, Level.value * catalystAmount, true, VFX); break;
                case MorphEffectsExtended.Destroy: RecipeExecutionBuffer.ScheduleRetirement(token, VFX); break;
                case MorphEffectsExtended.Induce:
                    Recipe recipeToInduce = Watchman.Get<Compendium>().GetEntityById<Recipe>(this.Id);
                    Roost.World.Recipes.Inductions.InductionsExtensions.SpawnNewSituation(situation, recipeToInduce, Expulsion);
                    break;
                default: Birdsong.Sing("Unknown @xtrigger '{0}' for element stack '{1}'", MorphEffect, token.PayloadEntityId); break;
            }
        }

        public void ExecuteOnMutation(Situation situation, Token token, string aspectId, int catalystAmount)
        {
            TokenContextAccessors.SetLocalToken(token);

            if (Chance.value < Random.Range(1, 101))
                return;

            int aspectQuantity = token.GetCurrentMutations()[aspectId];

            switch (MorphEffect)
            {
                case MorphEffectsExtended.Transform:
                    RecipeExecutionBuffer.ScheduleMutation(token, aspectId, aspectQuantity, true, RetirementVFX.None);
                    RecipeExecutionBuffer.ScheduleMutation(token, aspectId, 0, false, RetirementVFX.None);
                    break;
                case MorphEffectsExtended.Spawn:
                    RecipeExecutionBuffer.ScheduleCreation(token.Sphere, Id, aspectQuantity * Level.value * catalystAmount, VFX); break;
                case MorphEffectsExtended.Mutate:
                    RecipeExecutionBuffer.ScheduleMutation(token, Id, aspectQuantity * Level.value * catalystAmount, false, VFX); break;
                case MorphEffectsExtended.MutateAdd:
                    RecipeExecutionBuffer.ScheduleMutation(token, Id, aspectQuantity * Level.value * catalystAmount, true, VFX); break;
                case MorphEffectsExtended.Destroy:
                    RecipeExecutionBuffer.ScheduleMutation(token, aspectId, 0, false, RetirementVFX.None); break;
                case MorphEffectsExtended.Induce:
                    Recipe recipeToInduce = Watchman.Get<Compendium>().GetEntityById<Recipe>(this.Id);
                    Roost.World.Recipes.Inductions.InductionsExtensions.SpawnNewSituation(situation, recipeToInduce, Expulsion);
                    break;
                default:
                    Birdsong.Sing("Unknown @xtrigger '{0}' for element stack '{1}'", MorphEffect, token.PayloadEntityId); break;
            }
        }
    }
}
