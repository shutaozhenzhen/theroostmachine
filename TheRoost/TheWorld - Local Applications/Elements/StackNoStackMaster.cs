using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;

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
        }

        static bool CanMergeWith(ElementStack __instance, bool __result)
        {
            if (__instance.Element.RetrieveProperty<bool>(NO_STACK))
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

}
