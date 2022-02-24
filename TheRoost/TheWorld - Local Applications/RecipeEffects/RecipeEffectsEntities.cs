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
    public class RefMutationEffect : AbstractEntity<RefMutationEffect>, ICustomSpecEntity
    {
        [FucineValue(ValidateAsElementId = true, DefaultValue = null)]
        public string Mutate { get; set; }
        [FucineSpecial("1")]
        public Funcine<int> Level { get; set; }
        [FucineValue(false)]
        public bool Additive { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardLight)]
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

    public enum MorphEffectsExtended { Transform, Spawn, MutateSet, Mutate, Destroy, Decay, Induce, Link }
    public class RefMorphDetails : AbstractEntity<RefMorphDetails>, IQuickSpecEntity, ICustomSpecEntity
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
                        RecipeExecutionBuffer.ScheduleTransformation(token, Id, VFX);
                    else
                    {
                        RecipeExecutionBuffer.ScheduleMutation(token, elementId, aspectAmount, true, RetirementVFX.None);
                        RecipeExecutionBuffer.ScheduleMutation(token, elementId, -aspectAmount, true, VFX);
                    }
                    break;
                case MorphEffectsExtended.Spawn:
                    RecipeExecutionBuffer.ScheduleCreation(token.Sphere, Id, token.Quantity * Level.value * catalystAmount, VFX);
                    break;
                case MorphEffectsExtended.MutateSet:
                    RecipeExecutionBuffer.ScheduleMutation(token, Id, Level.value * catalystAmount * aspectAmount, false, VFX);
                    break;
                case MorphEffectsExtended.Mutate:
                    RecipeExecutionBuffer.ScheduleMutation(token, Id, Level.value * catalystAmount * aspectAmount, true, VFX);
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
                        Roost.World.Recipes.RecipeLinkMaster.SpawnNewSituation(situation, recipeToInduce, Expulsion);
                    break;
                case MorphEffectsExtended.Link: Machine.PushXtriggerLink(Id, Level.value); break;
                default: Birdsong.Sing("Unknown trigger '{0}' for element stack '{1}'", MorphEffect, token.PayloadEntityId); break;
            }
        }
    }

    public abstract class RecipeEffectsGroup : AbstractEntity<RecipeEffectsGroup>
    {
        [FucineValue]
        public string comments { get; set; }

        [FucineValue]
        public Dictionary<Funcine<bool>, List<RefMutationEffect>> Mutations { get; set; }
        [FucineValue]
        public Dictionary<string, Funcine<int>> Aspects { get; set; }
        [FucineValue]
        public bool ElementsAsCatalysts { get; set; }

        [FucineValue]
        public List<string> DeckShuffles { get; set; }
        [FucineValue]
        public Dictionary<string, List<string>> DeckForbids { get; set; }
        [FucineValue]
        public Dictionary<string, List<string>> DeckAllows { get; set; }
        [FucineValue]
        public Dictionary<string, Funcine<int>> DeckDraws { get; set; }
        [FucineValue]
        public Dictionary<string, List<string>> DeckAdds { get; set; }
        [FucineValue]
        public Dictionary<string, List<Funcine<bool>>> DeckTakeOuts { get; set; }
        [FucineValue]
        public Dictionary<string, List<Funcine<bool>>> DeckInserts { get; set; }

        [FucineValue]
        public Dictionary<Funcine<bool>, Funcine<int>> Effects { get; set; }
        [FucineValue]
        public Dictionary<Funcine<bool>, Funcine<int>> Decays { get; set; }

        [FucineValue]
        public Dictionary<SphereRef, List<IRecipeExecutionEffect>> GrandEffects { get; set; }

        public RecipeEffectsGroup(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }


    }

}
