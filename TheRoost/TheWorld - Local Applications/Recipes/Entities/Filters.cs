using System;
using System.Collections;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Entities;

using Roost.Twins.Entities;

namespace Roost.World.Recipes.Entities
{   
    public class TokenFilterSpec : AbstractEntity<TokenFilterSpec>, IQuickSpecEntity, IMalleable
    {
        [FucineConstruct(FucineExp<int>.UNDEFINED)] public FucineExp<bool> Filter { get; set; }
        [FucineConstruct(FucineExp<int>.UNDEFINED)] public FucineExp<int> Limit { get; set; } //unlimited by default

        public static void Enact()
        {
            Machine.AddImportMolding<Expulsion>(ConvertExpulsionFilters);
        }

        public TokenFilterSpec() { }
        public TokenFilterSpec(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            if (Filter.isUndefined)
            {
                foreach (object key in UnknownProperties.Keys)
                {
                    this.Filter = new FucineExp<bool>(key.ToString());
                    this.Limit = new FucineExp<int>(UnknownProperties[key].ToString());
                    break;
                }

                if (Filter.isUndefined)
                    log.LogWarning("FILTER IS UNDEFINED");
                else
                    UnknownProperties.Remove(this.Filter.formula);
            }
        }

        public List<Token> GetTokens(List<Token> tokens)
        {
            List<Token> filteredTokens = tokens.FilterTokens(Filter);

            if (!Limit.isUndefined)
                return filteredTokens.SelectRandom(Limit.value);

            return filteredTokens;
        }

        public void QuickSpec(string data)
        {
            try
            {
                Filter = new FucineExp<bool>(data);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void Mold(EntityData data, ContentImportLog log)
        {
            try
            {
                ConvertExpulsionFilters(data);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        static void ConvertExpulsionFilters(EntityData data)
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