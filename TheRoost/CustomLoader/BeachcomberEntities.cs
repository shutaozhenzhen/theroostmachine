using System.Collections;
using System.Collections.Generic;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

namespace SecretHistories.Fucine
{
    public interface IFancySpecEntity
    {
        void FancySpec(Hashtable data);
    }
}

namespace TheRoost.Beachcomber.Entities
{
    //example class - needs to have constructor and OnPostImportForSpecificEntity(); otherwise will not load
    [FucineImportable("beachcomberexample")]
    public class ExampleFucineClass : AbstractEntity<ExampleFucineClass>
    {
        [FucineValue(DefaultValue = "")]
        public string text { get; set; }
        [FucineValue(DefaultValue = 0)]
        public int number { get; set; }
        [FucineList]
        public List<string> list { get; set; }
        [FucineDict]
        public Dictionary<int, int> dict { get; set; }

        public ExampleFucineClass(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        void Examples()
        {
            //to add a custom property
            Birdsong.ClaimProperty<SecretHistories.Entities.Verb, string>("someProperty");

            //to add a custom property for a custom class
            Birdsong.ClaimProperty<ExampleFucineClass, int>("someProperty");

            //to get the property value
            this.RetrieveProperty<int>("someProperty");
        }
    }
}
