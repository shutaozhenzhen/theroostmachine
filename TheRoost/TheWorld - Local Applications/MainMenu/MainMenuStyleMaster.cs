using SecretHistories.Entities;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.ParticleSystem;

namespace Roost.World
{
    class MainMenuStyleMaster
    {
        public static void setSpriteAndTransform(string objectName, Sprite sprite, Vector2 position)
        {
            GameObject gameObject = GameObject.Find(objectName);
            if (sprite != null)
            {
                RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
                float currentWidth = rectTransform.rect.width < 0 ? rectTransform.sizeDelta.x : rectTransform.rect.width;
                float currentHeight = rectTransform.rect.height < 0 ? rectTransform.sizeDelta.y : rectTransform.rect.height;

                float newWidth = (currentWidth * sprite.rect.width) / gameObject.GetComponent<Image>().sprite.rect.width;
                float newHeight = (currentHeight * sprite.rect.height) / gameObject.GetComponent<Image>().sprite.rect.height;

                gameObject.GetComponent<Image>().sprite = sprite;
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);
                gameObject.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newHeight);
            }
            gameObject.GetComponent<Transform>().position = new Vector3(position.x, position.y, 90);
        }

        public static void overrideParticleEmitter(string objectName, Sprite sprite, Vector2 position, string parent, Vector2 rotMinMax, Color newColor)
        {
            GameObject gameObject = GameObject.Find(objectName);
            var main = gameObject.GetComponent<ParticleSystem>().main;

            //Birdsong.Sing("#####################");
            //Birdsong.Sing("PS " + objectName);
            //Birdsong.Sing("Parent name: "+gameObject.transform.parent.name);
            //Birdsong.Sing("Start rotation: " + main.startRotation.constantMin+" => "+ main.startRotation.constantMax);
            //Birdsong.Sing("Color: " + main.startColor);
            //Birdsong.Sing("Is sprite null? " + (sprite == null));
            if (sprite != null) gameObject.GetComponent<ParticleSystemRenderer>().material.mainTexture = sprite.texture;

            if (!gameObject.GetComponent<RectTransform>()) gameObject.AddComponent<RectTransform>();
            if (gameObject.transform.parent.name != parent) gameObject.transform.SetParent(GameObject.Find(parent).transform, false);

            if (!main.startRotation.Equals(new MinMaxCurve(rotMinMax.x, rotMinMax.y)))
            {
                main.startRotation = new MinMaxCurve(rotMinMax.x, rotMinMax.y);
            }

            if (!main.startColor.Equals(newColor)) main.startColor = newColor;

            //Birdsong.Sing("=== Post override ===");
            //Birdsong.Sing("PS " + objectName);
            //Birdsong.Sing("Parent name: " + gameObject.transform.parent.name);
            //Birdsong.Sing("Start rotation: " + main.startRotation.constantMin + " => " + main.startRotation.constantMax);
            //Birdsong.Sing("Color: " + main.startColor);
            //Birdsong.Sing("#####################");
            gameObject.GetComponent<ParticleSystemRenderer>().transform.position = new Vector3(position.x, position.y, 90);
            gameObject.GetComponent<ParticleSystem>().Clear();
        }

        public static void setSpritesBasedOnLegacyVisualOverrides(string legacyId, LegacyMenuVisualsOverride vo)
        {
            Sprite bgSprite = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackground);
            if (bgSprite != null) GameObject.Find("SkyHolder").GetComponent<RawImage>().texture = bgSprite.texture;

            Sprite peopleSprite = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundPeople);
            Sprite occultWindSprite = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundOccultWind);
            Sprite lightraySprite = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundLightray);
            Sprite characterSprite = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundCharacter);
            setSpriteAndTransform("PeepHolder", peopleSprite, vo.mmBackgroundPeoplePosition);
            setSpriteAndTransform("OccultWind", occultWindSprite, vo.mmBackgroundOccultWindPosition);
            setSpriteAndTransform("Lightray", lightraySprite, vo.mmBackgroundLightrayPosition);
            setSpriteAndTransform("Iris", characterSprite, vo.mmBackgroundCharacterPosition);
        }

        public static void setParticleEmittersBasedOnLegacyVisualOverrides(string legacyId, LegacyMenuVisualsOverride vo)
        {
            Sprite occultGlyphsSS1 = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundFloatingGlyphs1);
            Sprite occultGlyphsSS2 = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundFloatingGlyphs2);
            overrideParticleEmitter(
                "floatingGlyphs",
                occultGlyphsSS1,
                vo.mmBackgroundFloatingGlyphs1Position,
                vo.mmBackgroundFloatingGlyphs1Parent,
                vo.mmBackgroundFloatingGlyphs1RotationMinMax,
                vo.mmBackgroundFloatingGlyphs1Color
            );
            overrideParticleEmitter(
                "floatingGlyphs (1)",
                occultGlyphsSS2,
                vo.mmBackgroundFloatingGlyphs2Position,
                vo.mmBackgroundFloatingGlyphs2Parent,
                vo.mmBackgroundFloatingGlyphs2RotationMinMax,
                vo.mmBackgroundFloatingGlyphs2Color
            );

            Sprite ashFlakesSS1 = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundAshFlakes1);
            Sprite ashFlakesSS2 = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundAshFlakes2);
            Sprite ashFlakesSS3 = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundAshFlakes3);
            overrideParticleEmitter(
                "AshFlakes (7)",
                ashFlakesSS1,
                vo.mmBackgroundAshFlakes1Position,
                vo.mmBackgroundAshFlakes1Parent,
                vo.mmBackgroundAshFlakes1RotationMinMax,
                vo.mmBackgroundAshFlakes1Color
            );
            overrideParticleEmitter(
                "AshFlakes (8)",
                ashFlakesSS2,
                vo.mmBackgroundAshFlakes2Position,
                vo.mmBackgroundAshFlakes2Parent,
                vo.mmBackgroundAshFlakes2RotationMinMax,
                vo.mmBackgroundAshFlakes2Color
            );
            overrideParticleEmitter(
                "AshFlakes (9)",
                ashFlakesSS3,
                vo.mmBackgroundAshFlakes3Position,
                vo.mmBackgroundAshFlakes3Parent,
                vo.mmBackgroundAshFlakes3RotationMinMax,
                vo.mmBackgroundAshFlakes3Color
            );

            Sprite eyeGlowSprite = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundEyeGlow);
            Sprite eyeFlareSprite = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundEyeFlare);
            Sprite eyeEffectSprite = ResourcesManager.GetSpriteForUI(legacyId + "." + vo.mmBackgroundEyeEffect);
            // Glow
            overrideParticleEmitter(
                "Glow",
                eyeGlowSprite,
                vo.mmBackgroundEyeGlowPosition,
                vo.mmBackgroundEyeGlowParent,
                vo.mmBackgroundEyeGlowRotationMinMax,
                vo.mmBackgroundEyeGlowColor
            );
            // Flare
            overrideParticleEmitter(
                "Glow (1)",
                eyeFlareSprite,
                vo.mmBackgroundEyeFlarePosition,
                vo.mmBackgroundEyeFlareParent,
                vo.mmBackgroundEyeFlareRotationMinMax,
                vo.mmBackgroundEyeFlareColor
            );

            // Spikes
            overrideParticleEmitter(
                "GlowSpikes (2)",
                eyeEffectSprite,
                vo.mmBackgroundEyeEffectPosition,
                vo.mmBackgroundEyeEffectParent,
                vo.mmBackgroundEyeEffectRotationMinMax,
                vo.mmBackgroundEyeEffectColor
            );
        }

        public static void initOnMenuLoading()
        {
            try
            {
                Legacy legacy = Watchman.Get<Stable>().Protag().ActiveLegacy;
                if (legacy == null) return;
                Birdsong.Sing("Hello World from Fevered Imagination's Main Menu Manager!: Current legacy: " + legacy.Id);

                LegacyMenuVisualsOverride visualsOverride = legacy.RetrieveProperty<LegacyMenuVisualsOverride>("menuVisualsOverride");
                if (visualsOverride == null) return;

                setSpritesBasedOnLegacyVisualOverrides(legacy.Id, visualsOverride);
                setParticleEmittersBasedOnLegacyVisualOverrides(legacy.Id, visualsOverride);
            }
            catch (Exception err) { Console.WriteLine(err); }
        }

        public static void Enact()
        {
            Machine.ClaimProperty<Legacy, LegacyMenuVisualsOverride>("menuVisualsOverride");
            AtTimeOfPower.MenuSceneInit.Schedule(initOnMenuLoading, PatchType.Postfix);
        }
    }
}
