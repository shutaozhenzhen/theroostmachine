using System.Collections.Generic;
using System.Linq;

using SecretHistories.Entities;
using SecretHistories.Spheres;
using SecretHistories.Spheres.Angels;
using SecretHistories.UI;

using Roost.Twins;

namespace Roost.World.Slots
{
    static class GreedySlotsMaster
    {
        internal static void Enact()
        {
            //greedy slots grab tokens based on even chance
            Machine.Patch(
                original: typeof(GreedyAngel).GetMethodInvariant("FindStackForSlotSpecificationInSphere"),
                prefix: typeof(GreedySlotsMaster).GetMethodInvariant(nameof(TryGrabStackTrulyRandom)));
        }

        private static bool TryGrabStackTrulyRandom(SphereSpec slotSpec, Sphere sphereToSearch, ref Token __result)
        {
            List<Token> tokens = sphereToSearch.GetElementTokens();
            Crossroads.ResetCache();
            Crossroads.MarkAllLocalTokens(tokens);
            var candidateTokens = tokens.Where(token => token.CanBePulled() && slotSpec.CheckPayloadAllowedHere(token.Payload).MatchType == SlotMatchForAspectsType.Okay);

            __result = candidateTokens.SelectSingleToken();

            return false;
        }
    }
}
