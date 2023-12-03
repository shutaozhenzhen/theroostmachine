using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;
using System;
using UnityEngine;

namespace Roost.World.Beauty
{
    public class WindowStyle : AbstractEntity<WindowStyle>
    {
        [FucineValue(DefaultValue = null)] public string BodyColor { get; set; }
        public Color? _BodyColor => LookAndFeelMaster.HexToColor(BodyColor);

        [FucineValue(DefaultValue = null)] public string HeaderColor { get; set; }
        public Color? _HeaderColor => LookAndFeelMaster.HexToColor(HeaderColor);

        [FucineValue(DefaultValue = null)] public string SubFooterColor { get; set; }
        public Color? _SubFooterColor => LookAndFeelMaster.HexToColor(SubFooterColor);

        [FucineValue(DefaultValue = null)] public string FooterColor { get; set; }
        public Color? _FooterColor => LookAndFeelMaster.HexToColor(FooterColor);

        [FucineValue(DefaultValue = null)] public string BodyImage { get; set; }
        [FucineValue(DefaultValue = null)] public string HeaderImage { get; set; }
        [FucineValue(DefaultValue = null)] public string FooterImage { get; set; }

        [FucineEverValue(DefaultValue = null)] public ButtonStyle ActionButton { get; set; }
        [FucineEverValue(DefaultValue = null)] public ButtonStyle CloseButton { get; set; }
        [FucineEverValue(DefaultValue = null)] public NoteStyle Note { get; set; }

        public WindowStyle() { }
        public WindowStyle(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public WindowStyle OverrideWith(WindowStyle moreSpecificStyle)
        {
            if (moreSpecificStyle == null) return this;
            WindowStyle mergedStyle = this.MemberwiseClone() as WindowStyle;

            LegacyTheme theme = LookAndFeelMaster.GetCurrentTheme();

            mergedStyle.BodyColor = theme?.BodyColor ?? moreSpecificStyle?.BodyColor ?? mergedStyle.BodyColor;
            mergedStyle.HeaderColor = theme?.HeaderColor ?? moreSpecificStyle?.HeaderColor ?? mergedStyle.HeaderColor;
            mergedStyle.SubFooterColor = theme?.HeaderColor ?? moreSpecificStyle?.SubFooterColor ?? mergedStyle.SubFooterColor;
            mergedStyle.FooterColor = theme?.FooterColor ?? moreSpecificStyle?.FooterColor ?? mergedStyle.FooterColor;
            mergedStyle.HeaderImage = theme?.HeaderImage ?? moreSpecificStyle?.HeaderImage ?? mergedStyle.HeaderImage;
            mergedStyle.BodyImage = theme?.BodyImage ?? moreSpecificStyle?.BodyImage ?? mergedStyle.BodyImage;
            mergedStyle.FooterImage = theme?.FooterImage ?? moreSpecificStyle?.FooterImage ?? mergedStyle.FooterImage;

            mergedStyle.ActionButton = (theme?.ActionButtons ?? mergedStyle?.ActionButton)
                ?.OverrideWith(mergedStyle.ActionButton)
                ?.OverrideWith(moreSpecificStyle?.ActionButton);

            mergedStyle.CloseButton = (theme?.SecondaryButtons ?? mergedStyle?.CloseButton)
                ?.OverrideWith(mergedStyle.CloseButton)
                ?.OverrideWith(moreSpecificStyle?.CloseButton);
            mergedStyle.Note = mergedStyle?.Note.OverrideWith(moreSpecificStyle?.Note);

            return mergedStyle;
        }

        public static WindowStyle DefaultFromTheme(LegacyTheme theme)
        {
            EntityData data = new EntityData();
            WindowStyle defaultStyle = new WindowStyle(data, null)
            {
                BodyColor = theme?.BodyColor,
                HeaderColor = theme?.HeaderColor,
                SubFooterColor = theme?.HeaderColor,
                FooterColor = theme?.FooterColor,
                HeaderImage = theme?.HeaderImage,
                BodyImage = theme?.BodyImage,
                FooterImage = theme?.FooterImage,
                ActionButton = theme?.ActionButtons,
                CloseButton = theme?.SecondaryButtons,
                Note = NoteStyle.DefaultFromTheme(theme)
            };
            return defaultStyle;
        }
    }
}
