using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;
using UnityEngine;

namespace Roost.World.Beauty
{
    public class VerbStyle : AbstractEntity<VerbStyle>
    {
        // Window styling
        [FucineEverValue(DefaultValue = null)] public WindowStyle Window { get; set; }

        // Token styling
        // Border
        [FucineValue(DefaultValue = null)] public string BorderImage { get; set; }
        
        // Warmup
        [FucineValue(DefaultValue = null)] public string WarmupColor { get; set; }
        public Color? _WarmupColor => LookAndFeelMaster.HexToColor(WarmupColor);

        [FucineValue(DefaultValue = null)] public string WarmupShadowColor { get; set; }
        public Color? _WarmupShadowColor => LookAndFeelMaster.HexToColor(WarmupShadowColor);

        [FucineValue(DefaultValue = null)] public string WarmupBadgeColor { get; set; }
        public Color? _WarmupBadgeColor => LookAndFeelMaster.HexToColor(WarmupBadgeColor);

        // Token Slot
        /*[FucineValue(DefaultValue = "")] public string Slot { get; set; }

        [FucineValue(DefaultValue = "")] public string SlotColor { get; set; }
        public Color _SlotColor => LookAndFeelMaster.HexToColor(SlotColor);*/

        // Dump button
        [FucineValue(DefaultValue = "")] public string DumpButton { get; set; }

        [FucineValue(DefaultValue = null)] public string DumpButtonColor { get; set; }
        public Color? _DumpButtonColor => LookAndFeelMaster.HexToColor(DumpButtonColor);

        [FucineValue(DefaultValue = null)] public string DumpButtonHoverColor { get; set; }
        public Color? _DumpButtonHoverColor => LookAndFeelMaster.HexToColor(DumpButtonHoverColor);

        public VerbStyle() { }
        public VerbStyle(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public static VerbStyle OverrideWith(VerbStyle baseStyle, VerbStyle overrideStyle)
        {
            if (baseStyle == null)
                return overrideStyle;

            if (overrideStyle == null)
                return baseStyle;

            VerbStyle mergedStyle = baseStyle.MemberwiseClone() as VerbStyle;

            mergedStyle.Window = WindowStyle.OverrideWith(mergedStyle.Window, overrideStyle.Window);

            mergedStyle.BorderImage = overrideStyle.BorderImage ?? mergedStyle.BorderImage;
            mergedStyle.WarmupColor = overrideStyle.WarmupColor ?? mergedStyle.WarmupColor;
            mergedStyle.WarmupShadowColor = overrideStyle.WarmupShadowColor ?? mergedStyle.WarmupShadowColor;
            mergedStyle.WarmupBadgeColor = overrideStyle.WarmupBadgeColor ?? mergedStyle.WarmupBadgeColor;

            mergedStyle.DumpButton = overrideStyle.DumpButton ?? mergedStyle.DumpButton;
            mergedStyle.DumpButtonColor = overrideStyle.DumpButtonColor ?? mergedStyle.DumpButtonColor;
            mergedStyle.DumpButtonHoverColor = overrideStyle.DumpButtonHoverColor ?? mergedStyle.DumpButtonHoverColor;

            return mergedStyle;
        }
    }
}
