using SecretHistories.Entities;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Roost.World
{
    class SlideshowBurnImagesMaster : TabletopImageBurner
    {
        public static void Enact()
        {
            Machine.ClaimProperty<Recipe, List<string>>("burnimages", true);
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(checkForPresenceOfBurnImages, PatchType.Postfix);
        }

        public static void checkForPresenceOfBurnImages(Situation situation)
        {
            List<string> burnImages = situation.Recipe.RetrieveProperty<List<string>>("burnimages");
            if (burnImages == null) return;

            // Start a coroutine where you call
        }
        /*
        public void ShowImageBurn(string spriteName, Vector3 atPosition, float duration, float scale, ImageLayoutConfig config)
        {
            var sprite = LoadBurnSprite(spriteName);

            if (sprite == null)
            {
                NoonUtility.Log("Can't find a sprite at " + "burns/" + spriteName + "!", 1);
                return;
            }

            SoundManager.PlaySfx("FXBurnImage");

            var image = GetUnusedImage();
            image.sprite = sprite;
            image.SetNativeSize();
            image.gameObject.SetActive(true);
            image.rectTransform.pivot = GetPivotFromConfig(config);
            image.transform.position = atPosition;
            image.transform.localRotation = Quaternion.identity;
            image.transform.localScale = Vector3.one * scale;

            activeImages.Add(new BurnImage(image, duration));

            if (!coroutineRunning)
                StartCoroutine(DecayImages());
        }*/
    }
}
