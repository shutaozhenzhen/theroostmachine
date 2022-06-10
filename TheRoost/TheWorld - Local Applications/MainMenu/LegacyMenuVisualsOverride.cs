using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using UnityEngine;

public class LegacyMenuVisualsOverride : AbstractEntity<LegacyMenuVisualsOverride>
{
    [FucineValue(DefaultValue = "mainmenu_bg")] public string mmBackground { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_people")] public string mmBackgroundPeople { get; set; }
    [FucineConstruct(0.0, 0.0)] public Vector2 mmBackgroundPeoplePosition { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_occultwind")] public string mmBackgroundOccultWind { get; set; }
    [FucineConstruct(50.56, -20.38)] public Vector2 mmBackgroundOccultWindPosition { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_lightray")] public string mmBackgroundLightray { get; set; }
    [FucineConstruct(-124.35, 0.0)] public Vector2 mmBackgroundLightrayPosition { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_character")] public string mmBackgroundCharacter { get; set; }
    [FucineConstruct(31.56, -57.46)] public Vector2 mmBackgroundCharacterPosition { get; set; }

    // Particles
    [FucineValue(DefaultValue = "mainmenu_bg_floatingglyphs1")] public string mmBackgroundFloatingGlyphs1 { get; set; }
    [FucineValue(DefaultValue = "SkyHolder")] public string mmBackgroundFloatingGlyphs1Parent { get; set; }
    [FucineConstruct(-91.19, 0)] public Vector2 mmBackgroundFloatingGlyphs1Position { get; set; }
    [FucineConstruct(0, 0)] public Vector2 mmBackgroundFloatingGlyphs1RotationMinMax { get; set; }
    [FucineConstruct(0.963, 0.994, 1, 1)] public Color mmBackgroundFloatingGlyphs1Color { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_floatingglyphs2")] public string mmBackgroundFloatingGlyphs2 { get; set; }
    [FucineValue(DefaultValue = "IrisPlaceholder")] public string mmBackgroundFloatingGlyphs2Parent { get; set; }
    [FucineConstruct(35.15, -12.98)] public Vector2 mmBackgroundFloatingGlyphs2Position { get; set; }
    [FucineConstruct(0, 0)] public Vector2 mmBackgroundFloatingGlyphs2RotationMinMax { get; set; }
    [FucineConstruct(0.963, 0.994, 1.000, 1.000)] public Color mmBackgroundFloatingGlyphs2Color { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_ashflakes1")] public string mmBackgroundAshFlakes1 { get; set; }
    [FucineValue(DefaultValue = "PeepHolder")] public string mmBackgroundAshFlakes1Parent { get; set; }
    [FucineConstruct(-22.28, 0)] public Vector2 mmBackgroundAshFlakes1Position { get; set; }
    [FucineConstruct(0, 6.283185)] public Vector2 mmBackgroundAshFlakes1RotationMinMax { get; set; }
    [FucineConstruct(1.000, 1.000, 1.000, 1.000)] public Color mmBackgroundAshFlakes1Color { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_ashflakes2")] public string mmBackgroundAshFlakes2 { get; set; }
    [FucineValue(DefaultValue = "PeepHolder")] public string mmBackgroundAshFlakes2Parent { get; set; }
    [FucineConstruct(-83.46, 5.60)] public Vector2 mmBackgroundAshFlakes2Position { get; set; }
    [FucineConstruct(0, 6.283185)] public Vector2 mmBackgroundAshFlakes2RotationMinMax { get; set; }
    [FucineConstruct(1.000, 1.000, 1.000, 1.000)] public Color mmBackgroundAshFlakes2Color { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_ashflakes3")] public string mmBackgroundAshFlakes3 { get; set; }
    [FucineValue(DefaultValue = "PeepHolder")] public string mmBackgroundAshFlakes3Parent { get; set; }
    [FucineConstruct(-59.89, -7.14)] public Vector2 mmBackgroundAshFlakes3Position { get; set; }
    [FucineConstruct(0, 6.283185)] public Vector2 mmBackgroundAshFlakes3RotationMinMax { get; set; }
    [FucineConstruct(1.000, 1.000, 1.000, 1.000)] public Color mmBackgroundAshFlakes3Color { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_eyeglow")] public string mmBackgroundEyeGlow { get; set; }
    [FucineValue(DefaultValue = "GlowSpikes (2)")] public string mmBackgroundEyeGlowParent { get; set; }
    [FucineConstruct(42.16, 6.94)] public Vector2 mmBackgroundEyeGlowPosition { get; set; }
    [FucineConstruct(0, 0)] public Vector2 mmBackgroundEyeGlowRotationMinMax { get; set; }
    [FucineConstruct(0.690, 0.310, 0.600, 1.000)] public Color mmBackgroundEyeGlowColor { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_eyeflare")] public string mmBackgroundEyeFlare { get; set; }
    [FucineValue(DefaultValue = "GlowSpikes (2)")] public string mmBackgroundEyeFlareParent { get; set; }
    [FucineConstruct(42.16, 6.94)] public Vector2 mmBackgroundEyeFlarePosition { get; set; }
    [FucineConstruct(0, 1.570796)] public Vector2 mmBackgroundEyeFlareRotationMinMax { get; set; }
    [FucineConstruct(0.690, 0.310, 0.600, 1.000)] public Color mmBackgroundEyeFlareColor { get; set; }

    [FucineValue(DefaultValue = "mainmenu_bg_eyeeffect")] public string mmBackgroundEyeEffect { get; set; }
    [FucineValue(DefaultValue = "Iris")] public string mmBackgroundEyeEffectParent { get; set; }

    [FucineConstruct(42.16, 6.94)] public Vector2 mmBackgroundEyeEffectPosition { get; set; }
    [FucineConstruct(0.2617994, 0.6108652)] public Vector2 mmBackgroundEyeEffectRotationMinMax { get; set; }
    [FucineConstruct(1.000, 1.000, 1.000, 1.000)] public Color mmBackgroundEyeEffectColor { get; set; }


    public LegacyMenuVisualsOverride(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
    protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }
}
