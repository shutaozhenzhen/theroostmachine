using System;
using SecretHistories.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TheRoost.Vagabond
{
    public static class MenuManager
    {
        internal static void Enact()
        {
            AtTimeOfPower.MainMenuLoaded.Schedule(TapIntoMainMenu, PatchType.Prefix);

            TheRoostMachine.Patch(
                original: typeof(OptionsPanel).GetMethodInvariant("PopulateTabs"),
                prefix: typeof(MenuManager).GetMethodInvariant("SetModConfigInterface"));

            TheRoostMachine.Patch(
                original: typeof(OptionsPanelTab).GetMethodInvariant("Initialise"),
                prefix: typeof(MenuManager).GetMethodInvariant("FixIncrediblyAnnoyingNonTransparentTabsBackground"));

            TheRoostMachine.Patch(
                original: typeof(NotificationWindow).GetMethodInvariant("SetDetails"),
                prefix: typeof(MenuManager).GetMethodInvariant("ShowNotificationWithIntervention"));

            CommandLine.Enact();
        }

        private static Action hideCurrentOverlay;
        private static Action<CanvasGroupFader> showOverlay;
        private static void TapIntoMainMenu()
        {
            MenuScreenController controller = GameObject.FindObjectOfType<MenuScreenController>();

            hideCurrentOverlay = Delegate.CreateDelegate(typeof(Action), controller, controller.GetType().GetMethodInvariant("HideCurrentOverlay")) as Action;
            showOverlay = Delegate.CreateDelegate(typeof(Action<CanvasGroupFader>), controller, controller.GetType().GetMethodInvariant("ShowOverlay")) as Action<CanvasGroupFader>;
        }

        public static void ShowOverlay(CanvasGroupFader overlay)
        {
            HideCurrentOverlay();
            showOverlay.Invoke(overlay);
        }

        public static void HideCurrentOverlay()
        {
            hideCurrentOverlay.Invoke();
        }

        private static void SetModConfigInterface(Transform ___TabsHere)
        {
            GameObject.DestroyImmediate(___TabsHere.GetComponent<HorizontalLayoutGroup>());

            GridLayoutGroup grid = ___TabsHere.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;

            grid.constraintCount = 4;
            grid.padding = new RectOffset(0, 0, 40, 15);
            grid.spacing = new Vector2(10, 7);
            grid.cellSize = new Vector2(160, 50);

            ___TabsHere.parent.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            ___TabsHere.parent.GetComponent<VerticalLayoutGroup>().spacing = 3;
            ___TabsHere.parent.GetChild(1).GetComponent<LayoutElement>().minHeight = 100;
            ___TabsHere.parent.GetChild(1).GetComponent<LayoutElement>().flexibleHeight = 0.75f;
            ___TabsHere.parent.GetChild(2).gameObject.AddComponent<LayoutElement>().flexibleHeight = 0.25f;
        }

        private static void FixIncrediblyAnnoyingNonTransparentTabsBackground(OptionsPanelTab __instance)
        {
            Button button = __instance.GetComponentInChildren<Button>();
            ColorBlock colours = button.colors;
            colours.disabledColor = new Color(0, 0, 0, 0);
            colours.normalColor = new Color(0, 0, 0, 0);
            button.colors = colours;
        }

        private static bool intervention = false;
        private static Sprite interventionSprite;
        private static float interventionDuration;
        internal static void ShowNotificationWindow(Notifier notifier, string title, string description, Sprite image, float duration, bool duplicatesAllowed = true)
        {
            intervention = true;
            interventionSprite = image;
            interventionDuration = duration;
            notifier.ShowNotificationWindow(title, description, duplicatesAllowed);
            intervention = false;
            interventionSprite = null;
            interventionDuration = -1;
        }

        private static void ShowNotificationWithIntervention(NotificationWindow __instance, ref Image ___artwork)
        {
            if (intervention)
            {
                if (interventionDuration > 0)
                {
                    __instance.CancelInvoke("Hide");
                    __instance.SetDuration(interventionDuration);
                }
                if (interventionSprite != null)
                    ___artwork.sprite = interventionSprite;
            }
        }
    }
}

namespace TheRoost
{
    public partial class Birdsong
    {
        public static void ShowNotificationWindow(this Notifier notifier, string title, string description, Sprite image, float duration, bool duplicatesAllowed = true)
        {
            TheRoost.Vagabond.MenuManager.ShowNotificationWindow(notifier, title, description, image, duration, duplicatesAllowed);
        }
    }
}
