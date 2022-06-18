using SecretHistories.Entities;
using SecretHistories.Services;
using SecretHistories.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Roost.World
{
    class SlideshowBurnImagesMaster : MonoBehaviour
    {
        class BurnImage
        {
            public Image image;
            public float duration;
            public float timeSpent;
            public BurnImage(Image image, float duration)
            {
                this.image = image;
                this.duration = duration;
                this.timeSpent = 0f;
            }
        }

        public enum ImageLayoutConfig { CenterOnToken, LowerLeftCorner }

        Image burnImagePrefab;
        AnimationCurve burnAlphaCurve;

        bool slideshowCoroutineRunning = false;
        bool decayCoroutineRunning = false;
        List<Image> imagePool = new List<Image>();
        List<BurnImage> activeImages = new List<BurnImage>();

        public static void Enact()
        {
            Machine.ClaimProperty<Recipe, List<string>>("burnimages", true);
            SlideshowBurnImagesMaster obj = new SlideshowBurnImagesMaster();
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(obj.checkForPresenceOfBurnImages, PatchType.Postfix);
        }

        public SlideshowBurnImagesMaster()
        {
            burnImagePrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<>("");
        }

        public void Awake()
        {
            new Watchman().Register(this);
        }

        public void checkForPresenceOfBurnImages(Situation situation)
        {
            List<string> burnImages = situation.Recipe.RetrieveProperty<List<string>>("burnimages");
            if (burnImages == null) return;

            // Start a coroutine where you call
            StartCoroutine(ShowSlideshow(burnImages));
        }
        
        IEnumerator ShowSlideshow(List<string> burnImages)
        {
            SoundManager.PlaySfx("FXBurnImage");
            slideshowCoroutineRunning = true;
            int currentImageIndex = 0;
            float timeSinceLastChange = 0;
            string currentSpriteName = burnImages[currentImageIndex];
            ShowImageBurn(currentSpriteName, new Vector3(), 1000, 1, ImageLayoutConfig.CenterOnToken);
            
            while (slideshowCoroutineRunning)
            {
                timeSinceLastChange += Time.deltaTime;
                if(timeSinceLastChange > 1000)
                {
                    timeSinceLastChange = 0;
                    currentImageIndex++;
                    currentSpriteName = burnImages[currentImageIndex];
                    ShowImageBurn(currentSpriteName, new Vector3(), 1000, 1, ImageLayoutConfig.CenterOnToken);
                    yield return null;
                    slideshowCoroutineRunning = currentImageIndex < burnImages.Count - 1;
                }
                else yield return null;
            }
        }

        public void ShowImageBurn(string spriteName, Vector3 atPosition, float duration, float scale, ImageLayoutConfig config)
        {
            var sprite = LoadBurnSprite(spriteName);

            if (sprite == null)
            {
                NoonUtility.Log("Can't find a sprite at " + "burns/" + spriteName + "!", 1);
                return;
            }

            var image = GetUnusedImage();
            image.sprite = sprite;
            image.SetNativeSize();
            image.gameObject.SetActive(true);
            image.rectTransform.pivot = GetPivotFromConfig(config);
            image.transform.position = atPosition;
            image.transform.localRotation = Quaternion.identity;
            image.transform.localScale = Vector3.one * scale;

            activeImages.Add(new BurnImage(image, duration));

            if (!decayCoroutineRunning)
                StartCoroutine(DecayImages());
        }

        IEnumerator DecayImages()
        {
            decayCoroutineRunning = true;

            while (decayCoroutineRunning)
            {
                for (int i = activeImages.Count - 1; i >= 0; i--)
                {
                    activeImages[i].timeSpent += Time.deltaTime;

                    if (activeImages[i].timeSpent > activeImages[i].duration)
                    {
                        activeImages[i].image.gameObject.SetActive(false);
                        activeImages.RemoveAt(i);
                    }
                    else
                    {
                        activeImages[i].image.canvasRenderer.SetAlpha(GetBurnAlpha(activeImages[i]));
                    }
                }

                yield return null;
                decayCoroutineRunning = activeImages.Count > 0;
            }
        }

        float GetBurnAlpha(BurnImage burnImage)
        {
            return burnAlphaCurve.Evaluate(burnImage.timeSpent / burnImage.duration);
        }

        // Utility stuff

        Image GetUnusedImage()
        {
            for (int i = 0; i < imagePool.Count; i++)
                if (imagePool[i].gameObject.activeSelf == false)
                    return imagePool[i];

            return AddImage();
        }

        Vector2 GetPivotFromConfig(ImageLayoutConfig config)
        {
            switch (config)
            {
                case ImageLayoutConfig.CenterOnToken:
                    return new Vector2(0.5f, 0.5f);
                case ImageLayoutConfig.LowerLeftCorner:
                default:
                    return Vector2.zero;
            }
        }

        Image AddImage()
        {
            var newImg = Instantiate<Image>(burnImagePrefab, transform) as Image;
            imagePool.Add(newImg);

            return newImg;
        }

        Sprite LoadBurnSprite(string imageName)
        {
            return ResourcesManager.GetSprite("burns", imageName, false);
        }
    }
}
