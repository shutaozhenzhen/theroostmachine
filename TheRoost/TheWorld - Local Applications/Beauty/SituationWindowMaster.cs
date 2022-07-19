using System;
using System.Collections.Generic;

using Assets.Scripts.Application.UI.Situation;
using SecretHistories.UI;
using SecretHistories.Manifestations;
using SecretHistories.Entities;
using SecretHistories.Abstract;
using SecretHistories.Constants.Events;
using SecretHistories.Assets.Scripts.Application.Entities;
using SecretHistories.Assets.Scripts.Application.Infrastructure.Events;
using SecretHistories.Spheres;
using SecretHistories.Services;
using SecretHistories.Core;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Roost.World.Recipes
{
    internal static class SituationWindowMaster
    {
        internal static void Enact()
        {
            Machine.Patch(
                original: typeof(CardManifestation).GetMethodInvariant("Initialise"),
                postfix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(CardWithAspectIconOnTheTable)));

            Machine.Patch(
                original: typeof(TextRefiner).GetMethodInvariant(nameof(TextRefiner.RefineString)),
                prefix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(OverrideRecipeRefinement)));
        }

        //allow aspects to appear as a normal cards 
        private static void CardWithAspectIconOnTheTable(IManifestable manifestable, Image ___artwork)
        {
            if (Machine.GetEntity<Element>(manifestable.EntityId).IsAspect)
                ___artwork.sprite = ResourcesManager.GetSpriteForAspect(manifestable.Icon);
        }

        private static bool OverrideRecipeRefinement(string stringToRefine, AspectsDictionary ____aspectsInContext, ref string __result)
        {
            __result = Elegiast.Scribe.RefineString(stringToRefine, ____aspectsInContext);
            return false;
        }

        //called from the VagabondConfig.StorageSphereDisplay
        public static void UpdateDisplaySettingsForSituationWindows(int setting)
        {
            return;
            SituationWindow situationWindowPrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<SituationWindow>();
            GameObject storageSpherePrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<SituationStorageSphere>().gameObject;
            SetDisplaySettingForSituationWindow(situationWindowPrefab.gameObject, storageSpherePrefab, setting);

            SituationWindow[] allWindows = GameObject.FindObjectsOfType<SituationWindow>(); //zhestko
            foreach (SituationWindow window in allWindows)
            {
                SituationStorageSphere storageSphere = window.gameObject.GetComponentInChildren<SituationStorageSphere>(); //zhestko
                SetDisplaySettingForSituationWindow(window.gameObject, storageSphere.gameObject, setting);
                UpdateSituationWindowDisplay(storageSphere);
            }
        }

        private static void SetDisplaySettingForSituationWindow(GameObject situationWindow, GameObject storageSphereObject, int storagePlacementType)
        {

            RectTransform storageDominion = situationWindow.gameObject.transform.Find("StorageDominion").transform as RectTransform;

            switch (storagePlacementType)
            {
                case 0: //bottom-left horizontal; codename "Nostalgic"
                    storageDominion.anchoredPosition = new Vector2(-280, -287);
                    storageDominion.anchorMin = new Vector2(1, 1);
                    storageDominion.anchorMax = new Vector2(1, 1);

                    baseRowsAmount = 1;
                    tokensPerRow = 7;

                    if (storageSphereObject != null)
                    {
                        GridLayoutGroup storageSphere = storageSphereObject.GetComponent<GridLayoutGroup>();
                        storageSphere.startCorner = GridLayoutGroup.Corner.LowerLeft;
                        storageSphere.startAxis = GridLayoutGroup.Axis.Horizontal;
                        storageSphere.childAlignment = TextAnchor.UpperLeft;
                        storageSphere.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                        storageSphere.constraintCount = 7;
                    }

                    return;

                case 1: //top-right vertical placemenent, codename "Slender"
                    storageDominion.anchoredPosition = new Vector2(-5, -80);
                    storageDominion.anchorMin = new Vector2(1, 1);
                    storageDominion.anchorMax = new Vector2(1, 1);

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
                    storageDominion.anchoredPosition = new Vector2(65, -80);
                    storageDominion.anchorMin = new Vector2(1, 1);
                    storageDominion.anchorMax = new Vector2(1, 1);

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
        public static void UpdateSituationWindowDisplay(Sphere situationStorage)
        {
            Situation situation = situationStorage.GetContainer() as Situation;

            if (situation.Recipe?.Warmup == 0 || !situation.State.IsActiveInThisState(situationStorage))
                return;

            int visibleTokens = situationStorage.Tokens.Count;

            Compendium compendium = Watchman.Get<Compendium>();

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
            SituationWindow window = situationStorage.transform.parent.parent.parent.GetComponent<SituationWindow>();
            window.SituationSphereContentsUpdated(situation);
            //todo additional space for several rows in aspect display
            window.ContentsDisplayChanged(contentsDisplayChangedArgs);
        }
    }
}
