using SecretHistories.Enums;
using SecretHistories.Manifestations;
using SecretHistories.Services;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Roost.World.Recipes.MultiSlots
{
    class RecipeMultipleSlotsMaster : MonoBehaviour
    {
        static readonly Action<object, object> MaxSpheresAllowedSetter = typeof(SituationDominion).GetFieldInvariant("MaxSpheresAllowed").SetValue;

        public static void Enact()
        {
            PatchSituationWindowPrefab();
            PatchThresholdSpherePrefab();
            PatchVerbManifestationPrefab();
        }

        /*
         * We add two copies of the OngoingSlot object and add a custom component to handle them. We also patch VerbManifestation.DisplayRecipeThreshold so it calls to this component instead of running its own logic
         */
        private static void PatchVerbManifestationPrefab()
        {
            VerbManifestation verbManifestationPrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<VerbManifestation>();

            List<MiniSlotManager> managers = new();

            GameObject originalOngoingSlot = verbManifestationPrefab.transform.Find("OngoingSlot").gameObject;
            InstantiateNthMiniSlotManager(verbManifestationPrefab, managers, 0, originalOngoingSlot);

            for(int i=1; i<3; i++)
            {
                GameObject newSlot = Instantiate(originalOngoingSlot);
                InstantiateNthMiniSlotManager(verbManifestationPrefab, managers, i, newSlot);
            }

            var msm = verbManifestationPrefab.gameObject.AddComponent<MultipleSlotsManager>();
            msm.miniSlotManagers = managers;

            Machine.Patch(
                original: typeof(VerbManifestation).GetMethodInvariant("DisplayRecipeThreshold"),
                prefix: typeof(MultipleSlotsManager).GetMethodInvariant("_DisplayRecipeThreshold")
            );
        }

        private static void InstantiateNthMiniSlotManager(VerbManifestation verbManifestationPrefab, List<MiniSlotManager> managers, int i, GameObject newSlot)
        {
            newSlot.transform.SetParent(verbManifestationPrefab.transform);
            newSlot.GetComponent<RectTransform>().anchoredPosition = new Vector2(44.25f - (44.25f * i), -42.75002f);
            var sm = verbManifestationPrefab.gameObject.AddComponent<MiniSlotManager>();
            sm.SetSlot(newSlot);
            managers.Add(sm);
        }

        // We move the countdown slightly to the left to match the wider situation window
        private static void PatchCountdownViewInSituationWindowPrefab(SituationWindow situationWindowPrefab)
        {
            GameObject countdown = situationWindowPrefab.transform.Find("RecipeThresholdsDominion/SituationCountdownView").gameObject;
            countdown.GetComponent<RectTransform>().anchoredPosition = new Vector2(-35, 0);
        }

        /*
         * We patch the ThresholdSphere prefab so it destroys itself immediately when asked to retire
         */
        private static void PatchThresholdSpherePrefab()
        {
            ThresholdSphere thresholdSpherePrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<ThresholdSphere>();
            var le = thresholdSpherePrefab.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 90;

            Machine.Patch(
                original: typeof(RecipeSlotViz).GetMethodInvariant("TriggerHideAnim"),
                prefix: typeof(RecipeMultipleSlotsMaster).GetMethodInvariant(nameof(HideImmediately)));
        }

        /*
         * We patch the Holder game object containing the ThresholdSpheres, adding a HorizontalLayoutGroup to display it properly, and moving it slightly to the lef to match the wider situation window
         */
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

        /*
         * Widen the SituationWindow and set the max spheres allowed to 3. Call the patching of the thresholds holder object.
         */
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
