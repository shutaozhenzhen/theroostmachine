using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Spheres;

namespace Roost.World.Elements
{
    public static class StackShroudMaster
    {
        public const string SHROUDED = "shrouded";

        internal static void Enact()
        {
            //shroud override
            Machine.ClaimProperty<Element, bool>(SHROUDED, false, true);

            Machine.Patch(
                original: typeof(SituationStorageSphere).GetMethodInvariant(nameof(Sphere.AcceptToken)),
                prefix: typeof(StackShroudMaster).GetMethodInvariant(nameof(StorageShroudOverride)));
            Machine.Patch(
                original: typeof(OutputSphere).GetMethodInvariant(nameof(Sphere.AcceptToken)),
                prefix: typeof(StackShroudMaster).GetMethodInvariant(nameof(OutputShroudOverride)));
        }

        private static void StorageShroudOverride(Token token, ref Context context)
        {
            bool shroud = Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId).RetrieveProperty<bool>(SHROUDED);
            if (!shroud && context.actionSource == Context.ActionSource.SituationEffect)
                context.actionSource = Context.ActionSource.Unknown;
        }

        private static void OutputShroudOverride(Token token)
        {
            if (token.Shrouded && !Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId).RetrieveProperty<bool>(SHROUDED))
                token.Unshroud();
        }
    }

}
