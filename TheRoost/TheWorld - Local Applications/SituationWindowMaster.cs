using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Manifestations;
using SecretHistories.Entities;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

using SecretHistories.Abstract;
using SecretHistories.Constants.Events;
using Assets.Scripts.Application.UI.Situation;
using SecretHistories.Assets.Scripts.Application.Entities;
using SecretHistories.Assets.Scripts.Application.Infrastructure.Events;
using SecretHistories.Spheres;

namespace Roost.World.Recipes
{
    //here we fix (aka steal-pick-peck - geddit? geddit? it was previously a beachcomber's class) the bugs
    internal static class SituationWindowMaster
    {
        internal static void Enact()
        {
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.SetMutation)),
                postfix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(FixMutationsDisplay)));
            sphereContentsChangedEventArgs = new SphereContentsChangedEventArgs(null, RecipeExecutionBuffer.situationEffectContext);

            PutSparklesOnSituationWindow();

            Machine.Patch(
                original: typeof(CardManifestation).GetMethodInvariant("Initialise"),
                postfix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(CardWithAspectIconOnTheTable)));
        }

        private static SphereContentsChangedEventArgs sphereContentsChangedEventArgs;
        private static void FixMutationsDisplay(ElementStack __instance)
        {
            sphereContentsChangedEventArgs.Sphere = __instance.Token.Sphere;
            sphereContentsChangedEventArgs.TokenChanged = __instance.Token;
            __instance.Token.Sphere.NotifyTokensChangedForSphere(sphereContentsChangedEventArgs);
        }

        //allow aspects to appear as a normal cards 
        private static void CardWithAspectIconOnTheTable(IManifestable manifestable, Image ___artwork)
        {
            if (Machine.GetEntity<Element>(manifestable.EntityId).IsAspect)
                ___artwork.sprite = ResourcesManager.GetSpriteForAspect(manifestable.Icon);
        }

        private static void PutSparklesOnSituationWindow()
        {
            StoredManifestationDisplayAspectIcons();
            StoredManifestationDisplayQuantities();
            DeckEffectsPreviewInStorageSphere();
            AtTimeOfPower.TabletopLoaded.Schedule(ResizeWindowsOnTabletopEnter, PatchType.Postfix);
            //additionally, SituationStorage token placement is set from the VagabondConfig.StorageSphereDisplay
        }

        private static void StoredManifestationDisplayAspectIcons()
        {
            Machine.Patch(
                original: typeof(StoredManifestation).GetMethodInvariant("DisplayImage"),
                prefix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(StoredManifestationIconFix)));
        }

        private static bool StoredManifestationIconFix(Element element, Image ___aspectImage)
        {
            Sprite sprite;
            if (element.IsAspect)
                sprite = ResourcesManager.GetSpriteForAspect(element.Icon);
            else
                sprite = ResourcesManager.GetSpriteForElement(element.Icon);

            ___aspectImage.sprite = sprite;

            return false;
        }

        private static void StoredManifestationDisplayQuantities()
        {
            GameObject cardManifestation = Watchman.Get<SecretHistories.Services.PrefabFactory>().GetPrefabObjectFromResources<CardManifestation>().gameObject;
            GameObject quantityBadgePrefab = cardManifestation.FindInChildren("StackBadge");
            GameObject storedManifestation = Watchman.Get<SecretHistories.Services.PrefabFactory>().GetPrefabObjectFromResources<StoredManifestation>().gameObject;

            GameObject quantityBadgeForStoredManifestation = GameObject.Instantiate(quantityBadgePrefab);
            GameObject.DestroyImmediate(quantityBadgeForStoredManifestation.GetComponent<ElementStackBadge>());

            //somehow just disabling Canvas.overrideSorting doesn't work - it just ignores me setting it to false
            //well then, guess you'll die
            GameObject.DestroyImmediate(quantityBadgeForStoredManifestation.GetComponent<GraphicRaycaster>());
            GameObject.DestroyImmediate(quantityBadgeForStoredManifestation.GetComponent<Canvas>());

            quantityBadgeForStoredManifestation.transform.SetParent(storedManifestation.transform.GetChild(0));
            quantityBadgeForStoredManifestation.transform.SetAsLastSibling();
            quantityBadgeForStoredManifestation.SetActive(false);
            quantityBadgeForStoredManifestation.GetComponentInChildren<TextMeshProUGUI>().fontSizeMin = 4;
            RectTransform badgeTransform = quantityBadgeForStoredManifestation.GetComponent<RectTransform>();
            badgeTransform.sizeDelta *= 0.85f;
            badgeTransform.anchoredPosition = new Vector2(-6, 6);
            badgeTransform.localScale = Vector3.one;
            badgeTransform.anchorMin = new Vector2(1, 0);
            badgeTransform.anchorMax = new Vector2(1, 0);

            Machine.Patch(
                original: typeof(StoredManifestation).GetMethodInvariant(nameof(StoredManifestation.UpdateVisuals)),
                postfix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(DisplayQuantity)));
            Machine.Patch(
                original: typeof(StoredManifestation).GetMethodInvariant(nameof(StoredManifestation.Highlight)),
                prefix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(StoredManifestationHighlightQuantity)));
            Machine.Patch(
                original: typeof(StoredManifestation).GetMethodInvariant(nameof(StoredManifestation.Unhighlight)),
                prefix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(StoredManifestationUnhighlightQuantity)));
        }

        private static void DisplayQuantity(StoredManifestation __instance, IManifestable manifestable)
        {
            //somehow the badge ends up below the situation window in the current version. this doesn't makes any sense, but it's true.
            GameObject quantityBadge = __instance.transform.GetQuantityBadge();
            if (manifestable.Quantity == 1)
                quantityBadge.SetActive(false);
            else
            {
                quantityBadge.SetActive(true);
                quantityBadge.GetComponentInChildren<TextMeshProUGUI>().text = manifestable.Quantity.ToString();
            }
        }
        private static void StoredManifestationHighlightQuantity(StoredManifestation __instance)
        {
            __instance.transform.GetQuantityBadge().GetComponent<CanvasRenderer>().SetColor(UIStyle.aspectHover);
        }
        private static void StoredManifestationUnhighlightQuantity(StoredManifestation __instance)
        {
            __instance.transform.GetQuantityBadge().GetComponent<CanvasRenderer>().SetColor(Color.white);
        }
        private static GameObject GetQuantityBadge(this Transform storedManifestation)
        {
            return storedManifestation.GetChild(0).GetChild(1).gameObject;
        }

        //called from the VagabondConfig.StorageSphereDisplay
        public static void SetSituationWindowSettings(GameObject situationWindow, GameObject storageSphereObject, int storagePlacementType)
        {
            GameObject storageDominion = situationWindow.gameObject.FindInChildren("StorageDominion", true);
            RectTransform storageDominionTransform = storageDominion.transform.GetComponent<RectTransform>();

            switch (storagePlacementType)
            {
                case 0: //bottom-left horizontal; codename "Nostalgic"
                    storageDominionTransform.anchoredPosition = new Vector2(-280, -287);
                    storageDominionTransform.anchorMin = new Vector2(1, 1);
                    storageDominionTransform.anchorMax = new Vector2(1, 1);

                    baseRowsAmount = 1;
                    tokensPerRow = 7;

                    if (storageSphereObject != null)
                    {
                        GridLayoutGroup storageSphere = storageSphereObject.GetComponent<GridLayoutGroup>();
                        storageSphere.startCorner = GridLayoutGroup.Corner.UpperLeft;
                        storageSphere.startAxis = GridLayoutGroup.Axis.Horizontal;
                        storageSphere.childAlignment = TextAnchor.UpperLeft;
                        storageSphere.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                        storageSphere.constraintCount = 7;

                    }

                    return;

                case 1: //top-right vertical placemenent, codename "Slender"
                    storageDominionTransform.anchoredPosition = new Vector2(-5, -80);
                    storageDominionTransform.anchorMin = new Vector2(1, 1);
                    storageDominionTransform.anchorMax = new Vector2(1, 1);

                    baseRowsAmount = 5;
                    tokensPerRow = 2;

                    if (storageSphereObject != null)
                    {
                        GridLayoutGroup storageSphere = storageSphereObject.GetComponent<GridLayoutGroup>();
                        storageSphere.startCorner = GridLayoutGroup.Corner.UpperRight;
                        storageSphere.startAxis = GridLayoutGroup.Axis.Vertical;
                        storageSphere.childAlignment = TextAnchor.UpperRight;
                        storageSphere.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                        storageSphere.constraintCount = 2;
                    }

                    return;
                case 2://"out of bounds" horizontal; codename "Stranger"
                    storageDominionTransform.anchoredPosition = new Vector2(65, -80);
                    storageDominionTransform.anchorMin = new Vector2(1, 1);
                    storageDominionTransform.anchorMax = new Vector2(1, 1);

                    //no need to resize anything (but will resize at 10000 tokens)
                    baseRowsAmount = 100;
                    tokensPerRow = 100;

                    if (storageSphereObject != null)
                    {
                        GridLayoutGroup storageSphere = storageSphereObject.GetComponent<GridLayoutGroup>();
                        storageSphere.startCorner = GridLayoutGroup.Corner.UpperLeft;
                        storageSphere.startAxis = GridLayoutGroup.Axis.Vertical;
                        storageSphere.childAlignment = TextAnchor.UpperLeft;
                        storageSphere.constraint = GridLayoutGroup.Constraint.FixedRowCount;
                        storageSphere.constraintCount = 5;
                    }
                    return;
            }
        }

        static float rowHeight = 51;
        static float baseRowsAmount = 1;
        static float tokensPerRow = 7;
        public static void ResizeSituationWindowForStorageTokens(Sphere situationStorage)
        {
            int visibleTokens = situationStorage.Tokens.Count;

            Compendium compendium = Watchman.Get<Compendium>();
            Situation situation = situationStorage.GetContainer() as Situation;
            foreach (string deckId in situation.Recipe.DeckEffects.Keys)
            {
                DeckSpec deck = compendium.GetEntityById<DeckSpec>(deckId);
                if (deck.RetrieveProperty<bool>(Legerdemain.DECK_IS_HIDDEN) == false)
                    visibleTokens++;
            }

            int rows = (int)Mathf.Ceil(visibleTokens / tokensPerRow);
            float baseHeight = rowHeight * baseRowsAmount;
            float requiredHeight = rowHeight * rows;

            ContentsDisplayChangedArgs contentsDisplayChangedArgs = new ContentsDisplayChangedArgs();
            contentsDisplayChangedArgs.ExtraHeightRequested = Mathf.Max(requiredHeight - baseHeight, 0);
            situationStorage.transform.parent.parent.parent.GetComponent<SituationWindow>().ContentsDisplayChanged(contentsDisplayChangedArgs);
            situationStorage.transform.parent.parent.parent.GetComponent<SituationWindow>().SituationSphereContentsUpdated(situation);
        }

        private static void ResizeWindowsOnTabletopEnter()
        {
            foreach (SituationStorageSphere situationStorage in GameObject.FindObjectsOfType<SituationStorageSphere>()) //zhestko
                ResizeSituationWindowForStorageTokens(situationStorage);
        }

        static void DeckEffectsPreviewInStorageSphere()
        {
            GameObject situationWindowPrefab = Watchman.Get<SecretHistories.Services.PrefabFactory>().GetPrefabObjectFromResources<SituationWindow>().gameObject;
            GridLayoutGroup storageSphere = Watchman.Get<SecretHistories.Services.PrefabFactory>().GetPrefabObjectFromResources<SituationStorageSphere>().GetComponent<GridLayoutGroup>();
            //deck effects together with the situation storage
            storageSphere.cellSize = new Vector2(35, rowHeight - 3);
            storageSphere.spacing = new Vector2(10, 3);

            GameObject deckPreviewManager = situationWindowPrefab.FindInChildren("SituationDeckEffectsView", true);
            deckPreviewManager.AddComponent<SituationDeckEffectsViewSetter>();
            for (int n = 0; n < 3; n++)
                deckPreviewManager.transform.GetChild(n).gameObject.SetActive(false);
            deckEffectPrefab = deckPreviewManager.transform.GetChild(0).gameObject;

            Machine.Patch(
                original: typeof(SituationDeckEffectsView).GetMethodInvariant("ShowDeckEffects"),
                prefix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(ShowDeckEffects)));
        }

        static GameObject deckEffectPrefab;
        private static bool ShowDeckEffects(DeckEffectView[] ___deckEffectViews, SituationDeckEffectsView __instance, Dictionary<string, int> deckEffects)
        {
            if (deckEffects == null || deckEffects.Count == 0)
                return false;

            Transform situationStorageSphere;
            if (___deckEffectViews[0] == null)
                situationStorageSphere = __instance.transform.parent.parent.gameObject.FindInChildren("StorageSphereHolder", true).transform.GetChild(0);
            else
                situationStorageSphere = ___deckEffectViews[0].transform.parent;

            Compendium compendium = Watchman.Get<Compendium>();
            int i = 0;
            foreach (KeyValuePair<string, int> keyValuePair in deckEffects)
            {
                DeckSpec deck = compendium.GetEntityById<DeckSpec>(keyValuePair.Key);
                if (deck.RetrieveProperty<bool>(Legerdemain.DECK_IS_HIDDEN))
                    continue;

                DeckEffect deckEffect = new DeckEffect(deck, keyValuePair.Value);

                if (___deckEffectViews[i] == null)
                    ___deckEffectViews[i] = GameObject.Instantiate(deckEffectPrefab, situationStorageSphere).GetComponent<DeckEffectView>();
                ___deckEffectViews[i].gameObject.SetActive(true);
                ___deckEffectViews[i].PopulateDisplay(deckEffect);
                ___deckEffectViews[i].transform.SetAsLastSibling();

                i++;
            }

            while (___deckEffectViews[i] != null)
                ___deckEffectViews[i]?.gameObject.SetActive(false);

            return false;
        }
    }

    public class SituationDeckEffectsViewSetter : MonoBehaviour
    {
        void Awake()
        {
            typeof(SituationDeckEffectsView).GetFieldInvariant("deckEffectViews").SetValue(GetComponent<SituationDeckEffectsView>(), new DeckEffectView[99]);
        }
    }
}
