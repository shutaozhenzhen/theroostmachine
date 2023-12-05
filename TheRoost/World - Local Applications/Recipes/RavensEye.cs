using System;
using System.Collections.Generic;
using SecretHistories.UI;

using SecretHistories.Entities;
using SecretHistories.Entities.NullEntities;
using SecretHistories.States;
using SecretHistories.Manifestations;


namespace Roost.World
{
    static class RavensEye
    {
        private static List<Situation> currentSituations = new List<Situation>() { NullSituation.Create() };
        public static Situation currentSituation => currentSituations[currentSituations.Count - 1];

        public static Token lastClickedElementStack { get; private set; }
        public static Token lastHoveredElementStack { get; private set; }

        internal static void Enact()
        {
            Machine.Patch(
                original: Machine.GetMethod<RequiresExecutionState>(nameof(SituationState.Enter)),
                prefix: typeof(RavensEye).GetMethodInvariant(nameof(PushCurrentSituation)));

            Machine.Patch(
                 original: Machine.GetMethod<RequiresExecutionState>(nameof(SituationState.Exit)),
                 prefix: typeof(RavensEye).GetMethodInvariant(nameof(PopCurrentSituation)));

            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.OpenAt)),
                prefix: typeof(RavensEye).GetMethodInvariant(nameof(SetSelectedToken))
            );

            Machine.Patch(
                original: typeof(Token).GetMethodInvariant(nameof(Token.OnPointerEnter)),
                prefix: typeof(RavensEye).GetMethodInvariant(nameof(SetHoveredToken))
            );

            Machine.Patch(
                original: typeof(Token).GetMethodInvariant(nameof(Token.OnPointerExit)),
                prefix: typeof(RavensEye).GetMethodInvariant(nameof(UnsetHoveredToken))
            );

            AtTimeOfPower.NewGameSceneInit.Schedule(ResetTrackingOnNewGame, PatchType.Prefix);
        }


        private static void PushCurrentSituation(Situation situation)
        {
            currentSituations.Add(situation);
        }

        private static void PopCurrentSituation(Situation situation)
        {
            if (currentSituations[currentSituations.Count - 1] != situation)
                Birdsong.TweetLoud("uh-uh something very secretly unexpected happened contact the authorities");

            currentSituations.RemoveAt(currentSituations.Count - 1);
        }

        public static void SetSelectedToken(ElementStack __instance)
        {
            lastClickedElementStack = __instance.GetToken();
        }

        public static void SetHoveredToken(Token __instance)
        {
            lastHoveredElementStack = __instance;

            // Not clean, but we call the Meniscate accessibility window again for reasons.
            // (the reasons: we can't easily patch the right method so SetHoveredToken is called after the event fires up and the magnifying glass appears.
            // Because of that, we force it to appear again and update itself a second time right after the first event fired. In practice, this is unnoticeable.)
            if (__instance.GetManifestation() is CardManifestation manifestation)
                manifestation.OnPointerEnter(null);
        }

        public static void UnsetHoveredToken(Token __instance)
        {
            if (__instance.Payload.GetType() != typeof(ElementStack)) return;
            lastHoveredElementStack = null;
        }

        public static void ResetTrackingOnNewGame()
        {
            lastHoveredElementStack = null;
            lastClickedElementStack = null;
        }

    }
}
