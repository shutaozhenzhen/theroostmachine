using System;
using System.Collections.Generic;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

//example class - needs to have constructor and OnPostImportForSpecificEntity()
public class PhonyFucineClass : AbstractEntity<PhonyFucineClass>
{
    [FucineValue(DefaultValue = "")]
    public string label { get; set; }
    [FucineList]
    public List<string> list { get; set; }
    [FucineDict]
    public Dictionary<string, int> dict { get; set; }

    public PhonyFucineClass(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
    protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }
}