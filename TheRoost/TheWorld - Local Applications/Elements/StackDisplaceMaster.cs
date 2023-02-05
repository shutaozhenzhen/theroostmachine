using System.Collections.Generic;

using System.Reflection;
using System.Reflection.Emit;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Spheres;
using SecretHistories.Abstract;
using SecretHistories.Entities.NullEntities;

using HarmonyLib;

namespace Roost.World.Elements
{
    public static class StackDisplaceMaster
    {
        public const string DISPLACE_TO = "displaceTo";
        public const string DISPLACEMENT_VFX = "displacementVFX";
        public const string DISPLACEMENT_REVERSE = "reverseDisplacement";

        internal static void Enact()
        {
            //vfx for uniquenessgroup's displacements
            Machine.ClaimProperty<Element, string>(DISPLACE_TO);
            Machine.ClaimProperty<Element, RetirementVFX>(DISPLACEMENT_VFX, false, RetirementVFX.CardHide);
            Machine.ClaimProperty<Element, bool>(DISPLACEMENT_REVERSE, false, false);

            Machine.Patch(
                original: typeof(Sphere).GetMethodInvariant(nameof(Sphere.EnforceUniquenessForIncomingStack)),
                transpiler: typeof(StackDisplaceMaster).GetMethodInvariant(nameof(TryDisplaceDuplicate)));
        }

        private static IEnumerable<CodeInstruction> TryDisplaceDuplicate(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
              new CodeInstruction(OpCodes.Ldarg_1),
              new CodeInstruction(OpCodes.Ldloc_2),
              new CodeInstruction(OpCodes.Call, typeof(StackNoStackMaster).GetMethodInvariant(nameof(DisplaceStack))),
            };

            //do it twice for two calls
            MethodInfo retireMethod = typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.Retire), new System.Type[] { typeof(RetirementVFX) });
            instructions = instructions.ReplaceMethodCall(retireMethod, myCode);
            instructions = instructions.ReplaceMethodCall(retireMethod, myCode);

            return instructions;
        }


        public static bool DisplaceStack(ITokenPayload incomingStack, ElementStack affectedStack)
        {
            Element element = Machine.GetEntity<Element>(affectedStack.EntityId);

            if (element.RetrieveProperty<bool>(DISPLACEMENT_REVERSE) == true)
            {
                affectedStack = incomingStack as ElementStack;
                element = Machine.GetEntity<Element>(affectedStack.EntityId);
            }

            string displaceTo = element.RetrieveProperty<string>(DISPLACE_TO);
            RetirementVFX vfx = element.RetrieveProperty<RetirementVFX>(DISPLACEMENT_VFX);

            element = Machine.GetEntity<Element>(displaceTo);

            //if ugroup is involved, element we're displacing into can have it too, thus creating chain of displacements;
            //we need to perform them all at once, otherwise there'll be two tokens with the same group
            if (string.IsNullOrEmpty(affectedStack.UniquenessGroup) == false)
                while (!string.IsNullOrWhiteSpace(displaceTo) && element.UniquenessGroup == affectedStack.UniquenessGroup)
                {
                    displaceTo = element.RetrieveProperty<string>(DISPLACE_TO);
                    element = Machine.GetEntity<Element>(displaceTo);
                }

            if (element.Id == NullElement.NULL_ELEMENT_ID)
            {
                Birdsong.TweetLoud($"Trying to displace {affectedStack.EntityId} into non-existent element {displaceTo}");
                affectedStack.Retire(RetirementVFX.CardHide);
                return false;
            }

            if (string.IsNullOrWhiteSpace(displaceTo))
                affectedStack.Retire(vfx);
            else
            {
                ElementVFXMaster.StoreVFXForCurrentTransformation(vfx);
                affectedStack.ChangeTo(displaceTo);
            }

            return false;
        }
    }

}
