using System;
using System.Collections;
using System.Collections.Generic;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;


namespace Roost.World.Recipes.Entities
{    
    internal static class MoldingsStorage
    {
        internal static void ConvertLegacyMutations(EntityData entityData)
        {
            try
            {
                if (entityData.ValuesTable.ContainsKey("mutations") && entityData.ValuesTable["mutations"] is ArrayList)
                {
                    ArrayList oldMutations = entityData.ValuesTable["mutations"] as ArrayList;
                    EntityData newMutations = new EntityData();

                    foreach (EntityData mutation in oldMutations)
                    {
                        object filter;
                        if (mutation.ContainsKey("limit"))
                        {
                            filter = new EntityData();
                            (filter as EntityData).ValuesTable.Add("filter", mutation.ValuesTable["filter"]);
                            (filter as EntityData).ValuesTable.Add("limit", mutation.ValuesTable["limit"]);
                        }
                        else
                            filter = mutation.ValuesTable["filter"].ToString();

                        if (newMutations.ContainsKey(filter) == false)
                            newMutations.ValuesTable[filter] = new ArrayList();

                        (newMutations.ValuesTable[filter] as ArrayList).Add(mutation);
                        mutation.ValuesTable.Remove("filter");
                    }

                    entityData.ValuesTable["mutations"] = newMutations;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        internal static void ConvertExpulsionFilters(EntityData data)
        {
            try
            {
                EntityData filters = data.GetEntityDataFromEntityData("filter");
                if (filters != null)
                {
                    string singleFilter = string.Empty;
                    foreach (DictionaryEntry filter in filters.ValuesTable)
                        singleFilter += $"{AsExpression(filter.Key)}>={AsExpression(filter.Value)}||";

                    singleFilter = singleFilter.Remove(singleFilter.Length - 2);
                    data["filter"] = singleFilter;
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
