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
    public interface IRecipeExecutionEffect : IEntityWithId
    {
        RetirementVFX VFX { get; set; }
        void Execute(Sphere storage, Situation fromSituation);
    }

    public class MutationExecution : AbstractEntity<MutationExecution>, IRecipeExecutionEffect
    {
        [FucineValue]
        public Dictionary<Funcine<bool>, List<RefMutationEffect>> Mutations { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.None)]
        public RetirementVFX VFX { get; set; }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public void Execute(Sphere sphere, Situation fromSituation)
        {
            List<Token> tokens = sphere.GetElementTokens();

            foreach (Funcine<bool> filter in Mutations.Keys)
            {
                List<Token> targets = tokens.FilterTokens(filter);

                if (targets.Count > 0)
                    foreach (RefMutationEffect mutationEffect in Mutations[filter])
                        RecipeExecutionBuffer.ScheduleMutation(targets, mutationEffect.Mutate, mutationEffect.Level.value, mutationEffect.Additive, mutationEffect.VFX);
            }
        }
    }

    public class XTriggerExecution : AbstractEntity<XTriggerExecution>, IRecipeExecutionEffect
    {
        [FucineValue]
        public Dictionary<string, Funcine<int>> Aspects { get; set; }
        [FucineValue(DefaultValue = false)]
        public bool TokensCatalyse { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.None)]
        public RetirementVFX VFX { get; set; }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        private static readonly AspectsDictionary allCatalystsInSphere = new AspectsDictionary();
        public void Execute(Sphere sphere, Situation situation)
        {
            allCatalystsInSphere.Clear();
            if (Aspects != null) foreach (KeyValuePair<string, Funcine<int>> catalyst in Aspects)
                    allCatalystsInSphere[catalyst.Key] = catalyst.Value.value;

            if (TokensCatalyse)
            {
                allCatalystsInSphere.ApplyMutations(sphere.GetTotalAspects());
                allCatalystsInSphere[situation.Recipe.Id] = 1;
            }

            if (allCatalystsInSphere.Count == 0)
                return;

            Dictionary<string, List<RefMorphDetails>> xtriggers;
            foreach (Token token in sphere.GetElementTokens())
            {
                if (token.IsValidElementStack() == false)
                    continue;

                if (RecipeEffectsMaster.TryGetRefXTriggers(token.PayloadEntityId, out xtriggers))
                    foreach (KeyValuePair<string, int> catalyst in allCatalystsInSphere)
                        if (xtriggers.ContainsKey(catalyst.Key)) foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                morphDetails.Execute(situation, token, token.PayloadEntityId, 1, catalyst.Value, true);

                AspectsDictionary tokenAspects = new AspectsDictionary(Machine.GetEntity<Element>(token.PayloadEntityId).Aspects);
                tokenAspects.ApplyMutations(token.GetCurrentMutations());

                foreach (KeyValuePair<string, int> aspect in tokenAspects)
                    if (RecipeEffectsMaster.TryGetRefXTriggers(token.PayloadEntityId, out xtriggers))
                        foreach (KeyValuePair<string, int> catalyst in allCatalystsInSphere)
                            if (xtriggers.ContainsKey(catalyst.Key)) foreach (RefMorphDetails morphDetails in xtriggers[catalyst.Key])
                                    morphDetails.Execute(situation, token, aspect.Key, aspect.Value, catalyst.Value, false);
            }

            TokenContextAccessors.ResetLocalToken();
        }
    }

    public class DeckEffectsExecution : AbstractEntity<DeckEffectsExecution>, IRecipeExecutionEffect
    {
        [FucineValue(DefaultValue = RetirementVFX.None)]
        public RetirementVFX VFX { get; set; }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        private static readonly AspectsDictionary allCatalystsInSphere = new AspectsDictionary();
        public void Execute(Sphere sphere, Situation situation)
        {

        }
    }

    public class RecipeEffectsExecution : AbstractEntity<RecipeEffectsExecution>, IRecipeExecutionEffect
    {
        [FucineValue]
        Dictionary<Funcine<bool>, Funcine<int>> Effects { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.None)]
        public RetirementVFX VFX { get; set; }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public void Execute(Sphere sphere, Situation situation)
        {
            List<Token> tokens = sphere.GetElementTokens();
            foreach (Funcine<bool> filter in Effects.Keys)
            {
                int level = Effects[filter].value;
                if (level < 0)
                {
                    List<Token> filteredTokens = tokens.FilterTokens(filter);
                    level = Mathf.Max(level, -filteredTokens.Count);
                    while (level++ < 0)
                        RecipeExecutionBuffer.ScheduleRetirement(filteredTokens[UnityEngine.Random.Range(0, filteredTokens.Count)], RetirementVFX.None);
                }
                else
                    RecipeExecutionBuffer.ScheduleCreation(sphere, filter.formula, level, RetirementVFX.None);
            }
        }
    }
}