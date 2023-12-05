using Roost.Twins.Entities;
using Roost.World.Beauty;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

using UnityEngine;

namespace Roost.Beauty
{
    public class OverlayEntity : AbstractEntity<OverlayEntity>
    {
        // Expression used to check, based on the element's aspects, if the overlay should be displayed or not.
        // If left undefined, the overlay will always be applied.
        [FucineEverValue(DefaultValue = FucineExp<bool>.UNDEFINED)]
        public FucineExp<bool> Expression { get; set; }

        // The name of the image to display. Picked from the images/elements/ folder.
        [FucineValue(DefaultValue = "_x")]
        public string Image { get; set; }

        // Id of the layer. If left undefined, the code will assign an incrementing Overlay_X layer id.
        // When the overlay applying process is executed, two overlays cannot use the same layer id. Once a layer id is used, next overlays using the
        // same id are skipped. You can use that to write sets of overlays switching between two visuals based on a condition.
        [FucineValue(DefaultValue = null)]
        public string Layer { get; set; }

        // The "color". If Grayscale is false, it will tint the image. If set to true, it will tint and control the saturation of the image.
        [FucineValue(DefaultValue = null)]
        public string Color { get; set; }

        private Color? _color;

        // Controls the way color is applied (by picking a different material). Allows to desaturate the overlay image.
        [FucineValue(DefaultValue = false)]
        public bool Grayscale { get; set; }

        public OverlayEntity(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }

        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            _color = LookAndFeelMaster.HexToColor(Color);
        }

        public Color GetColor()
        {
            return _color ?? UnityEngine.Color.white;
        }
    }
}
