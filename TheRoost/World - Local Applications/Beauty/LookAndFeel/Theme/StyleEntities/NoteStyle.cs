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

        public static NoteStyle OverrideWith(NoteStyle baseStyle, NoteStyle moreSpecificStyle)
        {
            if (baseStyle == null)
                return moreSpecificStyle;

            if (moreSpecificStyle == null) 
                return baseStyle;

            NoteStyle mergedStyle = baseStyle.MemberwiseClone() as NoteStyle;

            var theme = LookAndFeelMaster.GetCurrentTheme();

            mergedStyle.BackgroundImage = moreSpecificStyle.BackgroundImage ?? mergedStyle.BackgroundImage;
            mergedStyle.Color = moreSpecificStyle.Color ?? mergedStyle.Color;

            mergedStyle.PreviousButton = ButtonStyle.OverrideWith(theme?.SecondaryButtons, mergedStyle.PreviousButton);
            mergedStyle.PreviousButton = ButtonStyle.OverrideWith(mergedStyle.PreviousButton, moreSpecificStyle.PreviousButton);

            mergedStyle.NextButton = ButtonStyle.OverrideWith(theme?.SecondaryButtons, mergedStyle.NextButton);
            mergedStyle.NextButton = ButtonStyle.OverrideWith(mergedStyle.NextButton, moreSpecificStyle.NextButton);

            return mergedStyle;
        }

        public static NoteStyle DefaultFromTheme(LegacyTheme theme)
        {
            EntityData data = new EntityData();
            NoteStyle defaultStyle = new NoteStyle(data, null)
            {
                Color = theme?.HeaderColor,
                PreviousButton = theme?.SecondaryButtons,
                NextButton = theme?.SecondaryButtons
            };
            return defaultStyle;
        }
    }
}
