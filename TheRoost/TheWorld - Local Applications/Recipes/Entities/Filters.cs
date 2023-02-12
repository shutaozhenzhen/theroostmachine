using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Logic;

using Roost.Twins.Entities;

namespace Roost.World.Recipes.Entities
{   
    public class TokenFilterSpec : AbstractEntity<TokenFilterSpec>, IQuickSpecEntity, IMalleable
    {
        [FucineConstruct(FucineExp<int>.UNDEFINED)] public FucineExp<bool> Filter { get; set; }
        [FucineConstruct(FucineExp<int>.UNDEFINED)] public FucineExp<int> Limit { get; set; } //unlimited by default

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
                MoldingsStorage.ConvertExpulsionFilters(data);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}