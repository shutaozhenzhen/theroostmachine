using System.Collections.Generic;

using System.Reflection;
using System.Reflection.Emit;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Manifestations;
using Assets.Scripts.Application.Infrastructure.Events;

using HarmonyLib;

namespace Roost.World.Elements
{
    public static class CardVFXMaster
    {
        const string ELEMENT_DECAY_VFX = "decayvfx";
        //CardBurn,	CardBlood,	CardBloodSplatter, CardDrown, CardLight, CardLightDramatic,	CardSpend, CardTaken, CardTakenShadow,
        //CardTakenShadowSlow, CardTransformWhite, CardHide, Default, None

        internal static void Enact()
        {
            Machine.ClaimProperty<Element, RetirementVFX>(ELEMENT_DECAY_VFX);

            //vfx for decay
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant("ExecuteHeartbeat"),
                transpiler: typeof(CardVFXMaster).GetMethodInvariant("ElementDecayTranspiler"));

            //vfx for transformations
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant("ChangeTo"),
                prefix: typeof(CardVFXMaster).GetMethodInvariant("StoreOldElementVFX"));

            Machine.Patch(
                original: typeof(Token).GetMethodInvariant("OnPayloadChanged"),
                transpiler: typeof(CardVFXMaster).GetMethodInvariant("RemanifestWithVFXTranspiler"));

            Machine.Patch(
                original: typeof(Token).GetMethodInvariant("ReplaceManifestation"),
                prefix: typeof(CardVFXMaster).GetMethodInvariant("PlaceVFXOnTop"));
        }

        private static IEnumerable<CodeInstruction> ElementDecayTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
              new CodeInstruction(OpCodes.Ldarg_0),
              new CodeInstruction(OpCodes.Ldarg_0),
              new CodeInstruction(OpCodes.Call, typeof(ElementStack).GetMethodInvariant(("get_Element"))),
              new CodeInstruction(OpCodes.Call, typeof(CardVFXMaster).GetMethodInvariant("RetireWithVFX")),
            };

            MethodInfo methodCallToReplace = typeof(ElementStack).GetMethodInvariant("Retire", typeof(SecretHistories.Enums.RetirementVFX));
            return instructions.ReplaceMethodCall(methodCallToReplace, myCode);
        }

        private static void RetireWithVFX(ElementStack stack, Element element)
        {
            if (element.HasCustomProperty(ELEMENT_DECAY_VFX))
                stack.Retire(element.RetrieveProperty<RetirementVFX>(ELEMENT_DECAY_VFX));
            else
                stack.Retire(RetirementVFX.CardBurn);
        }

        private static IEnumerable<CodeInstruction> RemanifestWithVFXTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
              new CodeInstruction(OpCodes.Ldarg_0),
              new CodeInstruction(OpCodes.Ldarg_1),
              new CodeInstruction(OpCodes.Call, typeof(CardVFXMaster).GetMethodInvariant("RemanifestWithVFX")),
            };

            MethodInfo methodCallToReplace = typeof(Token).GetMethodInvariant("Remanifest");
            return instructions.ReplaceMethodCall(methodCallToReplace, myCode);
        }

        //the cleaner solution would be to pass VFX with TokenPayloadChangeArgs
        //but the vanilla code does everything to ensure that doing that will be the most horrible, most annoying pain in the ay which CS modding community ever faced since $ ops incompatibility bug was discovered
        private static RetirementVFX VFXforCurrentTransformation = RetirementVFX.CardBurn;
        private static void StoreOldElementVFX(ElementStack __instance)
        {
            Element transformedElement = Watchman.Get<Compendium>().GetEntityById<Element>(__instance.EntityId);
            if (transformedElement.HasCustomProperty(ELEMENT_DECAY_VFX))
                VFXforCurrentTransformation = transformedElement.RetrieveProperty<RetirementVFX>(ELEMENT_DECAY_VFX);
            else
                VFXforCurrentTransformation = RetirementVFX.CardTransformWhite;
        }

        private static void RemanifestWithVFX(Token token, TokenPayloadChangedArgs args)
        {
            token.Remanifest(VFXforCurrentTransformation);
        }

        private static void PlaceVFXOnTop(IManifestation oldManifestation)
        {
            oldManifestation.Transform.SetAsLastSibling();
        }
    }

}
