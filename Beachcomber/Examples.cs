using System;
using System.Collections.Generic;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

namespace Hoard.Examples
{
    //example class - needs to have constructor and OnPostImportForSpecificEntity()
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

        void ExampleRegister()
        {
            //to add custom class
            Beachcomber.InfectFucineWith<PhonyFucineClass>();
            //to add custom property
            Beachcomber.ClaimProperty<SecretHistories.Entities.Verb>("someProperty", typeof(string));
            //even on the custom class
            Beachcomber.ClaimProperty<PhonyFucineClass>("someProperty", typeof(string));
        }
    }
}