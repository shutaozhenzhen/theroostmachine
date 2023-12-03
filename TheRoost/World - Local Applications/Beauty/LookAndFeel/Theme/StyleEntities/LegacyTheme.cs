using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;
using UnityEngine;

namespace Roost.World.Beauty
{
    public class LegacyTheme : AbstractEntity<LegacyTheme>
    {
        [FucineEverValue(DefaultValue = null)] public string BodyColor { get; set; }
        public Color? _BodyColor => LookAndFeelMaster.HexToColor(BodyColor);

        [FucineEverValue(DefaultValue = null)] public string HeaderColor { get; set; }
        public Color? _HeaderColor => LookAndFeelMaster.HexToColor(HeaderColor);

        [FucineEverValue(DefaultValue = null)] public string FooterColor { get; set; }
        public Color? _FooterColor => LookAndFeelMaster.HexToColor(FooterColor);

        [FucineEverValue(DefaultValue = null)] public ButtonStyle ActionButtons { get; set; }
        [FucineEverValue(DefaultValue = null)] public ButtonStyle SecondaryButtons { get; set; }

        [FucineEverValue(DefaultValue = null)] public string HeaderImage { get; set; }
        [FucineEverValue(DefaultValue = null)] public string BodyImage { get; set; }
        [FucineEverValue(DefaultValue = null)] public string FooterImage { get; set; }

        [FucineEverValue] public VerbStyle Verbs { get; set; }
        [FucineEverValue(DefaultValue = null)] public VerbStyle TemporaryVerbs { get; set; }

        [FucineValue(DefaultValue = null)] public string StatusBarImage { get; set; }
        [FucineValue(DefaultValue = null)] public string StatusBarColor { get; set; }
        public Color? _StatusBarColor => LookAndFeelMaster.HexToColor(StatusBarColor);

        VerbStyle _defaultVerbStyle;
        public VerbStyle DefaultVerbStyle => _defaultVerbStyle;

        public LegacyTheme(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) 
        {
            _defaultVerbStyle = new VerbStyle(new EntityData(), null)
            {
                Window = WindowStyle.DefaultFromTheme(this)
            };
        }


    }
}
