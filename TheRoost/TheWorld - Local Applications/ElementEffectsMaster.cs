using System.Collections.Generic;

using System.Reflection;
using System.Reflection.Emit;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Spheres;
using SecretHistories.Abstract;

using HarmonyLib;

namespace Roost.World.Elements
{
    public static class ElementEffectsMaster
    {
        public const string DECAY_VFX = "decayvfx";

        public const string DISPLACE_TO = "displaceTo";
        public const string DISPLACEMENT_VFX = "displacementVFX";
        public const string DISPLACEMENT_REVERSE = "reverseDisplacement";

        public const string SHROUDED = "shrouded";
        //CardBurn,	CardBlood,	CardBloodSplatter, CardDrown, CardLight, CardLightDramatic,	CardSpend, CardTaken, CardTakenShadow,
        //CardTakenShadowSlow, CardTransformWhite, CardHide, Default, None
        internal static void Enact()
        {
            //vfx for decay retirements and transformation
            Machine.ClaimProperty<Element, RetirementVFX>(DECAY_VFX, false, RetirementVFX.CardBurn);

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
                original: typeof(Sphere).GetMethodInvariant(nameof(Sphere.EnforceUniquenessForIncomingStack)),
                transpiler: typeof(ElementEffectsMaster).GetMethodInvariant(nameof(TryDisplaceDuplicate)));

            //shroud override
            Machine.ClaimProperty<Element, bool>(SHROUDED, false, true);

            Machine.Patch(
                original: typeof(SituationStorageSphere).GetMethodInvariant(nameof(Sphere.AcceptToken)),
                prefix: typeof(ElementEffectsMaster).GetMethodInvariant(nameof(StorageShroudOverride)));
            Machine.Patch(
                original: typeof(OutputSphere).GetMethodInvariant(nameof(Sphere.AcceptToken)),
                prefix: typeof(ElementEffectsMaster).GetMethodInvariant(nameof(OutputShroudOverride)));
        }

        private static void StorageShroudOverride(Token token, ref Context context)
        {
            bool shroud = Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId).RetrieveProperty<bool>(SHROUDED);
            if (!shroud && context.actionSource == Context.ActionSource.SituationEffect)
                context.actionSource = Context.ActionSource.Unknown;
        }

        private static void OutputShroudOverride(Token token)
        {
            if (token.Shrouded() && !Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId).RetrieveProperty<bool>(SHROUDED))
                token.Unshroud();
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
            Vagabond.CodeInstructionMask startMask = instruction => instruction.Calls(typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.Retire), new System.Type[] { typeof(RetirementVFX) }));
            Vagabond.CodeInstructionMask endMask = instruction => instruction.opcode == OpCodes.Pop;
            instructions = instructions.ReplaceSegment(startMask, endMask, retireCode, true, true, -2);

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

        private static IEnumerable<CodeInstruction> TryDisplaceDuplicate(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
              new CodeInstruction(OpCodes.Ldarg_1),
              new CodeInstruction(OpCodes.Ldloc_2),
              new CodeInstruction(OpCodes.Call, typeof(ElementEffectsMaster).GetMethodInvariant(nameof(DisplaceStack))),
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
                Birdsong.Sing($"Trying to displace {affectedStack.EntityId} into non-existent element {displaceTo}");
                affectedStack.Retire(RetirementVFX.CardHide);
                return false;
            }

            if (string.IsNullOrWhiteSpace(displaceTo))
                affectedStack.Retire(vfx);
            else
            {
                StoreVFXForCurrentTransformation(vfx);
                affectedStack.ChangeTo(displaceTo);
            }

            return false;
        }
    }

}
