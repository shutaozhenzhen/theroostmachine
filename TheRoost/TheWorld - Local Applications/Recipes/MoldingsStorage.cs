using System;
using System.Collections;

using OrbCreationExtensions;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Fucine;


namespace Roost.World.Recipes.Entities
{
    internal static class MoldingsStorage
    {

        internal static void ConvertLegacyMutations(EntityData recipeEntityData)
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
                throw ex;
            }
        }

        internal static void ConvertExpulsionFilters(EntityData data)
        {
            const string FILTER = "filter";

            try
            {
                EntityData filters = data.GetEntityDataFromEntityData(FILTER);
                if (filters != null)
                {
                    string positiveORFilters = string.Empty;
                    string negativeANDFilters = string.Empty;

                    foreach (DictionaryEntry filter in filters.ValuesTable)
                    {
                        if (filter.Value.ToString()[0] == '-')
                            //if starts with '-', negative requirement; must be "less than abs()"; but instead of abs() we just remove '-' - same result
                            negativeANDFilters += $"{AsExpression(filter.Key)}<{AsExpression(filter.Value).Substring(1)}&&";
                        else
                            positiveORFilters += $"{AsExpression(filter.Key)}>={AsExpression(filter.Value)}||";
                    }

                    if (positiveORFilters.Length > 0)
                        positiveORFilters = positiveORFilters.Remove(positiveORFilters.Length - 2);
                    if (negativeANDFilters.Length > 0)
                        negativeANDFilters = negativeANDFilters.Remove(negativeANDFilters.Length - 2);

                    if (positiveORFilters.Length > 0 && negativeANDFilters.Length > 0)
                        data[FILTER] = $"({positiveORFilters})&&({negativeANDFilters})";
                    else
                        data[FILTER] = positiveORFilters.Length > 0 ? positiveORFilters : negativeANDFilters;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            string AsExpression(object value)
            {
                string result = value.ToString();
                if (!result.Contains("[") && !int.TryParse(result, out _))//not a wrapped expression already and not a numeric value
                    result = "[" + result + "]";
                return result;
            }
        }
    }
}
