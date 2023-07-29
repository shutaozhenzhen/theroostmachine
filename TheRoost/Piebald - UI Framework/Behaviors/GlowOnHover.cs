namespace Roost.Piebald
{
    using UnityEngine;
    using UnityEngine.EventSystems;

    [RequireComponent(typeof(Glow))]
    public class GlowOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Glow glow;

        public void Awake()
        {
            this.glow = this.gameObject.GetOrAddComponent<Glow>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            this.glow.Show();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            this.glow.Hide();
        }
    }

    // Note: This isnt very useful, as a glow wants to be a sibling of the thing that glows, which will not work in most cases.
    // If we wanted to be fancy, we could slice the widget off of its parent, create a wrapper, and insert the widget into its parent.
    // However, to do that, we would need to remove the layout behaviors of the target widget and inherit them into the wrapper widget.
    // This is entirely doable, but complicated and error prone.
    // public static class GlowOnHoverWidgetExtensions
    // {
    //     public static TWidget WithGlowOnHover<TWidget>(this TWidget widget, Action<GlowOnHover> configure = null)
    //         where TWidget : UIGameObjectWidget
    //     {

    //         widget.WithBehavior<GlowOnHover>(glow =>
    //         {
    //             configure?.Invoke(glow);
    //         });

    //         return widget;
    //     }
    // }
}
