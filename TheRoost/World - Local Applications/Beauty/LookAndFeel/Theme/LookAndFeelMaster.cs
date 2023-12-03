using Roost.Vagabond;
using SecretHistories;
using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Manifestations;
using SecretHistories.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Roost.World.Beauty
{
    static class LookAndFeelMaster
    {
        public static string THEME_PROPERTY = "theme";
        public static string STYLE_PROPERTY = "style";

        public static Color? HexToColor(string hexCode)
        {
            if (hexCode == null) return null;
            ColorUtility.TryParseHtmlString(hexCode, out Color c);
            return c;
        }

        internal static void Enact()
        {
            Machine.ClaimProperty<Legacy, LegacyTheme>(THEME_PROPERTY);
            Machine.ClaimProperty<Verb, VerbStyle>(STYLE_PROPERTY);

            AtTimeOfPower.TabletopSceneInit.Schedule(ApplyThemeToUI, PatchType.Postfix);

            Machine.Patch(
                original: typeof(VerbManifestation).GetMethodInvariant(nameof(VerbManifestation.Initialise), typeof(IManifestable)),
                postfix: typeof(LookAndFeelMaster).GetMethodInvariant(nameof(PatchSituationTokenOnSpawn))
            );

            Machine.Patch(
                original: typeof(SituationWindow).GetMethodInvariant(nameof(SituationWindow.Attach), typeof(Situation)),
                postfix: typeof(LookAndFeelMaster).GetMethodInvariant(nameof(PatchSituationWindowOnSpawn))
            );
        }

        public static LegacyTheme GetCurrentTheme()
        {
            Legacy legacy = Watchman.Get<Stable>().Protag().ActiveLegacy;
            return legacy.RetrieveProperty<LegacyTheme>(THEME_PROPERTY);
        }

        public static void ApplyThemeToUI()
        {
            LegacyTheme theme = GetCurrentTheme();
            if (theme == null) return;

            // Apply the theme to the in-game UI elements
            GameObject statusBarBG = GameObject.Find("StatusBar").FindInChildren("ButtonBG");
            ReplaceSprite(statusBarBG, theme.StatusBarImage);
            ReplaceColor(statusBarBG, theme._StatusBarColor);


            // Apply the theme to the Esc menu
            GameObject window = GameObject.Find("CanvasMeta").FindInChildren("OptionsPanel ", true);

            ReplaceColor(window.FindInChildren("BG_Title"), theme._HeaderColor);
            ReplaceColor(window.FindInChildren("BG_Body"), theme._BodyColor);
            ReplaceColor(window.FindInChildren("BG_Footer"), theme._FooterColor);
            ApplyStyleToButton(window.FindInChildren("CloseButton", true), theme.SecondaryButtons);

            GameObject settingsList = window.FindInChildren("SettingsPanel", true);
            foreach(Transform settingTransform in settingsList.transform)
            {
                GameObject setting = settingTransform.gameObject;
                if(setting.name.StartsWith("SliderSetting") && theme?.SecondaryButtons?._Color != null)
                    setting.FindInChildren("Fill").GetComponent<Image>().color = theme.SecondaryButtons._Color.Value;
            }
        }

        public static VerbStyle GetComputedVerbStyle(string verbId)
        {
            Verb verbEntity = Watchman.Get<Compendium>().GetEntityById<Verb>(verbId);
            VerbStyle verbSpecificStyle = verbEntity.RetrieveProperty<VerbStyle>(STYLE_PROPERTY);

            LegacyTheme theme = GetCurrentTheme();
            VerbStyle baseVerbStyle = theme?.Verbs;
            VerbStyle defaultStyle = theme?.DefaultVerbStyle;

            if (baseVerbStyle == null && verbSpecificStyle == null && defaultStyle == null) 
                return null;

            /*
            Birdsong.Sing("Verb id:", verbId);
            Birdsong.Sing("Default verbstyle:", defaultStyle != null);
            Birdsong.Sing("Base verbstyle:", baseVerbStyle != null);
            Birdsong.Sing("Specific verbstyle:", verbSpecificStyle != null);*/
            VerbStyle computedStyle = defaultStyle.OverrideWith(baseVerbStyle).OverrideWith(verbSpecificStyle);
            return computedStyle;
        }

        public static void PatchSituationTokenOnSpawn(MonoBehaviour __instance, IManifestable manifestable)
        {
            // Compute the verb's style and apply it to the token
            // Get the unique VerbStyle, merge it with the base theme's verb style, or if it doesn't exist, with itself. If none exist, return.
            VerbManifestation verb = __instance as VerbManifestation;
            VerbStyle style = GetComputedVerbStyle(manifestable.EntityId);
            if (style == null) return;

            // For each property, if not null, apply it in the right way
            ReplaceSprite(verb.gameObject.FindInChildren("Token"), style?.BorderImage);

            ReplaceColor(verb.gameObject.FindInChildren("CountdownBar", true), style?._WarmupColor);
            ReplaceColor(verb.gameObject.FindInChildren("CoundownBarShadow", true), style?._WarmupShadowColor);
            ReplaceColor(verb.gameObject.FindInChildren("CountdownBadge", true), style?._WarmupBadgeColor);

            ReplaceSprite(verb.gameObject.FindInChildren("DumpButton", true), style?.DumpButton);

            if (style?._DumpButtonColor != null)
            {
                SituationTokenDumpButton comp = verb.gameObject.FindInChildren("CountdownBar", true).GetComponent<SituationTokenDumpButton>();
                typeof(SituationTokenDumpButton).GetPropertyInvariant("buttonColorDefault").SetValue(comp, style?._DumpButtonColor.Value);
            }
            if (style?._DumpButtonHoverColor != null)
            {
                SituationTokenDumpButton comp = verb.gameObject.FindInChildren("CountdownBar", true).GetComponent<SituationTokenDumpButton>();
                typeof(SituationTokenDumpButton).GetPropertyInvariant("buttonColorHover").SetValue(comp, style?._DumpButtonHoverColor.Value);
            }
        }

        public static void PatchSituationWindowOnSpawn(MonoBehaviour __instance, Situation newSituation)
        {
            GameObject window = (__instance as SituationWindow).gameObject;
            VerbStyle verbStyle = GetComputedVerbStyle(newSituation.EntityId);
            WindowStyle style = verbStyle?.Window;
            if (style == null) return;

            ReplaceColor(window.FindInChildren("BG_Body", true), style._BodyColor);
            ReplaceSprite(window.FindInChildren("BG_Body", true), style.BodyImage);

            ReplaceColor(window.FindInChildren("BG_Title", true), style._HeaderColor);
            ReplaceSprite(window.FindInChildren("BG_Title", true), style.HeaderImage);

            ReplaceColor(window.FindInChildren("AspectsDisplay", true), style._SubFooterColor);

            GameObject footer = window.FindInChildren("Footer", true).FindInChildren("Footer", true);
            ReplaceColor(footer, style._FooterColor);
            ReplaceSprite(footer, style.FooterImage);

            ApplyStyleToButton(window.FindInChildren("StartButton", true), style.ActionButton);
            ApplyStyleToButton(window.FindInChildren("CloseButton", true), style.CloseButton);
            ApplyStyleToNote(window.FindInChildren("NotesSphere_NotesSphere", true), style.Note);
        }

        public static void ApplyStyleToButton(GameObject go, ButtonStyle style)
        {
            if (style == null || go == null) return;
            Button b = go.GetComponent<Button>();
            ColorBlock cb = b.colors;
            cb.normalColor = style._Color ?? b.colors.normalColor;
            cb.highlightedColor = style._HighlightedColor ?? b.colors.highlightedColor;
            cb.pressedColor = style._PressedColor ?? b.colors.pressedColor;
            cb.selectedColor = style._SelectedColor ?? b.colors.selectedColor;
            cb.disabledColor = style._DisabledColor ?? b.colors.disabledColor;
            b.colors = cb;
            ReplaceSprite(go, style.BackgroundImage);
        }

        public static void ApplyStyleToNote(GameObject go, NoteStyle style)
        {
            if (style == null || go == null) return;
            ReplaceSprite(go, style.BackgroundImage);
            ReplaceColor(go, style._Color);

            ApplyStyleToButton(go.FindInChildren("NotePrev", true), style.PreviousButton);
            ApplyStyleToButton(go.FindInChildren("NoteNext", true), style.NextButton);
        }

        public static void ReplaceSprite(GameObject go, string spriteName)
        {
            if (spriteName == null || go == null) return;

            Sprite sprite = ResourcesManager.GetSpriteForUI(spriteName);
            if (sprite == null) return;

            go.GetComponent<Image>().sprite = sprite;
        }

        public static void ReplaceColor(GameObject go, Color? color)
        {
            if (color == null || go == null) return;
            go.GetComponent<Image>().color = color.Value;
        }
    }
}
