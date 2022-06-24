using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.Ghosts;
using SecretHistories.Manifestations;
using SecretHistories.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Roost.World.Beauty
{
    class CardStyleMaster
    {
        public static void Enact()
        {
            Machine.ClaimProperty<Element, string>("useBigPicture");
            Machine.ClaimProperty<Element, bool>("hideLabel");
            Machine.ClaimProperty<Element, bool>("useAlternativeTextStyle");

            Machine.Patch(
                    original: typeof(CardManifestation).GetMethodInvariant(nameof(CardManifestation.Initialise), typeof(IManifestable)),
                    postfix: typeof(CardStyleMaster).GetMethodInvariant(nameof(PatchTheCardContainingObject)));

            Machine.Patch(
                    original: typeof(CardGhost).GetMethodInvariant(nameof(CardGhost.UpdateVisuals), typeof(IManifestable)),
                    postfix: typeof(CardStyleMaster).GetMethodInvariant(nameof(PatchTheCardContainingObject)));
        }

        public static void PatchTheCardContainingObject(MonoBehaviour __instance, IManifestable manifestable)
        {
            ApplyStyle(manifestable, __instance.gameObject);
        }

        public static void ApplyStyle(IManifestable manifestable, GameObject o)
        {
            ElementStack stack = (ElementStack)manifestable;
            Element element = Watchman.Get<Compendium>().GetEntityById<Element>(stack.EntityId);

            GameObject card = o.FindInChildren("Card", true);
            ApplyBigPicture(element, card);
            ApplyHideLabel(element, card);
            ApplyAlternativeTextStyle(element, card);
        }

        public static void ApplyBigPicture(Element element, GameObject card)
        {
            string tableImageName = element.RetrieveProperty<string>("useBigPicture");
            if (tableImageName == null) return;
            
            Sprite tableImage = ResourcesManager.GetSpriteForElement(tableImageName);
            if (tableImage == null)
            {
                Birdsong.Sing("WARNING, element", element.Id, "defined a custom style and a custom tableImage property but it wasn't found");
                return;
            }

            card.FindInChildren("TextBG").SetActive(false);
            card.FindInChildren("Artwork").GetComponent<Image>().sprite = tableImage;
            var rt = card.FindInChildren("Artwork").GetComponent<RectTransform>();
            var sizeDelta = rt.sizeDelta;
            sizeDelta.y = 113;
            rt.sizeDelta = sizeDelta;
        }

        public static void ApplyHideLabel(Element element, GameObject card)
        {
            bool hideLabel = element.RetrieveProperty<bool>("hideLabel");
            if (!hideLabel) return;

            card.FindInChildren("Text").SetActive(false);
        }

        public static void ApplyAlternativeTextStyle(Element element, GameObject card)
        {
            bool useAlternativeTextStyle = element.RetrieveProperty<bool>("useAlternativeTextStyle");
            if (!useAlternativeTextStyle) return;
            
            TextMeshProUGUI t = card.FindInChildren("Text").GetComponent<TextMeshProUGUI>();
            t.color = Color.white;
            t.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            t.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.2f);
        }
    }
}
