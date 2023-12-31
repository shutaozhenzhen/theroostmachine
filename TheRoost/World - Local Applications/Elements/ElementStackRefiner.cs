﻿using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.Manifestations;
using SecretHistories.UI;
using SecretHistories.Core;
using SecretHistories.Commands;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

using HarmonyLib;

using Roost.Elegiast;

namespace Roost.World.Beauty
{
    public static class ElementStackRefiner
    {
        const string DYNAMIC_LABEL = "dynamiclabel";
        const string DYNAMIC_ICON = "dynamicicon";
        const string DYNAMIC_DESCRIPTION = "description";
        const string REFINED = "refined";

        internal static void Enact()
        {
            Machine.ClaimProperty<Element, string>(DYNAMIC_LABEL, true);
            Machine.ClaimProperty<Element, string>(DYNAMIC_ICON);
            Machine.ClaimProperty<Element, bool>(REFINED, false, false); //set automatically if there are refinements

            Machine.AddImportMolding<Element>(ElementNeedsRefinements);

            //refined values are returned from illuminations
            Machine.Patch(
                original: typeof(ElementStack).GetPropertyInvariant(nameof(ElementStack.Label)).GetGetMethod(),
                prefix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(GetRefinedLabel)));

            Machine.Patch(
                original: typeof(ElementStack).GetPropertyInvariant(nameof(ElementStack.Description)).GetGetMethod(),
                prefix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(GetRefinedDescription)));

