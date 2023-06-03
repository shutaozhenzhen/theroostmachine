using System;
using System.Collections.Generic;
using System.IO;

using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.Ghosts;
using SecretHistories.Manifestations;
using SecretHistories.UI;
using SecretHistories.Spheres;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Roost.World.Beauty
{
    static class CardStyleMaster
    {

        const string USE_BIG_PICTURE = "useBigPicture";
        const string HIDE_LABEL = "hideLabel";
        const string ALTERNATIVE_TEXT_STYLE = "useAlternativeTextStyle";
        const string BIG_PICTURE_FOLDER = "elementsbig";

        internal static void Enact()
        {
            Machine.ClaimProperty<Element, bool>(USE_BIG_PICTURE, defaultValue: false);
            Machine.ClaimProperty<Element, bool>(HIDE_LABEL, defaultValue: false);
            Machine.ClaimProperty<Element, bool>(ALTERNATIVE_TEXT_STYLE, defaultValue: false);

            Machine.Patch(
                    original: typeof(CardManifestation).GetMethodInvariant(nameof(CardManifestation.Initialise), typeof(IManifestable)),
                    postfix: typeof(CardStyleMaster).GetMethodInvariant(nameof(PatchTheCardContainingObject)));

            Machine.Patch(
                    original: typeof(CardManifestation).GetMethodInvariant(nameof(CardManifestation.Initialise), typeof(IManifestable)),
                    postfix: typeof(CardStyleMaster).GetMethodInvariant(nameof(UpdateAnimFrames)));

            Machine.Patch(
                    original: Machine.GetMethod<CardGhost>(nameof(CardGhost.UpdateVisuals), typeof(IManifestable), typeof(Sphere)),
                    postfix: typeof(CardStyleMaster).GetMethodInvariant(nameof(PatchTheCardContainingObject)));


        }

        public static void PatchTheCardContainingObject(MonoBehaviour __instance, IManifestable manifestable)
        {
            if (__instance == null || manifestable == null) return;
            ApplyStyle(manifestable, __instance?.gameObject);
        }

        public static void ApplyStyle(IManifestable manifestable, GameObject o)
        {
            if (o == null)
                return;

            ElementStack stack = (ElementStack)manifestable;
            Element element = Watchman.Get<Compendium>().GetEntityById<Element>(stack.EntityId);

            GameObject card = o.FindInChildren("Card", true);
            ApplyBigPicture(element, stack, card);
            ApplyHideLabel(element, card);
            ApplyAlternativeTextStyle(element, card);
        }

        public static void ApplyBigPicture(Element element, ElementStack stack, GameObject card)
        {
            if (!element.RetrieveProperty<bool>(USE_BIG_PICTURE))
                return;

            Sprite tableImage = ResourcesManager.GetSprite(BIG_PICTURE_FOLDER, stack.Icon);

            card.FindInChildren("TextBG").SetActive(false);
            card.FindInChildren("Artwork").GetComponent<Image>().sprite = tableImage;
            var rt = card.FindInChildren("Artwork").GetComponent<RectTransform>();
            var sizeDelta = rt.sizeDelta;
            sizeDelta.y = 113;
            rt.sizeDelta = sizeDelta;
        }

        public static void ApplyHideLabel(Element element, GameObject card)
        {
            if (!element.RetrieveProperty<bool>(HIDE_LABEL))
                return;

            card.FindInChildren("Text").SetActive(false);
        }

        public static void ApplyAlternativeTextStyle(Element element, GameObject card)
        {
            if (!element.RetrieveProperty<bool>(ALTERNATIVE_TEXT_STYLE))
                return;

            TextMeshProUGUI t = card.FindInChildren("Text").GetComponent<TextMeshProUGUI>();
            t.color = Color.white;
            t.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            t.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.2f);
        }

        public static void UpdateAnimFrames(CardManifestation __instance, IManifestable manifestable, List<Sprite> ___frames)
        {
            ElementStack stack = manifestable as ElementStack;

            if (stack == null)
                return;
            if (!Watchman.Get<Compendium>().GetEntityById<Element>(stack.EntityId).RetrieveProperty<bool>(USE_BIG_PICTURE))
                return;

            ___frames.Clear();
            ___frames.AddRange(GetAnimFramesForBigPicture(stack.Icon));
        }

        public static List<Sprite> GetAnimFramesForBigPicture(string iconId)
        {
            string animFolder = Path.Combine(BIG_PICTURE_FOLDER, "anim");
            List<Sprite> frames = new List<Sprite>();

            int i = 0;

            while (true)
            {
                Sprite frame = ResourcesManager.GetSprite(animFolder, iconId + "_" + i, false);

                if (frame == null)
                    break;

                frames.Add(frame);
                i++;
            }

            return frames;
        }
    }
}
