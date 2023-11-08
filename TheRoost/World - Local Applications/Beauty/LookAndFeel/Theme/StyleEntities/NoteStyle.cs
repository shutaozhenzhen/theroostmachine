using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;
using UnityEngine;

namespace Roost.World.Beauty
{
    public class NoteStyle : AbstractEntity<NoteStyle>
    {
        [FucineValue(DefaultValue = null)] public string BackgroundImage { get; set; }

        [FucineValue(DefaultValue = null)] public string Color { get; set; }
        public Color? _Color => LookAndFeelMaster.HexToColor(Color);

        [FucineEverValue(DefaultValue =null)] public ButtonStyle PreviousButton { get; set; }
        [FucineEverValue(DefaultValue = null)] public ButtonStyle NextButton { get; set; }

        public NoteStyle() { }
        public NoteStyle(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public NoteStyle OverrideWith(NoteStyle moreSpecificStyle)
        {
            if (moreSpecificStyle == null) return this;
            NoteStyle mergedStyle = this.MemberwiseClone() as NoteStyle;

            mergedStyle.BackgroundImage = moreSpecificStyle?.BackgroundImage ?? mergedStyle.BackgroundImage;
            mergedStyle.Color = moreSpecificStyle?.Color ?? mergedStyle.Color;
            mergedStyle.PreviousButton = (LookAndFeelMaster.GetCurrentTheme()?.SecondaryButtons ?? mergedStyle?.PreviousButton)
                ?.OverrideWith(mergedStyle.PreviousButton)
                ?.OverrideWith(moreSpecificStyle?.PreviousButton);

            mergedStyle.NextButton = (LookAndFeelMaster.GetCurrentTheme()?.SecondaryButtons ?? mergedStyle?.NextButton)
                ?.OverrideWith(mergedStyle.NextButton)
                ?.OverrideWith(moreSpecificStyle?.NextButton);

            return mergedStyle;
        }

        public static NoteStyle DefaultFromTheme(LegacyTheme theme)
        {
            EntityData data = new();
            NoteStyle defaultStyle = new(data, null)
            {
                Color = theme?.HeaderColor,
                PreviousButton = theme?.SecondaryButtons,
                NextButton = theme?.SecondaryButtons
            };
            return defaultStyle;
        }
    }
}
