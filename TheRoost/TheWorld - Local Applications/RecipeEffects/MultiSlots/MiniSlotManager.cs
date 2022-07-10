using SecretHistories.Spheres;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Roost.World.Recipes.MultiSlots
{
    /*
     * Handles the visual behaviour of one minislot GameObject
     */
    public class MiniSlotManager : MonoBehaviour
    {
        public GameObject slot;
        public Image slotImage;
        public GameObject slotGreedyIcon;

        public void SetSlot(GameObject slotToSet)
        {
            slot = slotToSet;
            slotImage = slotToSet.transform.Find("Artwork").GetComponent<Image>();
            slotGreedyIcon = slotToSet.transform.Find("GreedySlotIcon").gameObject;
        }

        public void EmptySlot()
        {
            slotImage.sprite = null;
            slotImage.color = Color.black;
        }

        public void UpdateSlotVisuals(Sphere sphere)
        {
            var token = sphere.GetElementTokens().SingleOrDefault();
            if (token == null)
            {
                EmptySlot();
            }
            else
            {
                ElementStack elementStackLordForgiveMe = token.Payload as ElementStack;
                slotImage.sprite = ResourcesManager.GetSpriteForElement(elementStackLordForgiveMe?.Icon);
                slotImage.color = Color.white;
            }
        }

        public void DisplaySlotIfCountAtLeastEquals(List<Sphere> spheres, int equals)
        {
            if (spheres.Count < equals)
            {
                slot.SetActive(false);
                slotImage.gameObject.SetActive(false);
                slotGreedyIcon.SetActive(false);
            }
            else if (!slotImage.isActiveAndEnabled)
            {
                slot.SetActive(true);
                slotImage.gameObject.SetActive(true);
                //ongoingSlotAppearFX.Play();
                if (equals == 1) SoundManager.PlaySfx("SituationTokenShowOngoingSlot");

                bool isGreedy = spheres[equals - 1].GoverningSphereSpec.Greedy;
                slotGreedyIcon.SetActive(isGreedy);
            }
        }
    }
}
