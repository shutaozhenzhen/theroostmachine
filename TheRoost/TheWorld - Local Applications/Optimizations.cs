using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Entities;
using SecretHistories.Commands;

namespace Roost.World
{
    internal static class Optimizations
    {
        const string LEGACY_FAMILY = nameof(Legacy.Family);
        const string DECK_LEGACY_FAMILY = nameof(DeckSpec.ForLegacyFamily);
        internal static void Enact()
        {
            Machine.Patch(
                original: typeof(Sphere).GetMethodInvariant(nameof(Sphere.AcceptToken)),
                prefix: typeof(Optimizations).GetMethodInvariant(nameof(EnableNonDormantTokens)),
                postfix: typeof(Optimizations).GetMethodInvariant(nameof(DisableDormantTokens)));

            Machine.ClaimProperty<Legacy, List<string>>(LEGACY_FAMILY);
            Machine.Patch(
                original: typeof(Legacy).GetPropertyInvariant(LEGACY_FAMILY).GetGetMethod(),
                prefix: typeof(Optimizations).GetMethodInvariant(nameof(GetLegacyFamily)));

            Machine.ClaimProperty<DeckSpec, List<string>>(DECK_LEGACY_FAMILY);
            Machine.Patch(
                original: typeof(DeckSpec).GetPropertyInvariant(DECK_LEGACY_FAMILY).GetGetMethod(),
                prefix: typeof(Optimizations).GetMethodInvariant(nameof(GetDeckFamily)));

            //the changes are nigh identical, only arguments we need to access are different
            Machine.Patch(
                original: typeof(RootPopulationCommand).GetMethodInvariant("DealersTableForLegacy"),
                transpiler: typeof(Optimizations).GetMethodInvariant(nameof(CheckActiveDecksOnNewGame)));

            Machine.Patch(
                original: typeof(PetromnemeImporter).GetMethodInvariant("AddDecksToRootCommand"),
                transpiler: typeof(Optimizations).GetMethodInvariant(nameof(CheckActiveDecksForSaveConversion)));
        }

        private static void EnableNonDormantTokens(Sphere __instance, Token token)
        {
            if (__instance.SphereCategory != SecretHistories.Enums.SphereCategory.Dormant)
                token.gameObject.SetActive(true);
        }

        private static void DisableDormantTokens(Sphere __instance, Token token)
        {
            if (__instance.SphereCategory == SecretHistories.Enums.SphereCategory.Dormant)
                token.gameObject.SetActive(false);
        }

        private static bool GetLegacyFamily(Legacy __instance, ref string __result)
        {
            __result = __instance.RetrieveProperty<List<string>>(LEGACY_FAMILY)?[0] ?? "";
            return false;
        }

        private static bool GetDeckFamily(DeckSpec __instance, ref string __result)
        {
            __result = __instance.RetrieveProperty<List<string>>(DECK_LEGACY_FAMILY)?[0] ?? "";
            return false;
        }

        private static IEnumerable<CodeInstruction> CheckActiveDecksOnNewGame(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Ldarg_0), //method is static, Ldarg_0 isn't instance
                new CodeInstruction(OpCodes.Call, typeof(Recipes.Legerdemain).GetMethodInvariant(nameof(Recipes.Legerdemain.DeckIsActiveForLegacy))),
            };

            Vagabond.CodeInstructionMask startMask = instruction => instruction.opcode == OpCodes.Ldloc_2;
            Vagabond.CodeInstructionMask endMask = instruction => instruction.opcode == OpCodes.Brfalse_S;
            return instructions.ReplaceSegment(startMask, endMask, myCode, true, false);
        }

        private static IEnumerable<CodeInstruction> CheckActiveDecksForSaveConversion(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldloc_3),
                new CodeInstruction(OpCodes.Ldarg_3),
                new CodeInstruction(OpCodes.Call, typeof(Recipes.Legerdemain).GetMethodInvariant(nameof(Recipes.Legerdemain.DeckIsActiveForLegacy))),
            };

            Vagabond.CodeInstructionMask startMask = instruction => instruction.opcode == OpCodes.Ldloc_3;
            Vagabond.CodeInstructionMask endMask = instruction => instruction.opcode == OpCodes.Brfalse_S;
            return instructions.ReplaceSegment(startMask, endMask, myCode, true, false);
        }
    }
}
