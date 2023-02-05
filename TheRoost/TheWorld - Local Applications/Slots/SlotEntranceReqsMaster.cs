using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Abstract;

using Roost.Twins;
using Roost.Twins.Entities;

namespace Roost.World.Slots
{
    static class SlotEntranceReqsMaster
    {
        const string SLOT_ENTRANCE_REQS = "filter";

        internal static void Enact()
        {
            //tokens are checked against an additional expression filter before going in the slot
            Machine.ClaimProperty<SphereSpec, FucineExp<bool>>(SLOT_ENTRANCE_REQS, false, FucineExp<bool>.UNDEFINED);

            Machine.Patch(
                original: typeof(SphereSpec).GetMethodInvariant(nameof(SphereSpec.CheckPayloadAllowedHere)),
                prefix: typeof(SlotEntranceReqsMaster).GetMethodInvariant(nameof(SlotFilterSatisfied)));
        }

        private static bool SlotFilterSatisfied(SphereSpec __instance, ITokenPayload payload, ref ContainerMatchForStack __result)
        {
            Token token = payload.GetToken();
            Crossroads.MarkLocalToken(token);
            FucineExp<bool> filter = __instance.RetrieveProperty<FucineExp<bool>>(SLOT_ENTRANCE_REQS);
            bool filterFailed = !filter.isUndefined && filter.value == false;
            Crossroads.ResetCache();

            if (filterFailed)
            {
                __result = new ContainerMatchForStack(new List<string>(), SlotMatchForAspectsType.InvalidToken);
                return false;
            }

            return true;
        }
    }
}
