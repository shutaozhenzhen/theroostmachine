using System.Collections.Generic;

using System.Reflection;
using System.Reflection.Emit;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;

using HarmonyLib;

namespace Roost.World.Elements
{
    public static class ElementVFXMaster
    {
        //CardBurn,	CardBlood,	CardBloodSplatter, CardDrown, CardLight, CardLightDramatic,	CardSpend, CardTaken, CardTakenShadow,
        //CardTakenShadowSlow, CardTransformWhite, CardHide, Default, None
        public const string DECAY_VFX = "decayvfx";

        internal static void Enact()
        {
            //vfx for decay retirements and transformation
            Machine.ClaimProperty<Element, RetirementVFX>(DECAY_VFX, false, RetirementVFX.Default);

            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.ExecuteHeartbeat)),
                transpiler: typeof(ElementVFXMaster).GetMethodInvariant(nameof(HeartbeatVFX)));

            Machine.Patch(
                original: typeof(Token).GetMethodInvariant("Remanifest"),
                prefix: typeof(ElementVFXMaster).GetMethodInvariant(nameof(ReplaceRemanifestVFX)));
        }

        private static IEnumerable<CodeInstruction> HeartbeatVFX(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> changeToCode = new List<CodeInstruction>()
            {
              new CodeInstruction(OpCodes.Call, typeof(ElementStack).GetMethodInvariant("get_Element")),
              new CodeInstruction(OpCodes.Call, typeof(ElementVFXMaster).GetMethodInvariant(nameof(OverrideVFXForChangeTo))),
              new CodeInstruction(OpCodes.Ldarg_0),
            };

            Vagabond.CodeInstructionMask startMask = instruction => instruction.Calls(Machine.GetMethod<ElementStack>("ChangeOrRetire"));
            instructions = instructions.InsertBefore(startMask, changeToCode, -4);
            return instructions;
        }

        private static void OverrideVFXForChangeTo(Element element)
        {
            elementVFXOverride = element.RetrieveProperty<RetirementVFX>(DECAY_VFX);
        }

        public static RetirementVFX elementVFXOverride = RetirementVFX.Default;
        public static void ReplaceRemanifestVFX(ref RetirementVFX vfx)
        {
            if (elementVFXOverride != RetirementVFX.Default)
            {
                vfx = elementVFXOverride;
                elementVFXOverride = RetirementVFX.Default;
            }
        }
    }

}
