using System;
using System.Collections.Generic;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

using TheRoost.Invocations;

namespace TheRoost.Examples
{
    //example class - needs to have constructor and OnPostImportForSpecificEntity(); otherwise will not load
    [FucineImportable("phony")]
    public class PhonyFucineClass : AbstractEntity<PhonyFucineClass>
    {
        [FucineValue(DefaultValue = "")]
        public string id { get; set; }
        [FucineValue(DefaultValue = 0)]
        public int number { get; set; }
        [FucineList]
        public List<string> list { get; set; }
        [FucineDict]
        public Dictionary<string, int> dict { get; set; }

        public PhonyFucineClass(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        void Examples()
        {
            //to add a custom property
            TheRoost.ClaimProperty<SecretHistories.Entities.Verb>("someProperty", typeof(string));

            //to add a custom class
            TheRoost.InfectFucineWith<PhonyFucineClass>();

            //to add a custom property for a custom class
            TheRoost.ClaimProperty<PhonyFucineClass>("someProperty", typeof(int));

            //to get the property's value
            this.RetrieveProperty<int>("someProperty");
        }
    }
}