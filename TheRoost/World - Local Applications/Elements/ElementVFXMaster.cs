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
            Machine.ClaimProperty<Element, RetirementVFX>(DECAY_VFX, false, RetirementVFX.CardBurn);

            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.ExecuteHeartbeat)),
                transpiler: typeof(ElementVFXMaster).GetMethodInvariant(nameof(DecayElementStackWithVFX)));

            Machine.Patch(
                original: typeof(Token).GetMethodInvariant("Remanifest"),
                prefix: typeof(ElementVFXMaster).GetMethodInvariant(nameof(SetVFXForTransformation)));

        }


        //the cleaner solution would be to pass VFX with TokenPayloadChangeArgs
        //but the code has compiled in an absolutely atrocious way and transpiling is effectively rewriting it
        private static RetirementVFX VFXforCurrentTransformation = RetirementVFX.CardBurn;
        public static void StoreVFXForCurrentTransformation(RetirementVFX vfx)
        {
            VFXforCurrentTransformation = vfx;
        }

        private static IEnumerable<CodeInstruction> DecayElementStackWithVFX(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> retireCode = new List<CodeInstruction>()
            {
              new CodeInstruction(OpCodes.Ldarg_0),
              new CodeInstruction(OpCodes.Ldarg_0),
              new CodeInstruction(OpCodes.Call, typeof(ElementStack).GetMethodInvariant("get_Element")),
              new CodeInstruction(OpCodes.Call, typeof(ElementVFXMaster).GetMethodInvariant(nameof(RetireWithVFX))),
            };

            //technically I can just pass the appropriate vfx to the original retire method instead of replacing it completely
            //but it's safer to look for the method call
            Vagabond.CodeInstructionMask startMask = instruction => instruction.Calls(typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.Retire), new System.Type[] { typeof(RetirementVFX) }));
            Vagabond.CodeInstructionMask endMask = instruction => instruction.opcode == OpCodes.Pop;
            instructions = instructions.ReplaceSegment(startMask, endMask, retireCode, true, true, -2);

            List<CodeInstruction> changeToCode = new List<CodeInstruction>()
            {
              new CodeInstruction(OpCodes.Ldarg_0),
              new CodeInstruction(OpCodes.Call, typeof(ElementStack).GetMethodInvariant("get_Element")),
              new CodeInstruction(OpCodes.Call, typeof(ElementVFXMaster).GetMethodInvariant(nameof(StoreVFXForStackDecayPayloadChange))),
            };

            instructions = instructions.InsertBeforeMethodCall(typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.ChangeTo)), changeToCode);
            return instructions;
        }

        private static void RetireWithVFX(ElementStack stack, Element element)
        {
            stack.Retire(element.RetrieveProperty<RetirementVFX>(DECAY_VFX));
        }

        private static void StoreVFXForStackDecayPayloadChange(Element element)
        {
            if (element.HasCustomProperty(DECAY_VFX))
                StoreVFXForCurrentTransformation(element.RetrieveProperty<RetirementVFX>(DECAY_VFX));
            else
                StoreVFXForCurrentTransformation(RetirementVFX.CardTransformWhite);
        }

        public static void SetVFXForTransformation(ref RetirementVFX vfx)
        {
            vfx = VFXforCurrentTransformation;
        }

        private static IEnumerable<CodeInstruction> RemanifestWithVFXTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
              new CodeInstruction(OpCodes.Ldarg_0),
              new CodeInstruction(OpCodes.Call, typeof(ElementVFXMaster).GetMethodInvariant(nameof(RemanifestWithVFX))),
            };

            Vagabond.CodeInstructionMask mask = instruction => instruction.operand as MethodInfo == typeof(Token).GetMethodInvariant(nameof(Token.Remanifest));
            return instructions.ReplaceInstruction(mask, myCode);
        }

        private static void RemanifestWithVFX(Token token)
        {
            token.Remanifest(VFXforCurrentTransformation);
        }
    }

}
