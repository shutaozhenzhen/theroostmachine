using Roost.Twins.Entities;
using SecretHistories.Constants;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Roost.World.Shelves
{
    class ShelfArea : AbstractEntity<ShelfArea>
    {
        [FucineValue(DefaultValue = 1)] public int X { get; set; }
        [FucineValue(DefaultValue = 1)] public int Y { get; set; }
        [FucineValue(DefaultValue = 1)] public int Rows { get; set; }
        [FucineValue(DefaultValue = 1)] public int Columns { get; set; }
        [FucineValue(DefaultValue = "")] public string Background { get; set; }
        [FucineEverValue(DefaultValue = FucineExp<bool>.UNDEFINED)] public FucineExp<bool> Expression { get; set; }

        public ShelfArea(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) {
        }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) {}
    }

    /*
     * A Shelf is an area:
     * - defined by N rows and M columns, 1, 1 by default
     * - an optional parent offset, 0 0 by default
     * - a filter property, to define the elements moved to here
     * - children areas
     * - an outline colour
     */
    [FucineImportable("shelves")]
    class Shelf : AbstractEntity<Shelf>
    {
        [FucineValue(DefaultValue = 1)] public int Rows { get; set; }
        [FucineValue(DefaultValue = 1)] public int Columns { get; set; }
        [FucineValue(DefaultValue = "")] public string Background { get; set; }
        [FucineEverValue(DefaultValue = FucineExp<bool>.UNDEFINED)] public FucineExp<bool> Expression { get; set; }
        [FucineList] public List<ShelfArea> Areas {get; set;}

        [FucineValue(DefaultValue = false)] public bool NoOutline { get; set; }

        public Shelf(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) {}
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) {}
    }
}
