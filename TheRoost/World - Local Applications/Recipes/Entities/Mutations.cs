using System;
using System.Collections;

using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Entities;

using Roost.Twins.Entities;

namespace Roost.World.Recipes.Entities
{
    public class RefMutationEffect : AbstractEntity<RefMutationEffect>, IQuickSpecEntity
    {
        [FucineValue(DefaultValue = null, ValidateValueAs = typeof(Element))] public string Mutate { get; set; }
        [FucineConstruct("0")] public FucineExp<int> Level { get; set; }
        [FucineValue(false)] public bool Additive { get; set; }
        [FucineValue(DefaultValue = RetirementVFX.CardTransformWhite)] public RetirementVFX VFX { get; set; }
        [FucineSubEntity] public TokenFilterSpec Filter { get; set; }

        public static void Enact()
        {
            Machine.AddImportMolding<Recipe>(ConvertLegacyMutations);
            Machine.AddImportMolding<GrandEffects>(ConvertLegacyMutations);
        }

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


        static void ConvertLegacyMutations(EntityData recipeEntityData)
        {
            const string FILTER = "filter";
            const string MUTATE = "mutate";
            const string MUTATIONS = "mutations";

            try
            {
                if (recipeEntityData.ValuesTable.ContainsKey(MUTATIONS)
                && (recipeEntityData.ValuesTable[MUTATIONS] is EntityData))
                {
                    EntityData mutations = recipeEntityData.ValuesTable[MUTATIONS] as EntityData;
                    ArrayList newMutations = new ArrayList();
                    recipeEntityData.ValuesTable[MUTATIONS] = newMutations;

                    foreach (string filter in mutations.ValuesTable.Keys)
                        foreach (object mutation in mutations.GetArrayListFromEntityData(filter))
                        {
                            EntityData mutationEntityData = mutation as EntityData;
                            if (mutationEntityData == null)
                            {
                                mutationEntityData = new EntityData();
                                mutationEntityData[MUTATE] = mutation;
                            }

                            mutationEntityData[FILTER] = filter;
                            newMutations.Add(mutationEntityData);
                        }
                }
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"Failed to mold mutations in {recipeEntityData.Id}: {ex.FormatException()}");
            }
        }
    }
}