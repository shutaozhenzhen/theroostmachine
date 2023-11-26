using Roost.Beauty;
using Roost.Piebald;
using Roost.World.Elements;
using Roost.World.Recipes;
using SecretHistories;
using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.Manifestations;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Roost.World.Beauty
{
    static class OverlaysMaster
    {
        public static string OVERLAYS_PROPERTY = "overlays";

        internal static void Enact()
        {
            Birdsong.Sing(Birdsong.Incr(), "Hello world from OverlaysMaster!");
            Machine.ClaimProperty<Element, List<OverlayEntity>>(OVERLAYS_PROPERTY);

            // When a mutation gets applied to a token, add it the scheduled tokens to update visually.
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.SetMutation)),
                prefix: typeof(OverlaysMaster).GetMethodInvariant(nameof(ScheduleOverlayUpdate))
            );

            // When a card is initialised (in the tabletop scene, this always only happen in ~/exterior), apply the overlays
            Machine.Patch(
                original: typeof(CardManifestation).GetMethodInvariant(nameof(CardManifestation.Initialise)),
                postfix: typeof(OverlaysMaster).GetMethodInvariant(nameof(ApplyOverlaysOnInitialiseManifestation))
            );

            // When a stored manifestation is created (inside verbs), apply the overlays
            Machine.Patch(
                original: typeof(StoredManifestation).GetMethodInvariant(nameof(StoredManifestation.Initialise)),
                postfix: typeof(OverlaysMaster).GetMethodInvariant(nameof(ApplyOverlaysOnInitialiseManifestation))
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
            // You have to do it in two loops, otherwise calling DestroyImmediate on the content of the very list you're looping over
            // leads to missed children
            List<GameObject> toDestroy = new();
            foreach (Transform child in imageGameObject.transform)
            {
                if (child.gameObject.name.StartsWith("Overlay_")) toDestroy.Add(child.gameObject);
            }
            foreach (GameObject child in toDestroy) GameObject.DestroyImmediate(child);
        }


        public static void UpdateMagnifyingGlassOverlays(Image ___artwork)
        {
            
            //Birdsong.Sing(Birdsong.Incr(), $"Updating magnifying glass for artwork GO {___artwork.gameObject.name}");
            GameObject baseImageGO = ___artwork.gameObject;

            // Clear any potential remaining previous overlay
            ClearOverlays(baseImageGO);

            if (SelectedStackService.HoveredElementStack == null)
            {
                //Birdsong.Sing(Birdsong.Incr(), $"No hovered element. Aborting.");
                return;
            }
            ApplyOverlaysToManifestation(SelectedStackService.HoveredElementStack.GetToken(), baseImageGO: baseImageGO);
        }

        public static void UpdateDetailsWindowOverlays(AbstractDetailsWindow __instance, Sprite image)
        {
            // We do this because we had to patch AbstractDetailsWindow, which is also a parent to the aspect detail window. That way, our code only affects
            // the right window.
            if (__instance.gameObject.name != "TokenDetailsWindow") return;
            //Birdsong.Sing(Birdsong.Incr(), $"Displaying details of an element!");

            Token token = SelectedStackService.SelectedElementStack.GetToken();

            GameObject artworkGO = __instance.gameObject.FindInChildren("Artwork", true);

            // Clear any potential remaining previous overlay
            ClearOverlays(artworkGO);

            Image artwork = artworkGO.GetComponent<Image>();
            GameObject artworkPin = __instance.gameObject.FindInChildren("ArtworkPin", true);
            artwork.gameObject.SetActive(image != null);
            artwork.sprite = image;
            if (artworkPin != null)
            {
                artworkPin.gameObject.SetActive(image != null);
                artwork.transform.localEulerAngles = new Vector3(0f, 0f, -5f + Random.value * 10f);
            }

            ApplyOverlaysToManifestation(token, baseImageGO: artwork.gameObject);
        }

        public static void ScheduleOverlayUpdate(ElementStack __instance)
        {
            Token token = __instance.GetToken();
            // On save load, the payload (ElementStack) isn't already tied to a Token, and we don't want to catch this case anyway.
            if(token != null)
            {
                //Birdsong.Sing(Birdsong.Incr(), "SetMutation! Add this token to the buffered overlay updates...");
                RecipeExecutionBuffer.ScheduleOverlay(token);
            }
        }

        public static void ApplyOverlaysOnInitialiseManifestation(CardManifestation __instance, IManifestable manifestable)
        {
            //Birdsong.Sing(Birdsong.Incr(), $"CardManifestation initialisation of {manifestable.Id}, checking overlays...");
            ApplyOverlaysToManifestation(manifestable.GetToken(), __instance.gameObject);
        }

        // Optional second parameter, to work around the fact that when this is called, the token isn't always already holding the right manifestation in its gameObject ref
        public static void ApplyOverlaysToManifestation(Token token, GameObject manifestationGO = null, GameObject baseImageGO = null)
        {
            Element element = Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId);
            List<OverlayEntity> overlays = element.RetrieveProperty<List<OverlayEntity>>(OVERLAYS_PROPERTY);

            if (overlays == null || overlays.Count == 0 || element.Lifetime != 0) return;

            manifestationGO ??= token.gameObject;
            baseImageGO ??= GetImageGOBasedOnManifestationType(token, manifestationGO);

            ApplyOverlaysToGO(token, baseImageGO, overlays);
        }

        public static void ApplyOverlaysToGO(Token token, GameObject baseImageGO, List<OverlayEntity> overlays)
        { 
            int implicitLayerId = 1;
            HashSet<GameObject> alreadyAssignedLayers = new();

            foreach (OverlayEntity overlay in overlays)
            {
                string layerName = overlay.Layer.Equals("") ? $"Overlay_{implicitLayerId}" : $"Overlay_{overlay.Layer}";
                implicitLayerId++;

                GameObject overlayLayer = baseImageGO.FindInChildren(layerName, true);
                Image imageComp;
                if (overlayLayer != null)
                {
                    if (alreadyAssignedLayers.Contains(overlayLayer)) continue;
                    imageComp = overlayLayer.GetComponent<Image>();
                    if(imageComp != null) imageComp.sprite = null;
                }

                // MatchesExpression also returns true if the expression.isUndefined, no need to check it here
                //Birdsong.Sing(Birdsong.Incr(), $"Checking the expression...");
                if (!token.MatchesExpression(overlay.Expression)) continue;

                //Birdsong.Sing(Birdsong.Incr(), $"Valid expression. This overlay must be displayed.");
                // Apply Overlay
                if (overlayLayer == null)
                {
                    // Instantiate overlay GO
                    //Birdsong.Sing(Birdsong.Incr(), $"This token doesn't have a '{layerName}' layer yet. Instantiating one...");
                    overlayLayer = InstantiateOverlayLayer(baseImageGO, layerName);
                }
                //else Birdsong.Sing(Birdsong.Incr(), $"This token already has a '{layerName}' layer.");

                // Assign image
                imageComp = overlayLayer.GetComponent<Image>();
                imageComp.sprite = ResourcesManager.GetSpriteForElement(overlay.Image);

                if (overlay.Grayscale == true)
                {
                    imageComp.material = ResourceHack.FindMaterial("GreyoutUI");
                }
                else imageComp.material = null;

                imageComp.color = overlay._Color ?? Color.white;
                alreadyAssignedLayers.Add(overlayLayer);
                //Birdsong.Sing(Birdsong.Incr(), $"Applied sprite '{overlay.Image}' to the overlay layer '{layerName}'.");
            }

            // Bring back the decay timer to the front. Not important anyway because overlays do not support decaying cards, officially. But in case we want to
            // support them in the future, this won't come back to bite us.
            var decay = baseImageGO.FindInChildren("DecayView", true);
            if (decay != null) decay.transform.SetAsLastSibling();
        }

        public static GameObject GetImageGOBasedOnManifestationType(Token token, GameObject manifestationGO)
        {
            IManifestation manifestation = token.GetManifestation();
            Type manifestationType = manifestation.GetType();

            if (manifestationType == typeof(CardManifestation))
            {
                //Birdsong.Sing(Birdsong.Incr(), $"This token is a card manifestation!");
                return manifestationGO.FindInChildren("Artwork", true);
            }
            else if (manifestationType == typeof(StoredManifestation))
            {
                //Birdsong.Sing(Birdsong.Incr(), $"This token is a stored manifestation!");
                return manifestationGO.FindInChildren("Icon", true);
            }
            throw new NotImplementedException("When overlay layers are applied and a specific baseImageGO isn't provided, the code can only guess which gameObject to apply the layers to if it is a CardManifestation or a StoredManifestation");
        }

        public static GameObject InstantiateOverlayLayer(GameObject baseImageGO, string layerName)
        {
            // Duplicate the original image GO, then delete all the children and reset the sprite.
            // I have no idea why this works so well, because nothing says it should, in theory. But it works perfectly.
            GameObject layer = GameObject.Instantiate(baseImageGO, baseImageGO.transform, true);
            layer.name = layerName;
            layer.GetComponent<Image>().sprite = null;

            List<GameObject> toDestroy = new();
            foreach (Transform child in layer.transform)
            {
                toDestroy.Add(child.gameObject);
            }
            foreach (GameObject child in toDestroy) GameObject.DestroyImmediate(child);

            // This clean method never worked properly for all cases. Somehow, the "dirty" method above works.
            /*
            GameObject layer = new GameObject(layerName);
            layer.transform.SetParent(baseImageGO.transform);

            Image baseImage = baseImageGO.GetComponent<Image>();
            Image image = layer.AddComponent<Image>();
            image.material = baseImage.material;
            image.maskable = baseImage.maskable;
            
            RectTransform rt = layer.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            //rt.pivot = new Vector2(0.5f, 0.5f);
            //rt.transform.localPosition = Vector2.zero;
            RectTransform baseRt = baseImageGO.GetComponent<RectTransform>();*/

            return layer;
        }
    }
}
