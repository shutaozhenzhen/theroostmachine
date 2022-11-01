using SecretHistories.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Roost.World.Elements
{
    class ElementRandomDecay
    {
        public static void Enact()
        {
            Machine.ClaimProperty<Element, List<String>>("decayTo");

            Machine.Patch(
                original: typeof(Element).GetPropertyInvariant(nameof(Element.DecayTo)).GetGetMethod(),
                prefix: typeof(ElementRandomDecay).GetMethodInvariant(nameof(GetRandomDecayElement))
            );
        }

        public static bool GetRandomDecayElement(ref string __result, Element __instance)
        {
            List<String> ids = Machine.RetrieveProperty<List<String>>(__instance, "decayTo");
            int randomIndex = UnityEngine.Random.Range(0, ids.Count - 1);
            __result = ids[randomIndex];
            return false;
        }
    }
}
