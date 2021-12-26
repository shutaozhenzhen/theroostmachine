﻿using System.Collections.Generic;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

namespace TheRoost.Entities
{
    //example class - needs to have constructor and OnPostImportForSpecificEntity(); otherwise will not load
    [FucineImportable("phony")]
    public class PhonyFucineClass : AbstractEntity<PhonyFucineClass>
    {
        [FucineValue(DefaultValue = "")]
        public string text { get; set; }
        [FucineValue(DefaultValue = 0)]
        public int number { get; set; }
        [FucineList]
        public List<string> list { get; set; }
        [FucineDict]
        public Dictionary<int, int> dict { get; set; }

        public PhonyFucineClass(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        void Examples()
        {
            //to add a custom property
            Beachcomber.ClaimProperty<SecretHistories.Entities.Verb, string>("someProperty");

            //to add a custom property for a custom class
            Beachcomber.ClaimProperty<PhonyFucineClass, int>("someProperty");

            //to get the property value
            this.RetrieveProperty<int>("someProperty");
        }
    }
}