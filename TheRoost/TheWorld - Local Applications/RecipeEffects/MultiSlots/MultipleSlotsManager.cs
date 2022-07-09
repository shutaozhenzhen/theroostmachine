using SecretHistories.Abstract;
using SecretHistories.Enums;
using SecretHistories.Manifestations;
using SecretHistories.Spheres;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Roost.World.Recipes.MultiSlots
{
    /*
     * Component added to the VerbManifestation, acting as an alternative entrypoint to handle all minislot management (the actual logic of the minislots is handled by the MiniSlotManager components it knows about
     */
    public class MultipleSlotsManager : MonoBehaviour
    {
        public List<MiniSlotManager> miniSlotManagers;
        
        /*
         * Handles the visibility update of all slots based on the amount of spheres. The entire list is provided because if a slot is visible, we need to provide it the proper sphere so it can check if it is greedy or not
         * */
        public void DisplayStacksInMiniSlots(List<Sphere> spheres)
        {
            if (spheres.Count == 0)
            {
                miniSlotManagers.ForEach(slot => slot.EmptySlot());
                return;
            }
            int sphereToGive = spheres.Count-1;
            for(int i=0; i<spheres.Count; i++, sphereToGive--)
            {
                miniSlotManagers[i].UpdateSlotVisuals(spheres[sphereToGive]);
            }
        }

        void UpdateSlotsVisibility(List<Sphere> spheres)
        {
            for (int i = 0; i < miniSlotManagers.Count; i++)
            {
                miniSlotManagers[i].DisplaySlotIfCountAtLeastEquals(spheres, i+1);
            }
        }

        /*
         * Called each time a Manifestable (recipe) wants to be properly reflected on the verb token's visuals. Handles minislots visibility and artwork+greedy icon
         */
        public void DisplayRecipeThreshold(IManifestable manifestable)
        {
            var recipeThresholdDominion = manifestable.Dominions.SingleOrDefault(d =>
                d.Identifier == SituationDominionEnum.RecipeThresholds.ToString());

            if (recipeThresholdDominion == null)
                return;

            var recipeThresholdSpheres = recipeThresholdDominion.Spheres;
            UpdateSlotsVisibility(recipeThresholdSpheres);
            DisplayStacksInMiniSlots(recipeThresholdSpheres);
        }

        public static bool _DisplayRecipeThreshold(IManifestable manifestable, VerbManifestation __instance)
        {
            __instance.gameObject.GetComponent<MultipleSlotsManager>().DisplayRecipeThreshold(manifestable);
            return false;
        }
    }
}
