using SecretHistories.Spheres;
using SecretHistories.UI;

using UnityEngine;
using UnityEngine.UI;

namespace Roost.World.Slots
{
    /*
     * Handles the visual behaviour of one minislot GameObject
     */
    public class MiniSlotManager : MonoBehaviour
    {
        [SerializeField] private Image slotImage;
        [SerializeField] private GameObject slotGreedyIcon;
        [SerializeField] private ParticleSystem appearFX;

        public void Initialise()
        {
            slotImage = this.transform.Find("Artwork").GetComponent<Image>();
            slotGreedyIcon = this.transform.Find("GreedySlotIcon").gameObject;
            appearFX = this.gameObject.GetComponentInChildren<ParticleSystem>();
        }

        public void UpdateSlotVisuals(Sphere sphere)
        {
            var token = sphere.GetElementTokens().Find(x => true);
            if (token == null)
            {
                slotImage.sprite = null;
                slotImage.color = Color.black;
            }
            else
            {
                ElementStack elementStackLordForgiveMe = token.Payload as ElementStack;
                slotImage.sprite = ResourcesManager.GetSpriteForElement(elementStackLordForgiveMe.Icon);
                slotImage.color = Color.white;
            }
        }

        public void DisplaySlot(bool greedy)
        {
            slotGreedyIcon.SetActive(greedy);

            if (!this.gameObject.activeInHierarchy)
            {
                this.gameObject.SetActive(true);
                SoundManager.PlaySfx("SituationTokenShowOngoingSlot");
                appearFX.Play();
            }
        }
    }
}
