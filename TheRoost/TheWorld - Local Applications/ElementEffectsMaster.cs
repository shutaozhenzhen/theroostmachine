using System.Collections.Generic;

using System.Reflection;
using System.Reflection.Emit;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using Assets.Scripts.Application.Infrastructure.Events;
using SecretHistories.Spheres;
using SecretHistories.Abstract;

using Roost.Twins.Entities;

using HarmonyLib;

namespace Roost.World.Elements
{
    public static class ElementEffectsMaster
    {
        const string DECAY_VFX = "decayvfx";

        const string DISPLACE_TO = "displaceTo";
        const string DISPLACEMENT_VFX = "displacementVFX";
        const string DISPLACEMENT_REVERSE = "reverseDisplacement";
        //CardBurn,	CardBlood,	CardBloodSplatter, CardDrown, CardLight, CardLightDramatic,	CardSpend, CardTaken, CardTakenShadow,
        //CardTakenShadowSlow, CardTransformWhite, CardHide, Default, None
        internal static void Enact()
        {
            Machine.ClaimProperty<Element, RetirementVFX>(DECAY_VFX, false, RetirementVFX.CardBurn);

            //vfx for decay retirements and transformation
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.ExecuteHeartbeat)),
                transpiler: typeof(ElementEffectsMaster).GetMethodInvariant(nameof(DecayElementStackWithVFX)));

            Machine.Patch(
                original: typeof(Token).GetMethodInvariant("Remanifest"),
                prefix: typeof(ElementEffectsMaster).GetMethodInvariant(nameof(Boken)));

            //vfx for uniquenessgroup's displacements
            Machine.ClaimProperty<Element, string>(DISPLACE_TO);
            Machine.ClaimProperty<Element, RetirementVFX>(DISPLACEMENT_VFX, false, RetirementVFX.CardHide);
            Machine.ClaimProperty<Element, bool>(DISPLACEMENT_REVERSE, false, false);

            Machine.Patch(
                original: typeof(Sphere).GetMethodInvariant(nameof(Sphere.RemoveDuplicates)),
                prefix: typeof(ElementEffectsMaster).GetMethodInvariant(nameof(ApplyDisplacements)));
        }

        //the cleaner solution would be to pass VFX with TokenPayloadChangeArgs
        //but the code has compiled in an absolutely atrocious way and transpiling is effectively rewriting it
        private static RetirementVFX VFXforCurrentTransformation = RetirementVFX.CardBurn;
        private static void StoreVFXForCurrentTransformation(RetirementVFX vfx)
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
              new CodeInstruction(OpCodes.Call, typeof(ElementEffectsMaster).GetMethodInvariant(nameof(RetireWithVFX))),
            };

            //technically I can just pass the appropriate vfx to the original retire method instead of replacing it completely
            //but it's safer to look for the method call
            Vagabond.CodeInstructionMask retireMask = instruction => instruction.operand as MethodInfo == typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.Retire), typeof(RetirementVFX));
            instructions = instructions.ReplaceInstruction(retireMask, retireCode);

            List<CodeInstruction> changeToCode = new List<CodeInstruction>()
            {
              new CodeInstruction(OpCodes.Ldarg_0),
              new CodeInstruction(OpCodes.Call, typeof(ElementStack).GetMethodInvariant("get_Element")),
              new CodeInstruction(OpCodes.Call, typeof(ElementEffectsMaster).GetMethodInvariant(nameof(StoreVFXForStackDecayPayloadChange))),
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

        private static void Boken(ref RetirementVFX vfx)
        {
            vfx = VFXforCurrentTransformation;
        }

        private static IEnumerable<CodeInstruction> RemanifestWithVFXTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
              new CodeInstruction(OpCodes.Ldarg_0),
              new CodeInstruction(OpCodes.Call, typeof(ElementEffectsMaster).GetMethodInvariant(nameof(RemanifestWithVFX))),
            };

            Vagabond.CodeInstructionMask mask = instruction => instruction.operand as MethodInfo == typeof(Token).GetMethodInvariant(nameof(Token.Remanifest));
            return instructions.ReplaceInstruction(mask, myCode);
        }

        private static void RemanifestWithVFX(Token token)
        {
            token.Remanifest(VFXforCurrentTransformation);
        }

        public static bool ApplyDisplacements(ITokenPayload incomingStack, Sphere __instance)
        {
            Sphere sphere = __instance;
            if (!incomingStack.Unique)
                return false;

            bool hasUQ = !string.IsNullOrEmpty(incomingStack.UniquenessGroup);
            foreach (ElementStack tokenPayload in new List<ElementStack>(sphere.GetElementStacks()))
                if (tokenPayload != incomingStack &&
                    ((hasUQ && tokenPayload.UniquenessGroup == incomingStack.UniquenessGroup)
                    || tokenPayload.EntityId == incomingStack.EntityId))
                {
                    ElementStack affectedStack = tokenPayload;
                    Element element = Machine.GetEntity<Element>(tokenPayload.EntityId);

                    if (element == null)
                    {
                        affectedStack.Retire(RetirementVFX.CardHide);
                        return false;
                    }

                    if (element.RetrieveProperty<bool>(DISPLACEMENT_REVERSE) == true)
                    {
                        affectedStack = incomingStack as ElementStack;
                        element = Machine.GetEntity<Element>(incomingStack.EntityId);
                    }

                    string displaceTo = element.RetrieveProperty<string>(DISPLACE_TO);
                    RetirementVFX vfx = element.RetrieveProperty<RetirementVFX>(DISPLACEMENT_VFX);

                    element = Machine.GetEntity<Element>(displaceTo);
                    while (string.IsNullOrWhiteSpace(displaceTo) == false && element?.UniquenessGroup == incomingStack.UniquenessGroup)
                    {
                        displaceTo = element.RetrieveProperty<string>(DISPLACE_TO);
                        element = Machine.GetEntity<Element>(displaceTo);
                    }

                    if (string.IsNullOrWhiteSpace(displaceTo))
                        affectedStack.Retire(vfx);
                    else
                    {
                        StoreVFXForCurrentTransformation(vfx);
                        affectedStack.ChangeTo(displaceTo);
                    }
                }

            return false;
        }
    }

}
