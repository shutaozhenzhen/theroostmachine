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

        readonly Image burnImageBase;
        readonly AnimationCurve burnAlphaCurve;

        bool slideshowCoroutineRunning = false;
        bool decayCoroutineRunning = false;
        readonly List<Image> imagePool = new List<Image>();
        readonly List<BurnImage> activeImages = new List<BurnImage>();

        static SlideshowBurnImagesMaster _instance;

        public static void Enact()
        {
            Birdsong.Sing("Slideshow Was properly enabled");
            Machine.ClaimProperty<Recipe, List<string>>("burnimages", true);
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(CheckForPresenceOfBurnImages, PatchType.Postfix);
            var o = new GameObject();
            _instance = o.AddComponent<SlideshowBurnImagesMaster>();
        }

        public SlideshowBurnImagesMaster()
        {
            var o = new GameObject();
            
            burnImageBase = o.AddComponent<Image>();
            var shader = Shader.Find("UI-Multiply");
            burnImageBase.material = new Material(shader);
            burnImageBase.maskable = true;

            burnAlphaCurve = AnimationCurve.Linear(0.0f, 1f, 2.0f, 0.0f);
        }
        /*
        public void Awake()
        {
            new Watchman().Register(this);
        }*/

        public static void CheckForPresenceOfBurnImages(Situation situation)
        {
            Birdsong.Sing("Checking for presence of burn images...");
            List<string> burnImages = situation.Recipe.RetrieveProperty<List<string>>("burnimages");
            if (burnImages == null) return;

            // Start a coroutine where we'll regularly display new burn images
            var spawnTransform = situation.Token.Location.Anchored3DPosition;
            Birdsong.Sing("Found burnimages. Starting slideshow coroutine...");
            _instance.StartCoroutine(_instance.ShowSlideshow(spawnTransform, burnImages));
        }
        
        IEnumerator ShowSlideshow(Vector3 location, List<string> burnImages)
        {
            SoundManager.PlaySfx("FXBurnImage");
            slideshowCoroutineRunning = true;
            int currentImageIndex = 0;
            float timeSinceLastChange = 0;
            string currentSpriteName = burnImages[currentImageIndex];
            Birdsong.Sing("Current sprite is", currentSpriteName);
            ShowImageBurn(currentSpriteName, location, 20f, 2f, ImageLayoutConfig.CenterOnToken);
            
            while (slideshowCoroutineRunning)
            {
                timeSinceLastChange += Time.deltaTime;
                if(timeSinceLastChange > 5)
                {
                    Birdsong.Sing("Enough time elapsed, spawning the next burn image...");
                    timeSinceLastChange = 0;
                    currentImageIndex++;
                    currentSpriteName = burnImages[currentImageIndex];
                    ShowImageBurn(currentSpriteName, location, 20f, 2f, ImageLayoutConfig.CenterOnToken);
                    yield return null;
                    slideshowCoroutineRunning = currentImageIndex < burnImages.Count - 1;
                }
                else yield return null;
            }
            Birdsong.Sing("Finished the slideshow.");
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
            var newImg = Instantiate<Image>(burnImageBase, transform) as Image;
            imagePool.Add(newImg);

            return newImg;
        }

        Sprite LoadBurnSprite(string imageName)
        {
            return ResourcesManager.GetSprite("burns", imageName, false);
        }
    }
}
