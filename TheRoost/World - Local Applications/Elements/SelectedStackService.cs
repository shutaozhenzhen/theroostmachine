using Roost.World.Beauty;
using SecretHistories.Manifestations;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Roost.World.Elements
{

    public class SelectedStackService
    {
        private static ElementStack selectedElementStack = null;
        public static ElementStack SelectedElementStack { get { return selectedElementStack; } }

        private static ElementStack hoveredElementStack = null;
        public static ElementStack HoveredElementStack { get { return hoveredElementStack; } }

        public static void Enact()
        {
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.OpenAt)),
                prefix: typeof(SelectedStackService).GetMethodInvariant(nameof(SetSelectedToken))
            );

            Machine.Patch(
                original: typeof(Token).GetMethodInvariant(nameof(Token.OnPointerEnter)),
                prefix: typeof(SelectedStackService).GetMethodInvariant(nameof(SetHoveredToken))
            );

            Machine.Patch(
                original: typeof(Token).GetMethodInvariant(nameof(Token.OnPointerExit)),
                prefix: typeof(SelectedStackService).GetMethodInvariant(nameof(UnsetHoveredToken))
            );
        }

        public static void SetSelectedToken(ElementStack __instance)
        {
            selectedElementStack = __instance;
        }

        public static void SetHoveredToken(Token __instance)
        {
            if (__instance.Payload.GetType() != typeof(ElementStack)) return;
            hoveredElementStack = __instance.Payload as ElementStack;

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
            hoveredElementStack = null;
        }
    }
}
