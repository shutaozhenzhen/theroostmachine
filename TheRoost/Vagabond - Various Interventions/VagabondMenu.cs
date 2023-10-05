using System;
using SecretHistories.UI;
using UnityEngine;
using UnityEngine.UI;
using SecretHistories.Infrastructure;

namespace Roost.Vagabond
{
    internal static class MenuMask
    {
        internal static void Enact()
        {
            AtTimeOfPower.MenuSceneInit.Schedule(TapIntoMainMenu, PatchType.Prefix);

            Machine.Patch(
                original: typeof(NotificationWindow).GetMethodInvariant("SetDetails"),
                prefix: typeof(MenuMask).GetMethodInvariant("ShowNotificationWithIntervention"));
        }

        private static Action hideCurrentOverlay;
        private static Action<CanvasGroupFader> showOverlay;
        private static void TapIntoMainMenu()
        {
            MenuScreenController controller = GameObject.FindObjectOfType<MenuScreenController>();

            hideCurrentOverlay = Delegate.CreateDelegate(typeof(Action), controller, controller.GetType().GetMethodInvariant("HideCurrentOverlay")) as Action;
            showOverlay = Delegate.CreateDelegate(typeof(Action<CanvasGroupFader>), controller, controller.GetType().GetMethodInvariant("ShowOverlay")) as Action<CanvasGroupFader>;
        }

        internal static void ShowOverlay(CanvasGroupFader overlay)
        {
            hideCurrentOverlay();
            showOverlay(overlay);
        }

        internal static void HideCurrentOverlay()
        {
            hideCurrentOverlay();
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

namespace Roost
{
    public static partial class Machine
    {
        public static void ShowOverlay(this MenuScreenController menuScreenController, CanvasGroupFader overlay)
        {
            if (menuScreenController == null)
            {
                Birdsong.TweetLoud("Trying to ShowOverlay, but we're not in the main menu");
                return;
            }

            Vagabond.MenuMask.ShowOverlay(overlay);
        }

        public static void HideCurrentOverlay(this MenuScreenController menuScreenController)
        {
            if (menuScreenController == null)
            {
                Birdsong.TweetLoud("Trying to ShowOverlay, but we're not in the main menu");
                return;
            }

            Vagabond.MenuMask.HideCurrentOverlay();
        }

        public static void ShowNotificationWindow(this Notifier notifier, string title, string description, Sprite image, float duration, bool duplicatesAllowed = true)
        {
            Roost.Vagabond.MenuMask.ShowNotificationWindow(notifier, title, description, image, duration, duplicatesAllowed);
        }
    }
}
