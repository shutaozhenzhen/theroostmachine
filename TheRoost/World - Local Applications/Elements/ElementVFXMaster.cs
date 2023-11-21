using System.Collections.Generic;

using System.Reflection.Emit;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Spheres;

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

            Machine.Patch<ElementStack>(nameof(ElementStack.ExecuteHeartbeat),
                transpiler: typeof(ElementVFXMaster).GetMethodInvariant(nameof(HeartbeatVFX)));

            Machine.Patch<Token>("Remanifest",
                prefix: typeof(ElementVFXMaster).GetMethodInvariant(nameof(ReplaceRemanifestVFX)));

            Machine.Patch<Xamanek>("DestroyTravelAnimationForToken",
                prefix: typeof(ElementVFXMaster).GetMethodInvariant(nameof(SafeTokenAnimationRetire)));
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

        public static bool SafeTokenAnimationRetire(Token token)
        {
            token.gameObject.GetComponent<TokenTravelAnimation>()?.Retire();
            return false;
        }
    }

}

namespace Roost
{
    public partial class Machine
    {
        public static bool SupportsVFX(this Sphere sphere)
        {
            return sphere.IsExteriorSphere || sphere.SphereCategory == SphereCategory.Threshold; //thresholds aren't always exteriors but we want vfx nevertheless
        }

        public static void AcceptWithVFX(this Sphere sphere, Token token, Context context)
        {
            if (sphere.SupportsVFX())
                SphereMovementVFX(token, sphere, context);
            else if (token.Sphere != sphere)
                sphere.AcceptToken(token, context);
        }

        public static void SphereMovementVFX(this Token token, Sphere sphere, Context context)
        {
            if (!token.Sphere.SupportsVFX())
                token.transform.position = UnityEngine.Vector3.up * 1200;
            token.Payload.GetEnRouteSphere().AcceptToken(token, context);

            if (sphere.IsCategory(SphereCategory.World) || !sphere.CanAcceptToken(token))
                sphere.ProcessEvictedToken(token, context);
            else
                sphere.GetItineraryFor(token).WithDuration(0.2f).Depart(token, context);
        }
    }
}