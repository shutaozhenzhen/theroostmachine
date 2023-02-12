using SecretHistories.Abstract;
using SecretHistories.Enums;
using SecretHistories.Manifestations;
using SecretHistories.Spheres;
using System.Collections.Generic;
using UnityEngine;

namespace Roost.World.Slots
{
    /*
     * Component added to the VerbManifestation, acting as an alternative entrypoint to handle all minislot management (the actual logic of the minislots is handled by the MiniSlotManager components it knows about
     */
    public class MultipleSlotsManager : MonoBehaviour
    {
        public List<MiniSlotManager> miniSlotManagers;

        /*
         * Handles the visibility update of all slots based on the amount of spheres.          
         */
        void UpdateSlots(List<Sphere> spheres)
        {
            for (int i = 0; i < miniSlotManagers.Count; i++)
                if (i < spheres.Count)
                {
                    miniSlotManagers[i].DisplaySlot(spheres[i].GoverningSphereSpec.Greedy);
                    miniSlotManagers[i].UpdateSlotVisuals(spheres[i]);
                }
                else
                    miniSlotManagers[i].gameObject.SetActive(false);
        }

        /*
         * Called each time a Manifestable (recipe) wants to be properly reflected on the verb token's visuals. Handles minislots visibility and artwork+greedy icon
         */
        public void DisplayRecipeThreshold(IManifestable manifestable)
        {
            var recipeThresholdDominion = manifestable.Dominions.Find(d =>
                d.Identifier == SituationDominionEnum.RecipeThresholds.ToString());

            if (recipeThresholdDominion == null)
                return;

            List<Sphere> spheres = recipeThresholdDominion.Spheres;
            spheres.Reverse();
            UpdateSlots(spheres);
        }

        public static bool _DisplayRecipeThreshold(IManifestable manifestable, VerbManifestation __instance)
        {
            __instance.gameObject.GetComponent<MultipleSlotsManager>().DisplayRecipeThreshold(manifestable);
            return false;
        }
    }
}
