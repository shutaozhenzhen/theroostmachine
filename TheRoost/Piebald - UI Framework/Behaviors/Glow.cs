namespace Roost.Piebald
{
    using SecretHistories.Manifestations;
    using SecretHistories.Services;
    using SecretHistories.UI;
    using System;
    using UnityEngine;
    using UnityEngine.UI;

    public class Glow : MonoBehaviour
    {
        private ImageWidget glowImage;
        private GraphicFader glowFader;

        public void Awake()
        {
            var sprite = LoadGlowSprite();
            if (sprite == null)
            {
                return;
            }

            WidgetMountPoint.On(this.gameObject, mountPoint =>
            {
                this.glowImage = mountPoint.AddImage("Glow")
                    .SetLeft(0, -8)
                    .SetRight(1, 8)
                    .SetTop(1, 8)
                    .SetBottom(0, -8)
                    .SetSprite(sprite)
                    .SliceImage()
                    .WithBehavior<GraphicFader>(fader =>
                    {
                        this.glowFader = fader;
                        fader.Hide(true);
                    });
                this.glowImage.GameObject.transform.SetAsFirstSibling();
            });
        }

        public void Show()
        {
            this.glowFader.Show();
        }

        public void Hide()
        {
            this.glowFader.Hide();
        }

        private static Sprite LoadGlowSprite()
        {
            // HACK: Cant get glow from resources, and we cant use our resource hack as it might not have loaded yet.  Get it from a prefab.
            var prefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<VerbManifestation>("manifestations");
            if (prefab == null)
            {
                NoonUtility.LogWarning("Could not find prefab for VerbManifestation");
                return null;
            }

            var tokenOutline = prefab.gameObject.transform.Find("Glow")?.GetComponent<Image>()?.sprite;
            if (tokenOutline == null)
            {
                NoonUtility.LogWarning("Could not find token outline sprite");
                return null;
            }

            return tokenOutline;
        }
    }

    public static class GlowWidgetExtensions
    {
        public static TWidget WithGlow<TWidget>(this TWidget widget, Action<Glow> configure = null)
            where TWidget : UIGameObjectWidget
        {
            widget.WithBehavior<Glow>(glow =>
            {
                configure?.Invoke(glow);
            });

            return widget;
        }
    }
}
