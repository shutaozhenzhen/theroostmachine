using System;
using System.Collections.Generic;

using SecretHistories;
using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.Manifestations;
using SecretHistories.UI;
using SecretHistories.Meta;

using UnityEngine;
using UnityEngine.UI;

using Roost.Beauty;
using Roost.Piebald;
using Roost.World.Recipes;
using Roost.Twins;

namespace Roost.World.Beauty
{
    static class OverlaysMaster
    {
        public const string OVERLAYS_PROPERTY = "overlays";
        public const string OVERLAY_PREFIX = "Overlay_";
        internal static void Enact()
        {
            Machine.ClaimProperty<Element, List<OverlayEntity>>(OVERLAYS_PROPERTY);

            // When a mutation gets applied to a token, add it the scheduled tokens to update visually.
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.SetMutation)),
                prefix: typeof(OverlaysMaster).GetMethodInvariant(nameof(ScheduleOverlayUpdate))
            );

            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.SetQuantity)),
                prefix: typeof(OverlaysMaster).GetMethodInvariant(nameof(ScheduleOverlayUpdate))
            );

            //some out-of-recipe cases where we want to manually apply scheduled overlay updates
            Machine.Patch<ElementStack>(nameof(ElementStack.InteractWithIncoming),
                postfix: typeof(RecipeExecutionBuffer).GetMethodInvariant(nameof(RecipeExecutionBuffer.ApplyOverlayUpdates)));

            Machine.Patch<Token>(nameof(Token.CalveToken),
                postfix: typeof(RecipeExecutionBuffer).GetMethodInvariant(nameof(RecipeExecutionBuffer.ApplyOverlayUpdates)));

            Machine.Patch<ElementsMalleary>(nameof(ElementsMalleary.Mutate),
                postfix: typeof(RecipeExecutionBuffer).GetMethodInvariant(nameof(RecipeExecutionBuffer.ApplyOverlayUpdates)));

            Machine.Patch<ElementsMalleary>(nameof(ElementsMalleary.Unmutate),
                postfix: typeof(RecipeExecutionBuffer).GetMethodInvariant(nameof(RecipeExecutionBuffer.ApplyOverlayUpdates)));

            // When a card is initialised (in the tabletop scene, this always only happen in ~/exterior), apply the overlays
            Machine.Patch(
                original: typeof(CardManifestation).GetMethodInvariant(nameof(CardManifestation.Initialise)),
                postfix: typeof(OverlaysMaster).GetMethodInvariant(nameof(ApplyOverlaysOnCardManifestation))
            );

            // When a stored manifestation is created (inside verbs), apply the overlays
            Machine.Patch(
                original: typeof(StoredManifestation).GetMethodInvariant(nameof(StoredManifestation.Initialise)),
                postfix: typeof(OverlaysMaster).GetMethodInvariant(nameof(ApplyOverlaysOnStoredManifestation))
            );

            // When the details window displays the image of the card, apply the overlays to its image
            Machine.Patch(
                original: typeof(AbstractDetailsWindow).GetMethodInvariant("ShowImage"),
                postfix: typeof(OverlaysMaster).GetMethodInvariant(nameof(UpdateDetailsWindowOverlays))
            );

            // When the magnifying glass accessibility feature is about to display the hovered element, apply the overlays
            Machine.Patch(
                original: typeof(ElementStackSimple).GetMethodInvariant(nameof(ElementStackSimple.Populate)),
                postfix: typeof(OverlaysMaster).GetMethodInvariant(nameof(UpdateMagnifyingGlassOverlays))
            );
        }

        // Clears the overlays from a gameObject containing the original Image component.
        // This works by searching for all the Overlay_X named game objects. All layers follow this naming convention.
        public static void ClearOverlays(GameObject imageGameObject)
        {
            foreach (Transform child in imageGameObject.transform)
                if (child.gameObject.name.StartsWith(OVERLAY_PREFIX))
                    child.gameObject.SetActive(false);
        }

        public static void UpdateMagnifyingGlassOverlays(Image ___artwork)
        {
            if (RavensEye.lastHoveredElementStack == null)
                return;

            // Clear any potential remaining previous overlay
            ClearOverlays(___artwork.gameObject);

            ApplyOverlaysToManifestation(RavensEye.lastHoveredElementStack, ___artwork.gameObject);
        }

        public static void UpdateDetailsWindowOverlays(AbstractDetailsWindow __instance, Sprite image, Image ___artwork)
        {
            if (image == null
                || RavensEye.lastClickedElementStack == null
                // We do this because we had to patch AbstractDetailsWindow, which is also a parent to the aspect detail window. That way, our code only affects
                // the right window.
                || (__instance is TokenDetailsWindow) == false)
                return;

            // Clear any potential remaining previous overlay
            ClearOverlays(___artwork.gameObject);

            ApplyOverlaysToManifestation(RavensEye.lastClickedElementStack, ___artwork.gameObject);
        }

        public static void ScheduleOverlayUpdate(ElementStack __instance)
        {
            Token token = __instance.GetToken();

            // On save load, the payload (ElementStack) isn't already tied to a Token, and we don't want to catch this case anyway.
            if (token != null)
                RecipeExecutionBuffer.ScheduleOverlay(token);
        }

        public static void ApplyOverlaysOnStoredManifestation(IManifestable manifestable, Image ___aspectImage)
        {
            ApplyOverlaysToManifestation(manifestable.GetToken(), ___aspectImage.gameObject);
        }

        public static void ApplyOverlaysOnCardManifestation(IManifestable manifestable, Image ___artwork)
        {
            ApplyOverlaysToManifestation(manifestable.GetToken(), ___artwork.gameObject);
        }

        // Optional second parameter, to work around the fact that when this is called, the token isn't always already holding the right manifestation in its gameObject ref
        public static void ApplyOverlaysToManifestation(Token token, GameObject baseImageGO)
        {
            Element element = Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId);
            List<OverlayEntity> overlays = element.RetrieveProperty<List<OverlayEntity>>(OVERLAYS_PROPERTY);

            if (overlays == null || overlays.Count == 0 || element.Lifetime != 0) return;

            if (baseImageGO == null)
                baseImageGO = GetImageGOBasedOnManifestationType(token.GetManifestation());

            int implicitLayerId = 1;
            HashSet<GameObject> alreadyAssignedLayers = new HashSet<GameObject>();

            foreach (OverlayEntity overlay in overlays)
            {
                string layerName;
                if (string.IsNullOrWhiteSpace(overlay.Layer))
                    layerName = OVERLAY_PREFIX + implicitLayerId;
                else
                    layerName = OVERLAY_PREFIX + overlay.Layer;

                implicitLayerId++;

                GameObject overlayLayer = baseImageGO.FindInChildren(layerName, true);

                Image imageComp = overlayLayer?.GetComponent<Image>();
                if (overlayLayer != null)
                {
                    if (alreadyAssignedLayers.Contains(overlayLayer))
                        continue;

                    if (imageComp != null)
                        overlayLayer.SetActive(false);
                }

                // MatchesExpression also returns true if the expression.isUndefined, no need to check it here
                if (!overlay.Expression.Matches(token))
                    continue;

                // Apply Overlay
                if (overlayLayer == null)
                {
                    // Instantiate overlay GO
                    overlayLayer = InstantiateOverlayLayer(baseImageGO, layerName);
                }
                else
                    overlayLayer.SetActive(true);
                // Assign image
                imageComp = overlayLayer.GetComponent<Image>();
                imageComp.sprite = ResourcesManager.GetSpriteForElement(overlay.Image);

                if (overlay.Grayscale == true)
                {
                    imageComp.material = ResourceHack.FindMaterial("GreyoutUI");
                }
                else imageComp.material = null;

                imageComp.color = overlay.GetColor();
                alreadyAssignedLayers.Add(overlayLayer);
            }

            // Bring back the decay timer to the front. Not important anyway because overlays do not support decaying cards, officially. But in case we want to
            // support them in the future, this won't come back to bite us.
            var decay = baseImageGO.FindInChildren("DecayView", true);
            if (decay != null) decay.transform.SetAsLastSibling();
        }

        private static GameObject GetImageGOBasedOnManifestationType(IManifestation manifestation)
        {
            if (manifestation is CardManifestation card)
                return card.gameObject.FindInChildren("Artwork", true);
            else if (manifestation is StoredManifestation stored)
                return stored.gameObject.FindInChildren("Icon", true);

            throw new NotImplementedException("When overlay layers are applied and a specific baseImageGO isn't provided, the code can only guess which gameObject to apply the layers to if it is a CardManifestation or a StoredManifestation");
        }

        public static GameObject InstantiateOverlayLayer(GameObject baseImageGO, string layerName)
        {
            GameObject layer = new GameObject(layerName);
            layer.transform.SetParent(baseImageGO.transform);
            layer.name = layerName;

            Image image = layer.AddComponent<Image>();
            image.maskable = true;

            RectTransform rt = layer.transform as RectTransform;
            rt.localEulerAngles = Vector3.zero;
            rt.localScale = Vector3.one;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition3D = Vector3.zero;

            return layer;
        }
    }
}
