namespace Roost.Piebald
{
    using UnityEngine.EventSystems;
    using UnityEngine;
    using System;
    using UnityEngine.UI;

    public class PointerSounds : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public string LeftClickSound { get; set; } = "UIButtonClick";

        public string RightClickSound { get; set; } = null;

        public string HoverSound { get; set; } = "TokenHover";

        private bool IsInteractable
        {
            get
            {
                var selectable = this.GetComponent<Selectable>();
                if (selectable == null)
                {
                    return true;
                }

                return selectable.interactable;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!this.IsInteractable)
            {
                return;
            }

            if (this.HoverSound != null)
            {
                SoundManager.PlaySfx(this.HoverSound);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!this.IsInteractable)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Left && !string.IsNullOrEmpty(this.LeftClickSound))
            {
                SoundManager.PlaySfx(this.LeftClickSound);
            }
            else if (eventData.button == PointerEventData.InputButton.Right && !string.IsNullOrEmpty(this.RightClickSound))
            {
                SoundManager.PlaySfx(this.RightClickSound);
            }
        }
    }

    public static class PointerSoundsWidgetExtensions
    {
        public static TWidget WithPointerSounds<TWidget>(this TWidget widget, Action<PointerSounds> configure)
            where TWidget : UIGameObjectWidget
        {
            widget.WithBehavior<PointerSounds>(sounds =>
            {
                configure?.Invoke(sounds);
            });

            return widget;
        }

        public static TWidget WithPointerSounds<TWidget>(this TWidget widget, string clickSound = "UIButtonClick", string hoverSound = "TokenHover")
            where TWidget : UIGameObjectWidget
        {
            widget.WithBehavior<PointerSounds>(sounds =>
            {
                sounds.LeftClickSound = clickSound;
                sounds.HoverSound = hoverSound;
            });

            return widget;
        }
    }
}
