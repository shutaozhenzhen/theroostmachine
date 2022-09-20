using System;
using System.Collections;

using OrbCreationExtensions;
using SecretHistories.Fucine.DataImport;


namespace Roost.World.Recipes.Entities
{
    internal static class MoldingsStorage
    {

        internal static void ConvertLegacyMutations(EntityData recipeEntityData)
        {
            return;
            const string FILTER = "filter";
            const string LIMIT = "limit";
            const string MUTATIONS = "mutations";

            try
            {
                if (recipeEntityData.ValuesTable.ContainsKey(MUTATIONS))
                {
                    if (recipeEntityData.ValuesTable[MUTATIONS] is ArrayList)
                    {
                        ArrayList oldMutations = recipeEntityData.ValuesTable[MUTATIONS] as ArrayList;
                        EntityData newMutations = new EntityData();
                        recipeEntityData.ValuesTable[MUTATIONS] = newMutations;

                        foreach (EntityData mutation in oldMutations)
                        {
                            object filter;
                            if (mutation.ContainsKey(LIMIT))
                            {
                                filter = new EntityData();
                                (filter as EntityData).ValuesTable.Add(FILTER, mutation.ValuesTable[FILTER]);
                                (filter as EntityData).ValuesTable.Add(LIMIT, mutation.ValuesTable[LIMIT]);
                            }
                            else
                                filter = mutation.ValuesTable[FILTER].ToString();

                            mutation.ValuesTable.Remove(FILTER);
                            mutation.ValuesTable.Remove(LIMIT);

                            if (newMutations.ContainsKey(filter) == false)
                                newMutations.ValuesTable[filter] = new ArrayList();

                            (newMutations.ValuesTable[filter] as ArrayList).Add(mutation);
                        }
                    }
                    else if (recipeEntityData.ValuesTable[MUTATIONS] is EntityData)
                    {
                        EntityData mutations = recipeEntityData.ValuesTable[MUTATIONS] as EntityData;
                        foreach (object filter in new Hashtable(mutations.ValuesTable).Keys)
                        {
                            ArrayList mutationsInFilter = mutations.GetArrayListFromEntityData(filter);
                            mutations[filter] = mutationsInFilter;

                            foreach (object mutation in new ArrayList(mutationsInFilter))
                            {
                                EntityData mutationData = mutation as EntityData;

                                if (mutationData == null)
                                    continue;

                                if (!mutationData.ContainsKey(LIMIT))
                                    continue;

                                //each limit requires new filter
                                EntityData filterData = new EntityData();
                                filterData[FILTER] = filter;
                                filterData[LIMIT] = mutationData[LIMIT];

                                mutationData.ValuesTable.Remove(LIMIT);
                                mutationsInFilter.Remove(mutation);
                                mutations.ValuesTable.Add(filterData, mutationData);
                            }

                            if (mutationsInFilter.Count == 0)
                                mutations.ValuesTable.Remove(filter);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        internal static ArrayList GetArrayListFromEntityData(this EntityData data, object key)
        {
            return data.ValuesTable.GetArrayList(key, true) ?? new ArrayList();
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
