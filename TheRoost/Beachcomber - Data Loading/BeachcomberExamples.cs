using System.Collections;
using System.Collections.Generic;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

namespace TheRoost.Beachcomber.Entities
{

    //All custom classes need to have FucineImportable attribute followed by a string-tag
    //The tag is how the game understands what type of entity each JSON file loads
    //ex. Elements are annotated as [FucineImportable("elements")] which means they are loaded from JSON of the form 
    //{"elements":[ { first element definition }, { second element definition } ...etc ]}
    //Accordingly, entities of this type will be loaded from JSON of the form {"beachcomberexample":[ content ]}
    [FucineImportable("beachcomberexample")]
    //the class itself needs to derive from AbstractEntity<T> where T is the name of the class
    //IQuickSpecEntity and IWeirdSpecEntity are optional, explained below
    public class ExampleFucineClass : AbstractEntity<ExampleFucineClass>, IQuickSpecEntity, IWeirdSpecEntity
    {

        //each loadable property needs to have FucineValue attribute
        //you can add a DefaultValue (which it'll have if property isn't specified in JSON definition)
        //and whether it needs to be Localised or ValidatedAsElementId - all three are optional
        //properties are case-insenstive for purposes of JSON loading

        //as thing stand now, you don't really need to use specific annotations like FucineList, FucineDict and FucineSubEntity - FucineValue suffice
        //but you may want to use them just in case something will change

        [FucineValue(DefaultValue = 0)]
        public int Number { get; set; }

        [FucineValue(DefaultValue = "", Localise = true)]
        public string Text { get; set; }

        [FucineList(ValidateAsElementId = true)]
        public List<string> ListOfElements { get; set; }

        [FucineDict]
        public Dictionary<int, int> MyDict { get; set; }

        //you can load enums - both by int and string
        [FucineValue(DefaultValue = SecretHistories.Entities.EndingFlavour.Grand)]
        public SecretHistories.Entities.EndingFlavour MyEnum { get; set; }

        //you can even load structs; in JSON structs are defined like "myVectorProperty": [ 1, 1 ], i.e. as lists, with []
        //similarly, a default value is passed to the property as param object[]
        [FucineStruct(0.5f, 100f)]
        public UnityEngine.Vector2 Vector { get; set; }

        //finally, your entity needs to implement two methods of AbstractEntity<T> - constructor and OnPostImportForSpecificEntity()
        //both of them can remain empty but the second one is sometimes useful - it's called right after all entities were imported from JSONs and created
        public ExampleFucineClass(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        //and of course, entity can have any amount of its own methods 
        void Examples()
        {
            //to add a custom property to any entity type: TheRoostMachine.ClaimProperty<entityType, propertyType>(propertyName)
            Machine.ClaimProperty<SecretHistories.Entities.Verb, string>("someProperty");

            //can be done with a custom class too
            Machine.ClaimProperty<ExampleFucineClass, int>("someProperty");

            //to get the property value: entity.RetrieveProperty<propertyType>(propertyName)
            this.RetrieveProperty<int>("someProperty");
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

        public void WeirdSpec(Hashtable data)
        {
            //ok, this one, as its name states, is weird
            //this one allows you to shape your entity using properties from JSON that won't be recognized normally
            //I'll leave the possible use-cases to your imagination
            //(but these unknown properties, for example, can be other entity ids)
        }
    }
}
