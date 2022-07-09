using SecretHistories.Abstract;
using SecretHistories.Enums;
using SecretHistories.Manifestations;
using SecretHistories.Services;
using SecretHistories.Spheres;
using SecretHistories.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Roost.World.Recipes
{
    public class MultislotsManager : MonoBehaviour
    {
        // TODO: STORE WAY MORE REFERENCES TO IMPROVE PERF, THIS SHIT IS CALLED CONSTANTLY
        public GameObject firstSlot;
        public GameObject secondSlot;
        public GameObject thirdSlot;
        
        public void DisplayStacksInMiniSlots(List<Sphere> spheres)
        {
            if (spheres.Count == 0)
            {
                EmptySlot(firstSlot);
                EmptySlot(secondSlot);
                EmptySlot(thirdSlot);
                return;
            }

            if (spheres.Count == 1) FillSlotVisuals(firstSlot, spheres[0]);
            if (spheres.Count == 2)
            {
                FillSlotVisuals(secondSlot, spheres[0]);
                FillSlotVisuals(firstSlot, spheres[1]);
            }
            if (spheres.Count >= 3)
            {
                FillSlotVisuals(thirdSlot, spheres[0]);
                FillSlotVisuals(secondSlot, spheres[1]);
                FillSlotVisuals(firstSlot, spheres[2]);
            }
        }

        void FillSlotVisuals(GameObject slot, Sphere sphere)
        {
            var token = sphere.GetElementTokens().SingleOrDefault();
            Birdsong.Sing(new System.Random().Next(), "Fill slot visuals for ", slot.name, "token=", token);
            if (token == null)
            {
                EmptySlot(slot);
            }
            else
            {
                Birdsong.Sing(new System.Random().Next(), slot.name, "=> position=", slot.GetComponent<RectTransform>().anchoredPosition);
                ElementStack elementStackLordForgiveMe = token.Payload as ElementStack;
                FillSlot(slot, elementStackLordForgiveMe);
            }
        }

        void EmptySlot(GameObject slot)
        {
            Image slotImage = slot.transform.Find("Artwork").GetComponent<Image>();
            slotImage.sprite = null;
            slotImage.color = Color.black;
        }

        void FillSlot(GameObject slot, ElementStack stack)
        {
            Image slotImage = slot.transform.Find("Artwork").GetComponent<Image>();
            slotImage.sprite = ResourcesManager.GetSpriteForElement(stack?.Icon);
            slotImage.color = Color.white;
        }

        void DisplaySlotIfCountAtLeastEquals(GameObject slot, List<Sphere> spheres, int equals)
        {
            Image image = slot.transform.Find("Artwork").GetComponent<Image>();
            Transform greedy = slot.transform.Find("GreedySlotIcon");
            Birdsong.Sing(new System.Random().Next(), "Visibility update for slot ", slot.name, "values:", spheres.Count, "<=>", equals);
            if (spheres.Count < equals)
            {
                slot.SetActive(false);
                image.gameObject.SetActive(false);
                greedy.gameObject.SetActive(false);
            }
            else if (!image.isActiveAndEnabled)
            {
                slot.SetActive(true);
                image.gameObject.SetActive(true);
                //ongoingSlotAppearFX.Play();
                if(equals == 1) SoundManager.PlaySfx("SituationTokenShowOngoingSlot");

                bool isGreedy = spheres[equals - 1].GoverningSphereSpec.Greedy;
                greedy.gameObject.SetActive(isGreedy);
            }
        }

        void UpdateSlotsVisibility(List<Sphere> spheres)
        {
            Birdsong.Sing(new System.Random().Next(), "Updating slot visibility, count=", spheres.Count);
            DisplaySlotIfCountAtLeastEquals(firstSlot, spheres, 1);
            DisplaySlotIfCountAtLeastEquals(secondSlot, spheres, 2);
            DisplaySlotIfCountAtLeastEquals(thirdSlot, spheres, 3);
        }

        public void DisplayRecipeThreshold(IManifestable manifestable)
        {
            var recipeThresholdDominion = manifestable.Dominions.SingleOrDefault(d =>
                d.Identifier == SituationDominionEnum.RecipeThresholds.ToString());

            if (recipeThresholdDominion == null)
                return;

            var recipeThresholdSpheres = recipeThresholdDominion.Spheres;
            Birdsong.Sing(new System.Random().Next(), "Got asked to display the slot spheres!", recipeThresholdSpheres.Count, recipeThresholdSpheres.Any());
            UpdateSlotsVisibility(recipeThresholdSpheres);
            DisplayStacksInMiniSlots(recipeThresholdSpheres);
        }

        public static bool _DisplayRecipeThreshold(IManifestable manifestable, VerbManifestation __instance)
        {
            __instance.gameObject.GetComponent<MultislotsManager>().DisplayRecipeThreshold(manifestable);
            return false;
        }
    }

    class RecipeMultipleSlotsMaster : MonoBehaviour
    {
        static Action<object, object> MaxSpheresAllowedSetter = typeof(SituationDominion).GetFieldInvariant("MaxSpheresAllowed").SetValue;
        public static void Enact()
        {
            PatchSituationWindowPrefab();
            PatchThresholdSpherePrefab();
            PatchVerbManifestationPrefab();
        }

        private static void PatchVerbManifestationPrefab()
        {
            VerbManifestation verbManifestationPrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<VerbManifestation>();
            GameObject firstOngoingMiniSlot = verbManifestationPrefab.transform.Find("OngoingSlot").gameObject;
            
            GameObject secondOngoingMiniSlot = Instantiate(firstOngoingMiniSlot, verbManifestationPrefab.transform);
            secondOngoingMiniSlot.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -42.75002f);
            
            GameObject thirdOngoingMiniSlot = Instantiate(firstOngoingMiniSlot, verbManifestationPrefab.transform);
            thirdOngoingMiniSlot.GetComponent<RectTransform>().anchoredPosition = new Vector2(-44.25f, -42.75002f);

            var msm = verbManifestationPrefab.gameObject.AddComponent<MultislotsManager>();

            msm.firstSlot = firstOngoingMiniSlot;
            msm.secondSlot = secondOngoingMiniSlot;
            msm.thirdSlot = thirdOngoingMiniSlot;

            Machine.Patch(
                original: typeof(VerbManifestation).GetMethodInvariant("DisplayRecipeThreshold"),
                prefix: typeof(MultislotsManager).GetMethodInvariant("_DisplayRecipeThreshold")
            );
        }

        private static void PatchCountdownViewInSituationWindowPrefab(SituationWindow situationWindowPrefab)
        {
            GameObject countdown = situationWindowPrefab.transform.Find("RecipeThresholdsDominion/SituationCountdownView").gameObject;
            countdown.GetComponent<RectTransform>().anchoredPosition = new Vector2(-35, 0);
        }

        private static void PatchThresholdSpherePrefab()
        {
            ThresholdSphere thresholdSpherePrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<ThresholdSphere>();
            var le = thresholdSpherePrefab.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 90;

            Machine.Patch(
                original: typeof(RecipeSlotViz).GetMethodInvariant("TriggerHideAnim"),
                prefix: typeof(RecipeMultipleSlotsMaster).GetMethodInvariant(nameof(HideImmediately)));
        }

        private static void PatchThresholdsHolderInSituationWindowPrefab(GameObject holder)
        {
            var hlg = holder.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var holderRect = holder.GetComponent<RectTransform>();
            holderRect.anchoredPosition = new Vector2(-170, 60);
            holderRect.sizeDelta = new Vector2(270, 120);
        }

        static void PatchSituationWindowPrefab()
        {
            SituationWindow situationWindowPrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<SituationWindow>();
            RectTransform rt = situationWindowPrefab.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(750, 420);
            PatchCountdownViewInSituationWindowPrefab(situationWindowPrefab);

            SituationDominion thresholdsDominion = situationWindowPrefab.transform.Find("RecipeThresholdsDominion").GetComponent<SituationDominion>();
            MaxSpheresAllowedSetter(thresholdsDominion, 3);

            GameObject holder = situationWindowPrefab.transform.Find("RecipeThresholdsDominion/ThresholdHolder").gameObject;
            PatchThresholdsHolderInSituationWindowPrefab(holder);
        }

        public static void HideImmediately(Action<SphereRetirementType> onRetirementComplete, ThresholdSphere __instance)
        {
            onRetirementComplete(SphereRetirementType.Graceful);
            Destroy(__instance);
        }
    }
}
