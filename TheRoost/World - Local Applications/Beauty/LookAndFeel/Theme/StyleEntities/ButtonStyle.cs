using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;
using UnityEngine;

namespace Roost.World.Beauty
{
    public class ButtonStyle : AbstractEntity<ButtonStyle>
    {
        [FucineValue(DefaultValue = null)] public string BackgroundImage { get; set; }

        [FucineValue(DefaultValue = null)] public string Color { get; set; }
        public Color? _Color => LookAndFeelMaster.HexToColor(Color);

        [FucineValue(DefaultValue = null)] public string HighlightedColor { get; set; }
        public Color? _HighlightedColor => LookAndFeelMaster.HexToColor(HighlightedColor);

        [FucineValue(DefaultValue = null)] public string PressedColor { get; set; }
        public Color? _PressedColor => LookAndFeelMaster.HexToColor(PressedColor);

        [FucineValue(DefaultValue = null)] public string SelectedColor { get; set; }
        public Color? _SelectedColor => LookAndFeelMaster.HexToColor(SelectedColor);

        [FucineValue(DefaultValue = null)] public string DisabledColor { get; set; }
        public Color? _DisabledColor => LookAndFeelMaster.HexToColor(DisabledColor);

        public ButtonStyle() { }
        public ButtonStyle(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public ButtonStyle OverrideWith(ButtonStyle moreSpecificStyle)
        {
            if (moreSpecificStyle == null) return this;
            ButtonStyle mergedStyle = this.MemberwiseClone() as ButtonStyle;

            mergedStyle.BackgroundImage = moreSpecificStyle?.BackgroundImage ?? mergedStyle.BackgroundImage;
            mergedStyle.Color = moreSpecificStyle?.Color ?? mergedStyle.Color;
            mergedStyle.HighlightedColor = moreSpecificStyle?.HighlightedColor ?? mergedStyle.HighlightedColor;
            mergedStyle.PressedColor = moreSpecificStyle?.PressedColor ?? mergedStyle.PressedColor;
            mergedStyle.SelectedColor = moreSpecificStyle?.SelectedColor ?? mergedStyle.SelectedColor;
            mergedStyle.DisabledColor = moreSpecificStyle?.DisabledColor ?? mergedStyle.DisabledColor;

            return mergedStyle;
        }
    }
}
