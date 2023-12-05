using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Commands;

using HarmonyLib;

namespace Roost.World.Elements
{
    public static class StackNoStackMaster
    {
        public const string NO_STACK = "noStack";

        internal static void Enact()
        {
            //noStack property prevents elements from being stacked
            Machine.ClaimProperty<Element, bool>(NO_STACK, false, false);

            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.CanMergeWith)),
                prefix: typeof(StackNoStackMaster).GetMethodInvariant(nameof(CanMergeWith)));

            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.SetQuantity)),
                postfix: typeof(StackNoStackMaster).GetMethodInvariant(nameof(TokenNoStackQuantity)));

            Machine.Patch(
                original: typeof(TokenCreationCommand).GetMethodInvariant(nameof(TokenCreationCommand.Execute)),
                postfix: typeof(StackNoStackMaster).GetMethodInvariant(nameof(TokenNoStackCreation)));
        }

        static bool CanMergeWith(ElementStack __instance, bool __result)
        {
            __result = !__instance.Element.RetrieveProperty<bool>(NO_STACK);

            return __result;
        }

        static void TokenNoStackQuantity(ElementStack __instance)
        {
            if (alreadyCalvingNoStack
                || !__instance.IsValid()
                || __instance?.Token.IsValid() != true)
                return;

            if (__instance.Element.RetrieveProperty<bool>(NO_STACK))
                CalveNoStack(__instance.Token);
        }

        static void TokenNoStackCreation(Token __result)
        {
            if (alreadyCalvingNoStack)
                return;

            if (__result.Payload.GetType() == typeof(ElementStack))
            {
                var element = Watchman.Get<Compendium>().GetEntityById<Element>(__result.PayloadEntityId);

                if (element.RetrieveProperty<bool>(NO_STACK))
                    CalveNoStack(__result);
            }
        }

        static bool alreadyCalvingNoStack;
        static void CalveNoStack(Token token)
        {
            alreadyCalvingNoStack = true;

            bool shrouded = token.Sphere is SituationStorageSphere || token.Payload.IsShrouded;
            bool evict = token.Sphere.IsCategory(SecretHistories.Enums.SphereCategory.World);

            while (token.Quantity > 1)
            {
                var newToken = token.CalveToken(1);

                if (shrouded)
                    newToken.Payload.Shroud(true);

                if (evict)
                    newToken.Sphere.ProcessEvictedToken(newToken, Context.Unknown());
            }

            alreadyCalvingNoStack = false;
        }

    }

}
