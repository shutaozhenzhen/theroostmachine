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

        public static ElementStack lastClickedElementStack { get; private set; }
        public static ElementStack lastHoveredElementStack { get; private set; }

        internal static void Enact()
        {
            Machine.Patch(
                original: Machine.GetMethod<RequiresExecutionState>(nameof(SituationState.Enter)),
                prefix: typeof(RavensEye).GetMethodInvariant(nameof(PushCurrentSituation)));

            Machine.Patch(
                 original: Machine.GetMethod<RequiresExecutionState>(nameof(SituationState.Exit)),
                 prefix: typeof(RavensEye).GetMethodInvariant(nameof(PopCurrentSituation)));

            //unused?
            
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
            lastClickedElementStack = __instance;
        }

        public static void SetHoveredToken(Token __instance)
        {
            if (__instance.Payload.GetType() != typeof(ElementStack)) return;
            lastHoveredElementStack = __instance.Payload as ElementStack;

            // Not clean, but we call the Meniscate accessibility window again for reasons.
            // (the reasons: we can't easily patch the right method so SetHoveredToken is called after the event fires up and the magnifying glass appears.
            // Because of that, we force it to appear again and update itself a second time right after the first event fired. In practice, this is unnoticeable.)
            var manifestation = (__instance.GetManifestation() as CardManifestation);
            var meniscate = Watchman.Get<Meniscate>();
            if (meniscate != null) //eg we might have a face down card on the credits page - in the longer term, of course, this should get interfaced
            {
                var _flipHelper = new FlipHelper(manifestation);
                if (_flipHelper.CurrentOrientation != FlipHelper.TargetOrientation.FaceDown)
                    meniscate.SetHighlightedElement(__instance.PayloadEntityId, __instance.Payload.Quantity);
                else
                    meniscate.SetHighlightedElement(null);
            }
        }

        public static void UnsetHoveredToken(Token __instance)
        {
            if (__instance.Payload.GetType() != typeof(ElementStack)) return;
            lastHoveredElementStack = null;
        }

    }
}
