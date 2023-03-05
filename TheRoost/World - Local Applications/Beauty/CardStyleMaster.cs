﻿using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.IO;

using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.Ghosts;
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
    static class CardStyleMaster
    {

        const string USE_BIG_PICTURE = "useBigPicture";
        const string HIDE_LABEL = "hideLabel";
        const string ALTERNATIVE_TEXT_STYLE = "useAlternativeTextStyle";
        const string BIG_PICTURE_FOLDER = "elementsbig";

        internal static void Enact()
        {
            Machine.ClaimProperty<Element, bool>(USE_BIG_PICTURE, defaultValue: false);
            Machine.ClaimProperty<Element, bool>(HIDE_LABEL, defaultValue: false);
            Machine.ClaimProperty<Element, bool>(ALTERNATIVE_TEXT_STYLE, defaultValue: false);

            Machine.Patch(
                    original: typeof(CardManifestation).GetMethodInvariant(nameof(CardManifestation.Initialise), typeof(IManifestable)),
                    postfix: typeof(CardStyleMaster).GetMethodInvariant(nameof(PatchTheCardContainingObject)));

            Machine.Patch(
                    original: typeof(CardManifestation).GetMethodInvariant(nameof(CardManifestation.Initialise), typeof(IManifestable)),
                    postfix: typeof(CardStyleMaster).GetMethodInvariant(nameof(UpdateAnimFrames)));

            Machine.Patch(
                    original: typeof(CardGhost).GetMethodInvariant(nameof(CardGhost.UpdateVisuals), typeof(IManifestable)),
                    postfix: typeof(CardStyleMaster).GetMethodInvariant(nameof(PatchTheCardContainingObject)));


        }

        public static void PatchTheCardContainingObject(MonoBehaviour __instance, IManifestable manifestable)
        {
            ApplyStyle(manifestable, __instance.gameObject);
        }

        public static void ApplyStyle(IManifestable manifestable, GameObject o)
        {
            ElementStack stack = (ElementStack)manifestable;
            Element element = Watchman.Get<Compendium>().GetEntityById<Element>(stack.EntityId);

            GameObject card = o.FindInChildren("Card", true);
            ApplyBigPicture(element, stack, card);
            ApplyHideLabel(element, card);
            ApplyAlternativeTextStyle(element, card);
        }

        public static void ApplyBigPicture(Element element, ElementStack stack, GameObject card)
        {
            if (!element.RetrieveProperty<bool>(USE_BIG_PICTURE))
                return;

            Sprite tableImage = ResourcesManager.GetSprite(BIG_PICTURE_FOLDER, stack.Icon);

            card.FindInChildren("TextBG").SetActive(false);
            card.FindInChildren("Artwork").GetComponent<Image>().sprite = tableImage;
            var rt = card.FindInChildren("Artwork").GetComponent<RectTransform>();
            var sizeDelta = rt.sizeDelta;
            sizeDelta.y = 113;
            rt.sizeDelta = sizeDelta;
        }

        public static void ApplyHideLabel(Element element, GameObject card)
        {
            if (!element.RetrieveProperty<bool>(HIDE_LABEL))
                return;

            card.FindInChildren("Text").SetActive(false);
        }

        public static void ApplyAlternativeTextStyle(Element element, GameObject card)
        {
            if (!element.RetrieveProperty<bool>(ALTERNATIVE_TEXT_STYLE))
                return;

            TextMeshProUGUI t = card.FindInChildren("Text").GetComponent<TextMeshProUGUI>();
            t.color = Color.white;
            t.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            t.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.2f);
        }

        public static void UpdateAnimFrames(CardManifestation __instance, IManifestable manifestable, List<Sprite> ___frames)
        {
            ElementStack stack = manifestable as ElementStack;

            if (stack == null)
                return;
            if (!Watchman.Get<Compendium>().GetEntityById<Element>(stack.EntityId).RetrieveProperty<bool>(USE_BIG_PICTURE))
                return;

            ___frames.Clear();
            ___frames.AddRange(GetAnimFramesForBigPicture(stack.Icon));
        }

        public static List<Sprite> GetAnimFramesForBigPicture(string iconId)
        {
            string animFolder = Path.Combine(BIG_PICTURE_FOLDER, "anim");
            List<Sprite> frames = new List<Sprite>();

            int i = 0;

            while (true)
            {
                Sprite frame = ResourcesManager.GetSprite(animFolder, iconId + "_" + i, false);

                if (frame == null)
                    break;

                frames.Add(frame);
                i++;
            }

            return frames;
        }
    }

    static class ElementStackRefiner
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

            //illuminations are set on creation
            Machine.Patch(
                original: typeof(ElementStackCreationCommand).GetMethodInvariant(nameof(ElementStackCreationCommand.Execute)),
                transpiler: typeof(ElementStackRefiner).GetMethodInvariant(nameof(RefineStackOnCreation)));

            //illuminations are updated on Element change
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.ChangeTo)),
                transpiler: typeof(ElementStackRefiner).GetMethodInvariant(nameof(RefineStackOnTransformation)));

            //...and after each mutation
            //it's a rare thing in this set of changes that is not optimal - refinement process will happen several times if several mutations were applied
            //but I doubt that refined cards will be heavily mutated in one go
            //and besides, it's harder to accesss illuminations from other contexts
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.SetMutation)),
                transpiler: typeof(ElementStackRefiner).GetMethodInvariant(nameof(RefineStackOnMutation)));

            //hooking the refined elements to TokenDetailsWindow
            Machine.Patch(
                original: typeof(TokenDetailsWindow).GetMethodInvariant("SetElementCard"),
                transpiler: typeof(ElementStackRefiner).GetMethodInvariant(nameof(SetRefinedValuesForTokenDetailsWindow)));

            //the refined icon is displayed correctly in StoredManifestation
            Machine.Patch(
                original: typeof(CardManifestation).GetMethodInvariant(nameof(CardManifestation.Initialise)),
                transpiler: typeof(ElementStackRefiner).GetMethodInvariant(nameof(SetIconFromManifestation)));

            Machine.Patch(
                original: typeof(CardManifestation).GetMethodInvariant(nameof(CardManifestation.Initialise)),
                transpiler: typeof(ElementStackRefiner).GetMethodInvariant(nameof(SetAnimFromManifestation)));

            Machine.Patch(
                original: typeof(StoredManifestation).GetMethodInvariant(nameof(StoredManifestation.UpdateVisuals)),
                transpiler: typeof(ElementStackRefiner).GetMethodInvariant(nameof(SetRefinedIconForStoredManifestation)));

            Machine.Patch(
                original: typeof(ResourcesManager).GetMethodInvariant(nameof(ResourcesManager.GetSprite)),
                prefix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(LastStand)));

            //the refined icon and label are displayed correctly for Accessible Card Text
            Machine.Patch(
                original: typeof(CardManifestation).GetMethodInvariant(nameof(CardManifestation.OnPointerEnter)),
                prefix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(StoreIconAndLabelForMagnifyingGlass)));

            Machine.Patch(
                original: typeof(ElementStackSimple).GetMethodInvariant(nameof(ElementStackSimple.Populate)),
                postfix: typeof(ElementStackRefiner).GetMethodInvariant(nameof(SetIconAndLabelForMagnifyingGlass)));

            AtTimeOfPower.NewGameSceneInit.Schedule(ResetIconAndLabelForElementStackSimple, PatchType.Prefix);
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

        private static void UpdateRefinementsForStack(Dictionary<string, string> illuminations, ElementStack stack, Element element)
        {
            element.TryRetrieveProperty(REFINED, out bool refined);

            if (!refined)
            {
                illuminations.Remove(DYNAMIC_LABEL);
                illuminations.Remove(DYNAMIC_ICON);
                illuminations.Remove(DYNAMIC_DESCRIPTION);
                return;
            }

            AspectsDictionary currentAspects = new AspectsDictionary() { { element.Id, 1 } };
            currentAspects.CombineAspects(element.Aspects);
            currentAspects.ApplyMutations(stack.Mutations);

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

        private static IEnumerable<CodeInstruction> RefineStackOnCreation(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Call, typeof(ElementStackCreationCommand).GetPropertyInvariant(nameof(ElementStackCreationCommand.Illuminations)).GetGetMethod()),
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Call, typeof(ElementStackRefiner).GetMethodInvariant(nameof(UpdateRefinementsForStack))),
                new CodeInstruction(OpCodes.Ldarg_0),
            };

            Vagabond.CodeInstructionMask mask = code => code.Calls(typeof(ElementStackCreationCommand).GetPropertyInvariant(nameof(ElementStackCreationCommand.Illuminations)).GetGetMethod());
            return instructions.InsertBefore(mask, myCode);
        }

        private static IEnumerable<CodeInstruction> RefineStackOnTransformation(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(ElementStack).GetFieldInvariant("_illuminations")),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Call, typeof(ElementStackRefiner).GetMethodInvariant(nameof(UpdateRefinementsForStack))),
            };

            Vagabond.CodeInstructionMask mask = code => code.opcode == OpCodes.Dup;
            return instructions.InsertBefore(mask, myCode, -2);
        }

        private static IEnumerable<CodeInstruction> RefineStackOnMutation(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(ElementStack).GetFieldInvariant("_illuminations")),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, typeof(ElementStack).GetPropertyInvariant("Element").GetGetMethod(true)),
                new CodeInstruction(OpCodes.Call, typeof(ElementStackRefiner).GetMethodInvariant(nameof(UpdateRefinementsForStack))),
            };

            Vagabond.CodeInstructionMask mask = code => code.opcode == OpCodes.Ret;
            return instructions.InsertBefore(mask, myCode);
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

        private static IEnumerable<CodeInstruction> SetRefinedIconForStoredManifestation(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(StoredManifestation).GetFieldInvariant("aspectImage")),
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Isinst, typeof(ElementStack)),
                new CodeInstruction(OpCodes.Call, typeof(ElementStackRefiner).GetMethodInvariant(nameof(GetRefinedSprite))),
                new CodeInstruction(OpCodes.Callvirt, typeof(Image).GetPropertyInvariant(nameof(Image.sprite)).GetSetMethod())
            };

            Vagabond.CodeInstructionMask start = code => code.opcode == OpCodes.Ldarg_0;
            Vagabond.CodeInstructionMask end = code => code.Calls(typeof(StoredManifestation).GetMethodInvariant("DisplayImage"));
            return instructions.ReplaceSegment(start, end, myCode, true, true); ;
        }

        static Sprite currentSprite = null;
        static string currentLabel = null;
        private static void StoreIconAndLabelForMagnifyingGlass(Image ___artwork, TextMeshProUGUI ___text)
        {
            currentSprite = ___artwork.sprite;
            currentLabel = ___text.text;
        }

        private static void SetIconAndLabelForMagnifyingGlass(Image ___artwork, TextMeshProUGUI ___text)
        {
            //ElementStackSimple, as it turns out, is shared between the Meniscate and the legacy startin items preview;
            //we don't want to modify that last one, so we set it to null once we reach 
            if (currentSprite != null)
            {
                ___text.text = currentLabel;
                ___artwork.sprite = currentSprite;
            }
        }

        private static IEnumerable<CodeInstruction> SetIconFromManifestation(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, typeof(ElementStackRefiner).GetMethodInvariant(nameof(GetElementIcon))),
            };

            var methodToReplace = typeof(ResourcesManager).GetMethodInvariant(nameof(ResourcesManager.GetSpriteForElement), new Type[] { typeof(Element) });
            return instructions.ReplaceMethodCall(methodToReplace, myCode);
        }

        private static Sprite GetElementIcon(IManifestable manifestable)
        {
            if (Watchman.Get<Compendium>().GetEntityById<Element>(manifestable.EntityId).IsAspect)
                return ResourcesManager.GetSpriteForAspect(manifestable.Icon);

            return ResourcesManager.GetSpriteForElement(manifestable.Icon);
        }

        private static IEnumerable<CodeInstruction> SetAnimFromManifestation(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> myCode = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Callvirt, typeof(IManifestable).GetPropertyInvariant(nameof(IManifestable.Icon)).GetGetMethod()),
                new CodeInstruction(OpCodes.Call, typeof(ResourcesManager).GetMethodInvariant(nameof(ResourcesManager.GetAnimFramesForElement))),
            };

            var methodToReplace = typeof(ResourcesManager).GetMethodInvariant(nameof(ResourcesManager.GetAnimFramesForElement));
            return instructions.ReplaceMethodCall(methodToReplace, myCode);
        }

        //in come context, unrefined icon can leak through all precations and end up in resource manager
        //in case this happened, do this one refinement again
        private static void LastStand(ref string file)
        {
            if (file.Contains("@"))
                file = "_x";
        }


        private static void ResetIconAndLabelForElementStackSimple()
        {
            currentSprite = null;
            currentLabel = null;
        }
    }
}