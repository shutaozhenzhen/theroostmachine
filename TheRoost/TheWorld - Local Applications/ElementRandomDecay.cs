using System;
using System.Collections.Generic;
using SecretHistories.Entities;

using Random = UnityEngine.Random;

namespace Roost.World.Elements
{
    internal static class ElementRandomDecay
    {
        public static void Enact()
        {
            Machine.ClaimProperty<Element, List<string>>(nameof(Element.DecayTo));

            Machine.Patch(
                original: typeof(Element).GetPropertyInvariant(nameof(Element.DecayTo)).GetGetMethod(),
                prefix: typeof(ElementRandomDecay).GetMethodInvariant(nameof(GetRandomDecayElement))
            );
        }

        private static bool GetRandomDecayElement(ref string __result, Element __instance)
        {
            List<string> ids = Machine.RetrieveProperty<List<string>>(__instance, nameof(Element.DecayTo));

            if (ids?.Count > 0)
            {
                int randomIndex = Random.Range(0, ids.Count);
                __result = ids[randomIndex];
            }
            else
                __result = string.Empty;

            return false;
        }
    }
}
