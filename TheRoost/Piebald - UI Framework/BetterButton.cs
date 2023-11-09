using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Roost.Piebald
{
    public class BetterButton : Button
    {
        public ButtonClickedEvent onRightClick = new();

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right) // 1 for Left Click, 3 for Middle Click 
            {
                onRightClick.Invoke();
            }
            else
            {
                onClick.Invoke();
            }
        }
    }
}
