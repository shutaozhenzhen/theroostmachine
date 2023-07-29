using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace Roost.World.Beauty.BurnImages
{
    class BurnImageData : AbstractEntity<BurnImageData>, IQuickSpecEntity
    {
        [FucineValue(DefaultValue=0.3f)] public float Duration { get; set; }
        [FucineValue(DefaultValue=2f)] public float Overlap { get; set; }

        public void QuickSpec(string value)
        {
            this.SetId(value);
            this.Duration = 0.3f;
            this.Overlap = 2f;
        }

        public BurnImageData() { }
        public BurnImageData(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }
    }

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

        static GameObject root;
        static GameObject prefab;
        static SlideshowBurnImagesMaster _instance;

        internal static void Enact()
        {
            Birdsong.TweetQuiet("Slideshow Was properly enabled");
            Machine.ClaimProperty<Recipe, List<BurnImageData>>("burnimages", true);
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(CheckForPresenceOfBurnImages, PatchType.Postfix);
            AtTimeOfPower.TabletopSceneInit.Schedule(SetupComponent, PatchType.Postfix);
        }

        public SlideshowBurnImagesMaster()
        {
            burnImageBase = prefab.AddComponent<Image>();
            var shader = Shader.Find("Custom/UI-Multiply");
            burnImageBase.material = new Material(shader);
            burnImageBase.maskable = true;

            burnAlphaCurve = new AnimationCurve();
            burnAlphaCurve.AddKey(new Keyframe(0f, 0f, 9.524999618530274f, 9.524999618530274f));
            burnAlphaCurve.AddKey(new Keyframe(0.25f, 1f, 0f, 0f));
            burnAlphaCurve.AddKey(new Keyframe(1f, 0f, -3.562192678451538f, -3.562192678451538f));
        }

        public static void SetupComponent()
        {
            root = GameObject.Find("BurnImages");
            prefab = new GameObject();
            _instance = root.AddComponent<SlideshowBurnImagesMaster>();
        }

        public static void CheckForPresenceOfBurnImages(Situation situation)
        {
            //Birdsong.Sing("Checking for presence of burn images...");
            List<BurnImageData> burnImages = situation.CurrentRecipe.RetrieveProperty<List<BurnImageData>>("burnimages");
            if (burnImages == null) return;

            // Start a coroutine where we'll regularly display new burn images
            var spawnTransform = situation.Token.Location.LocalPosition;
            //Birdsong.Sing("Found burnimages. Starting slideshow coroutine...");
            _instance.StartCoroutine(_instance.ShowSlideshow(spawnTransform, burnImages));
        }
        
        IEnumerator ShowSlideshow(Vector3 location, List<BurnImageData> burnImages)
        {
            SoundManager.PlaySfx("FXBurnImage");
            slideshowCoroutineRunning = true;
            int currentImageIndex = 0;
            float timeSinceLastChange = 0;
            BurnImageData currentImageData = burnImages[currentImageIndex];
            //Birdsong.Sing("Current sprite is", currentImageData.Id, "duration", currentImageData.Duration);
            ShowImageBurn(currentImageData.Id, location, currentImageData.Duration+currentImageData.Overlap, 2f, ImageLayoutConfig.CenterOnToken);
            
            while (slideshowCoroutineRunning)
            {
                timeSinceLastChange += Time.deltaTime;
                if(timeSinceLastChange > currentImageData.Duration)
                {
                    //Birdsong.Sing("Enough time elapsed, spawning the next burn image...");
                    timeSinceLastChange = 0;
                    currentImageIndex++;
                    currentImageData = burnImages[currentImageIndex];
                    //Birdsong.Sing("Current sprite is", currentImageData.Id, "duration", currentImageData.Duration);
                    ShowImageBurn(currentImageData.Id, location, currentImageData.Duration+currentImageData.Overlap, 2f, ImageLayoutConfig.CenterOnToken);
                    yield return null;
                    slideshowCoroutineRunning = currentImageIndex < burnImages.Count - 1;
                }
                else yield return null;
            }
            //Birdsong.Sing("Finished the slideshow.");
        }

        public void ShowImageBurn(string spriteName, Vector3 atPosition, float duration, float scale, ImageLayoutConfig config)
        {
            var sprite = LoadBurnSprite(spriteName);

            if (sprite == null)
            {
                Birdsong.TweetLoud("Can't find a sprite at " + "burns/" + spriteName + "!", 1);
                return;
            }

            var image = GetUnusedImage();
            image.canvasRenderer.SetAlpha(0f);
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
