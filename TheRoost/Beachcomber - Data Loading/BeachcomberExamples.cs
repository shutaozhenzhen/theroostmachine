using System.Collections;
using System.Collections.Generic;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

namespace Roost.Beachcomber.Entities
{

    //All custom classes need to have FucineImportable attribute followed by a string-tag
    //The tag is how the game understands what type of entity each JSON file loads
    //ex. Elements are annotated as [FucineImportable("elements")] which means they are loaded from JSON of the form 
    //{"elements":[ { first element definition }, { second element definition } ...etc ]}
    //Accordingly, entities of this type will be loaded from JSON of the form {"beachcomberexample":[ content ]}
    [FucineImportable("beachcomberexample")]
    //the class itself needs to derive from AbstractEntity<T> where T is the name of the class
    //IQuickSpecEntity and ICustomSpecEntity are optional, explained below
    public class ExampleFucineClass : AbstractEntity<ExampleFucineClass>, IQuickSpecEntity, ICustomSpecEntity
    {

        //each loadable property needs to have a corresponding FucineValue attribute
        //all struct types generally should have a default value
        
        [FucineValue(DefaultValue = 0)]
        public int Number { get; set; }
        [FucineValue(DefaultValue = "", Localise = true)]
        public string Text { get; set; }

        //enums can be loaded both by string and int
        [FucineValue(DefaultValue = SecretHistories.Entities.EndingFlavour.Grand)]
        public SecretHistories.Entities.EndingFlavour MyEnum { get; set; }

        [FucineList(ValidateAsElementId = true)]
        public List<string> ListOfElements { get; set; }
        [FucineDict]
        public Dictionary<int, int> MyDict { get; set; }

        //you can even load structs; in JSON structs are defined like "myVectorProperty": [ 1, 1 ], i.e. as lists, with []
        //similarly, a default value is passed to the property as param object[]
        [FucineConstruct(0.5f, 100f)]
        public UnityEngine.Vector2 MyVector { get; set; }

        //or, if you're lazy, just use FucineEverValue for everything; it'll recognize the type by itself, and import it correctly
        [FucineEverValue(DefaultValue = 1)]
        public int LazyInt { get; set; }
        [FucineEverValue]
        public List<ExampleFucineClass> LazyList { get; set; }
        [FucineEverValue]
        public Dictionary<int, string> LazyDict { get; set; }

        [FucineEverValue(100f, 0.5f)]
        public UnityEngine.Vector2 LazyVector { get; set; }

        //finally, your entity needs to implement two methods of AbstractEntity<T> - constructor and OnPostImportForSpecificEntity()
        //both of them can remain empty but the second one is sometimes useful - it's called right after all entities are imported
        public ExampleFucineClass(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            foreach (CachedFucineProperty<ExampleFucineClass> property in TypeInfoCache<ExampleFucineClass>.GetCachedFucinePropertiesForType())
                Birdsong.Tweet($"Example entity '{this.Id}' - '{property.ThisPropInfo.Name}': {property.ThisPropInfo.GetValue(this)}");
        }

        //and of course, entity can have any amount of its own methods 
        void Examples()
        {
            //to add a custom property to any entity type: TheRoostMachine.ClaimProperty<entityType, propertyType>(propertyName)
            Machine.ClaimProperty<SecretHistories.Entities.Verb, string>("someProperty");

            //you can add a custom property to a custom class
            Machine.ClaimProperty<ExampleFucineClass, int>("someProperty");

            //to get the property value: entity.RetrieveProperty<propertyType>(propertyName)
            this.RetrieveProperty<int>("someProperty");
            int value = (int)this.RetrieveProperty("someProperty"); //or cast directly
        }

        public void QuickSpec(string value)
        {
            //if entity implements IQuickSpec interface, it can be defined as a single string in JSON
            //(instead of the whole { "id": ... "property1": ... etc })
            //for example, the vanilla uses this for loading simple one-effect transform xtriggers, where instead of
            //"catalyst": [ { "id": "effectOne", "morphEffect": "transform" } ]
            //you can just define it as
            //"catalyst": "effectOne"
            //here in this method you interpret this single string and assign entity properties accordingly
        }

        public void CustomSpec(Hashtable data)
        {
            //some people want weird things; ICustomSpec is for you
            //it allows you to shape your entity using properties from JSON that won't be recognized normally
            //I'll leave the possible use-cases to your imagination
        }
    }
}
