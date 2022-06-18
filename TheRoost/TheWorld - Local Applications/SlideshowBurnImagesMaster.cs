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

        static GameObject root;
        static GameObject prefab;
        static SlideshowBurnImagesMaster _instance;
        static readonly float SLIDE_DURATION = 2f;

        public static void Enact()
        {
            Birdsong.Sing("Slideshow Was properly enabled");
            Machine.ClaimProperty<Recipe, List<string>>("burnimages", true);
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
            /*
            UnityEditor.AnimationCurveWrapperJSON:{ "curve":{ "serializedVersion":"2","m_Curve":[{ "serializedVersion":"3","time":0.0,"value":0.0,"inSlope":9.524999618530274,"outSlope":9.524999618530274,"tangentMode":0,"weightedMode":0,"inWeight":0.3333333432674408,"outWeight":0.3333333432674408},{ "serializedVersion":"3","time":0.25354909896850588,"value":0.9913880228996277,"inSlope":0.0,"outSlope":0.0,"tangentMode":0,"weightedMode":0,"inWeight":0.3333333432674408,"outWeight":0.3333333432674408},{ "serializedVersion":"3","time":0.9921259880065918,"value":0.012500107288360596,"inSlope":-3.562192678451538,"outSlope":-3.562192678451538,"tangentMode":0,"weightedMode":0,"inWeight":0.3333333432674408,"outWeight":0.3333333432674408}],"m_PreInfinity":2,"m_PostInfinity":2,"m_RotationOrder":0} }
            */
        }

        public static void SetupComponent()
        {
            root = GameObject.Find("BurnImages");
            prefab = new GameObject();
            _instance = root.AddComponent<SlideshowBurnImagesMaster>();
        }

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
            ShowImageBurn(currentSpriteName, location, SLIDE_DURATION+1f, 2f, ImageLayoutConfig.CenterOnToken);
            
            while (slideshowCoroutineRunning)
            {
                timeSinceLastChange += Time.deltaTime;
                if(timeSinceLastChange > SLIDE_DURATION)
                {
                    Birdsong.Sing("Enough time elapsed, spawning the next burn image...");
                    timeSinceLastChange = 0;
                    currentImageIndex++;
                    currentSpriteName = burnImages[currentImageIndex];
                    ShowImageBurn(currentSpriteName, location, SLIDE_DURATION+1f, 2f, ImageLayoutConfig.CenterOnToken);
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
