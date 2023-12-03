using SecretHistories.Entities;
using SecretHistories.Spheres;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Roost.World
{
    internal class OffstageInstantiator
    {
        public static void Enact()
        {
            AtTimeOfPower.TabletopSceneInit.Schedule(InstantiateOffstageSphere, PatchType.Postfix);
        }

        public static void InstantiateOffstageSphere()
        {
            FucineRoot root = FucineRoot.Get();
            root.DealersTable.TryCreateOrRetrieveSphere(new SphereSpec(typeof(OffstageSphere), "offstage"));
        }
    }
}
