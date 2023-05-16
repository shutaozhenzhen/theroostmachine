using SecretHistories.Enums;
using SecretHistories.Manifestations;
using SecretHistories.Services;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Roost.World.Slots
{
    class RecipeMultipleSlotsMaster : MonoBehaviour
    {
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
            VerbManifestation verbManifestationPrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<VerbManifestation>("manifestations");

            List<MiniSlotManager> managers = new List<MiniSlotManager>();

            // We instantiate a MiniSlotManager to handle the existing minislot GameObject, and two more via a loop to handle the two new minislots.
            GameObject originalOngoingSlot = verbManifestationPrefab.transform.Find("OngoingSlot").gameObject;
            InitialiseNthMiniSlot(verbManifestationPrefab, managers, 0, originalOngoingSlot);

            for(int n=1; n<3; n++)
            {
                GameObject newSlot = Instantiate(originalOngoingSlot);
                InitialiseNthMiniSlot(verbManifestationPrefab, managers, n, newSlot);
            }

            var msm = verbManifestationPrefab.gameObject.AddComponent<MultipleSlotsManager>();
            msm.miniSlotManagers = managers;

            Machine.Patch(
                original: typeof(VerbManifestation).GetMethodInvariant("DisplayRecipeThreshold"),
                prefix: typeof(MultipleSlotsManager).GetMethodInvariant(nameof(MultipleSlotsManager._DisplayRecipeThreshold))
            );
        }

        private static void InitialiseNthMiniSlot(VerbManifestation verbManifestationPrefab, List<MiniSlotManager> managers, int n, GameObject newSlot)
        {
            newSlot.transform.SetParent(verbManifestationPrefab.transform);
            newSlot.GetComponent<RectTransform>().anchoredPosition = new Vector2(44.25f - (44.25f * n), -42.75002f);
            var sm = newSlot.AddComponent<MiniSlotManager>();
            sm.Initialise();
            managers.Add(sm);
        }


        /*
         * We patch the ThresholdSphere prefab so it destroys itself immediately when asked to retire
         */
        private static void PatchThresholdSpherePrefab()
        {
            ThresholdSphere thresholdSpherePrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<ThresholdSphere>("");
            var le = thresholdSpherePrefab.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 90;

            Machine.Patch(
                original: typeof(RecipeSlotViz).GetMethodInvariant("TriggerHideAnim"),
                prefix: typeof(RecipeMultipleSlotsMaster).GetMethodInvariant(nameof(HideImmediately)));
        }

        public static void HideImmediately(Action<SphereRetirementType> onRetirementComplete, ThresholdSphere __instance)
        {
            onRetirementComplete(SphereRetirementType.Graceful);
            Destroy(__instance);
        }

        /*
         * Set the max threshold spheres allowed to 3 and move the . Call the patching of the thresholds holder object.
         */
        static void PatchSituationWindowPrefab()
        {
            SituationWindow situationWindowPrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<SituationWindow>("");

            SituationDominion thresholdsDominion = situationWindowPrefab.transform.Find("RecipeThresholdsDominion").GetComponent<SituationDominion>();
            thresholdsDominion.transform.position = thresholdsDominion.transform.position + new Vector3(27, 0, 0);
            typeof(SituationDominion).GetFieldInvariant("MaxSpheresAllowed").SetValue(thresholdsDominion, 3);

            // We move the countdown slightly to the left to match the new thresholds dominion position
            GameObject countdown = situationWindowPrefab.transform.Find("RecipeThresholdsDominion/SituationCountdownView").gameObject;
            countdown.GetComponent<RectTransform>().anchoredPosition = new Vector2(-35, 0);

            GameObject holder = situationWindowPrefab.transform.Find("RecipeThresholdsDominion/ThresholdHolder").gameObject;
            PatchThresholdsHolderInSituationWindowPrefab(holder);
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
    }
}