            Machine.Patch(
                original: typeof(ElementStack).GetPropertyInvariant(nameof(ElementStack.Icon)).GetGetMethod(),
                prefix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(GetRefinedIcon)));

            //refinements are set on creation
            Machine.Patch(
                original: Machine.GetMethod<ElementStackCreationCommand>(nameof(ElementStackCreationCommand.Execute)),
                prefix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(UpdateRefinementsOnCreation)));

            //...updated on Element change
            Machine.Patch(
                original: Machine.GetMethod<ElementStack>(nameof(ElementStack.ChangeTo)),
                postfix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(UpdateRefinementsOnChange)));

            //...and after each mutation
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.SetMutation)),
                postfix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(UpdateRefinementsOnChange)));
            //not optimal - refinement process will happen several times if several mutations were applied
            //but it's harder to accesss illuminations from other contexts

            //hooking the refined elements to TokenDetailsWindow
            Machine.Patch(
                original: typeof(TokenDetailsWindow).GetMethodInvariant("SetElementCard"),
                transpiler: typeof(ElementStackRefiner).GetMethodInvariant(nameof(SetRefinedValuesForTokenDetailsWindow)));

            Machine.Patch(
                original: typeof(ResourcesManager).GetMethodInvariant(nameof(GetAppropriateSpriteForElementManifestation)),
                prefix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(GetAppropriateSpriteForElementManifestation)));

            Machine.Patch(
                original: typeof(ResourcesManager).GetMethodInvariant(nameof(ResourcesManager.GetSprite)),
                prefix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(LastStand)));

            //the refined icon and label are displayed correctly for Accessible Card Text
            Machine.Patch(
                original: typeof(ElementStackSimple).GetMethodInvariant(nameof(ElementStackSimple.Populate)),
                postfix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(SetIconAndLabelForMagnifyingGlass)));
        }

        private static bool GetAppropriateSpriteForElementManifestation(IManifestable manifestable, ref Sprite __result)
        {
            Element element = Watchman.Get<Compendium>().GetEntityById<Element>(manifestable.EntityId);
            string icon = manifestable.Icon;

            if (element.IsAspect)
                __result = ResourcesManager.GetSpriteForAspect(icon);
            else
                __result = ResourcesManager.GetSpriteForElement(icon);


            if (element.ManifestationType == "Card" || __result.name != "_x")
                return false;

            __result = ResourcesManager.GetSprite(ResourcesManager.GetFolderForManifestationSprites(element.ManifestationType), icon, true);

            return false;
        }

        private static void ElementNeedsRefinements(SecretHistories.Fucine.DataImport.EntityData entityData)
        {
            if (entityData.ContainsKey(DYNAMIC_LABEL))
            {
                entityData["label"] = entityData[DYNAMIC_LABEL];
                entityData.ValuesTable.Remove(DYNAMIC_LABEL);
            }

            if (entityData.ContainsKey(DYNAMIC_ICON))
            {
                entityData["icon"] = entityData[DYNAMIC_ICON];
                entityData.ValuesTable.Remove(DYNAMIC_ICON);
            }

            if (PropertyNeedsRefinement("label") || PropertyNeedsRefinement("description") || PropertyNeedsRefinement("icon"))
                entityData[REFINED] = true;

            bool PropertyNeedsRefinement(string propertyName)
            {
                string value = entityData.ContainsKey(propertyName) ? entityData[propertyName].ToString() : string.Empty;
                return value.Contains("@") || value.Contains("#");
            }
        }

        private static void UpdateRefinementsForStack(Element element, ElementStack stack, AspectsDictionary currentAspects, Dictionary<string, string> illuminations)
        {
            string refinedString;

            refinedString = Scribe.RefineString(element.Label, currentAspects);
            if (refinedString != element.Icon)
                illuminations[DYNAMIC_LABEL] = refinedString;
            else //there's no RemoveIllumination(), can you believe it? 
                illuminations.Remove(DYNAMIC_LABEL);
            stack.UpdateRefinedLabel(refinedString);

            refinedString = Scribe.RefineString(element.Icon, currentAspects);
            if (refinedString != element.Icon)
                illuminations[DYNAMIC_ICON] = refinedString;
            else
                illuminations.Remove(DYNAMIC_ICON);
            stack.UpdateRefinedIcon(refinedString);

            refinedString = Scribe.RefineString(element.Description, currentAspects);
            if (refinedString != element.Description)
                illuminations[DYNAMIC_DESCRIPTION] = refinedString;
            else
                illuminations.Remove(DYNAMIC_DESCRIPTION);
            //description isn't immediately displayed, no need to update
        }

        private static bool GetRefinedLabel(ref string __result, ElementStack __instance)
        {
            __result = __instance.GetIllumination(DYNAMIC_LABEL);
            return __result == null;
        }

        private static bool GetRefinedDescription(ref string __result, ElementStack __instance)
        {
            __result = __instance.GetIllumination(DYNAMIC_DESCRIPTION);
            return __result == null;
        }

        private static bool GetRefinedIcon(ref string __result, ElementStack __instance)
        {
            __result = __instance.GetIllumination(DYNAMIC_ICON);
            return __result == null;
        }

        public static void UpdateRefinementsOnCreation(ElementStackCreationCommand __instance)
        {
            Element element = Watchman.Get<Compendium>().GetEntityById<Element>(__instance.EntityId);
            Dictionary<string, string> illuminations = __instance.Illuminations;

            element.TryRetrieveProperty(REFINED, out bool refined);

            if (!refined)
            {
                illuminations.Remove(DYNAMIC_LABEL);
                illuminations.Remove(DYNAMIC_ICON);
                illuminations.Remove(DYNAMIC_DESCRIPTION);
                return;
            }

            AspectsDictionary aspects = new AspectsDictionary() { { element.Id, 1 } };
            aspects.CombineAspects(element.Aspects);
            aspects.ApplyMutations(__instance.Mutations);

            UpdateRefinementsForStack(element, null, aspects, __instance.Illuminations);
        }

        public static void UpdateRefinementsOnChange(ElementStack __instance, Dictionary<string, string> ____illuminations)
        {
            __instance.Element.TryRetrieveProperty(REFINED, out bool refined);
            Dictionary<string, string> illuminations = ____illuminations;

            if (!refined)
            {
                illuminations.Remove(DYNAMIC_LABEL);
                illuminations.Remove(DYNAMIC_ICON);
                illuminations.Remove(DYNAMIC_DESCRIPTION);
                return;
            }

            AspectsDictionary aspects = new AspectsDictionary() { { __instance.Element.Id, 1 } };
            aspects.CombineAspects(__instance.Element.Aspects);
            aspects.ApplyMutations(__instance.Mutations);

            UpdateRefinementsForStack(__instance.Element, __instance, aspects, ____illuminations);
        }

        private static void UpdateRefinedLabel(this ElementStack stack, string labelRefined)
        {
            TextMeshProUGUI label = stack?.Token.gameObject.FindInChildren("Text", true)?.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.text = labelRefined;
        }

        private static void UpdateRefinedIcon(this ElementStack stack, string iconRefined)
        {
            Image artwork = stack?.Token.GetComponentInChildren<Image>();

            if (artwork != null)
                artwork.sprite = ResourcesManager.GetSpriteForElement(iconRefined);
        }

        private static IEnumerable<CodeInstruction> SetRefinedValuesForTokenDetailsWindow(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> getImage = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, typeof(ElementStackRefiner).GetMethodInvariant(nameof(GetRefinedSprite))),
                new CodeInstruction(OpCodes.Stloc_0),
            };
            instructions = instructions.ReplaceBeforeMask(code => code.opcode == OpCodes.Ldarg_0, getImage, false);

            List<CodeInstruction> setLabelAndDescription = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Callvirt, typeof(ElementStack).GetPropertyInvariant(nameof(ElementStack.Label)).GetGetMethod()),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Callvirt, typeof(ElementStack).GetPropertyInvariant(nameof(ElementStack.Description)).GetGetMethod()),
                new CodeInstruction(OpCodes.Call, typeof(AbstractDetailsWindow).GetMethodInvariant("ShowText")),
            };

            Vagabond.CodeInstructionMask start = code => code.Calls(typeof(AbstractDetailsWindow).GetMethodInvariant("ShowText"));
            Vagabond.CodeInstructionMask end = code => code.Calls(typeof(AbstractDetailsWindow).GetMethodInvariant("ShowText"));
            instructions = instructions.ReplaceSegment(start, end, setLabelAndDescription, true, true, -4);

            return instructions;
        }

        private static Sprite GetRefinedSprite(Element element, ElementStack stack)
        {
            if (element.IsAspect)
                return ResourcesManager.GetSpriteForAspect(stack.Icon);
            else
                return ResourcesManager.GetSpriteForElement(stack.Icon);
        }

        private static void SetIconAndLabelForMagnifyingGlass(TextMeshProUGUI ___text)
        {
            if (RavensEye.lastHoveredElementStack == null)
                return;

            ___text.text = RavensEye.lastHoveredElementStack.Payload.Label;
        }

        //in come context, unrefined icon can leak through all precations and end up in resource manager
        //in case this happened, do this one refinement again
        private static void LastStand(ref string file)
        {
            if (file.Contains("@"))
                file = "_x";
        }
    }
}
