﻿using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Collections.Generic;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Infrastructure;

using HarmonyLib;

namespace Roost.Vagabond
{
    public delegate bool CodeInstructionMask(CodeInstruction instruction);
    internal static class HarmonyMask
    {
        private static readonly Dictionary<string, Harmony> patchers = new Dictionary<string, Harmony>();

        public static void Patch(MethodBase original, MethodInfo prefix, MethodInfo postfix, MethodInfo transpiler, MethodInfo finalizer, string patchId)
        {
            if (original == null)
                throw Birdsong.Cack($"Trying to patch null method with {prefix}, {postfix}, {transpiler}, {finalizer}");
            if (prefix == null && postfix == null && transpiler == null && finalizer == null)
                throw Birdsong.Cack($"All patches for {original.Name}() are null!");

            if (patchers.ContainsKey(patchId) == false)
                patchers[patchId] = new Harmony(patchId);

            patchers[patchId].Patch(original,
                prefix: prefix == null ? null : new HarmonyMethod(prefix),
                postfix: postfix == null ? null : new HarmonyMethod(postfix),
                transpiler: transpiler == null ? null : new HarmonyMethod(transpiler),
                finalizer: finalizer == null ? null : new HarmonyMethod(finalizer));
        }

        public static void Unpatch(string patchId)
        {
            if (patchers.ContainsKey(patchId) == false)
                Birdsong.Tweet($"Harmony patch '{patchId}' isn't present in the Roost Machine");
            else if (Harmony.HasAnyPatches(patchId))
                patchers[patchId].UnpatchAll(patchId);
        }

        public static bool HasAnyPatches(string patchId)
        {
            return Harmony.HasAnyPatches(patchId);
        }

        internal static MethodInfo GetMethodInvariant(Type definingClass, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                Birdsong.Tweet($"Trying to find whitespace method for class {definingClass.Name} (don't!)");

            try
            {
                MethodInfo method = definingClass.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

                if (method == null)
                    Birdsong.Tweet($"Method not found");

                return method;
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"Failed to find method '{name}'  in '{definingClass.Name}', reason: {ex.FormatException()}");
            }
        }

        internal static MethodInfo GetMethodInvariant(Type definingClass, string name, params Type[] args)
        {
            try
            {
                MethodInfo method = definingClass.GetMethod(name, args);
                if (method == null)
                    throw Birdsong.Cack("Method not found");
                return method;
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"Failed to find method '{name}' with parameters '{args.UnpackAsString()}' in '{definingClass.Name}', reason: {ex.FormatException()}");
            }
        }

        internal static FieldInfo GetFieldInvariant(this Type definingClass, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                Birdsong.Tweet($"Trying to find whitespace field for class {definingClass.Name} (don't!)");

            FieldInfo field = definingClass.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null)
                Birdsong.Tweet($"Field {name} is not found in class {definingClass.Name}");

            return field;
        }

        internal static PropertyInfo GetPropertyInvariant(this Type definingClass, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                Birdsong.Tweet($"Trying to find whitespace property for class {definingClass.Name} (don't!)");

            PropertyInfo property = definingClass.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

            if (property == null)
                Birdsong.Tweet($"Property {name} not found in class {definingClass.Name}");

            return property;
        }

        private static readonly Dictionary<AtTimeOfPower, MethodBase> methodsToPatch = new Dictionary<AtTimeOfPower, MethodBase>()
        {
 { AtTimeOfPower.QuoteSceneInit, typeof(SplashScreen).GetMethodInvariant("Start") },
 { AtTimeOfPower.MenuSceneInit, typeof(MenuScreenController).GetMethodInvariant("InitialiseServices") },
 { AtTimeOfPower.TabletopSceneInit, typeof(GameGateway).GetMethodInvariant("PopulateEnvironment") },

 { AtTimeOfPower.NewGame, typeof(MenuScreenController).GetMethodInvariant(nameof(MenuScreenController.BeginNewSaveWithSpecifiedLegacy)) },

 { AtTimeOfPower.RecipeRequirementsCheck, typeof(Recipe).GetMethodInvariant(nameof(Recipe.RequirementsSatisfiedBy)) },

 { AtTimeOfPower.RecipeExecution, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("Execute", typeof(Situation)) },
 { AtTimeOfPower.RecipePortals, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("OpenPortals") },
 { AtTimeOfPower.RecipeVFX, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("DoRecipeVfx") },

 { AtTimeOfPower.OnPostImportCulture, typeof(Culture).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportDeck, typeof(DeckSpec).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportElement, typeof(Element).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportEnding, typeof(Ending).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportExpulsion, typeof(Expulsion).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportLegacy, typeof(Legacy).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportLink, typeof(LinkedRecipeDetails).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportXTrigger, typeof(MorphDetails).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportMutation, typeof(MutationEffect).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportPortal, typeof(Portal).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportRecipe, typeof(Recipe).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImpostSetting, typeof(Setting).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportSlot, typeof(SphereSpec).GetMethodInvariant("OnPostImportForSpecificEntity") },
 { AtTimeOfPower.OnPostImportVerb, typeof(Verb).GetMethodInvariant("OnPostImportForSpecificEntity") },
        };

        internal static void Unite(AtTimeOfPower time, Delegate patchMethod, PatchType patchType, string patchId)
        {
            if (patchType == PatchType.Prefix)
                Machine.Patch(methodsToPatch[time], prefix: patchMethod.Method, patchId: patchId);
            else if (patchType == PatchType.Postfix)
                Machine.Patch(methodsToPatch[time], postfix: patchMethod.Method, patchId: patchId);
            else
                Birdsong.Tweet("Trying to schedule method {0} at Time of Power '{1}' with patch type '{2}' - which is not a valid PatchType (not a prefix, not a postfix). No fooling around with the Times of Power, please.");
        }

        internal static IEnumerable<CodeInstruction> TranspilerReplaceMethodCall(IEnumerable<CodeInstruction> instructions, MethodInfo methodToReplace, List<CodeInstruction> myCode, int skipCallsCount)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int argsCount = methodToReplace.GetParameters().Length + (methodToReplace.IsStatic ? 0 : 1);
            int currentMethodCall = 0;

            for (int i = 0; i < codes.Count; i++)
                if (codes[i].Calls(methodToReplace))
                {
                    currentMethodCall++;
                    if (currentMethodCall <= skipCallsCount)
                        continue;

                    i -= argsCount;
                    for (var n = 0; n <= argsCount; n++)//additional deletion for the method call itself
                        codes.RemoveAt(i);
                    if (codes[i].opcode == OpCodes.Pop)
                        codes.RemoveAt(i);

                    codes.InsertRange(i, myCode);

                    break;
                }

            return codes.AsEnumerable();
        }

        internal static IEnumerable<CodeInstruction> TranspilerInsertAtMethod(IEnumerable<CodeInstruction> instructions, MethodInfo insertCodeNearThisMethodCall, List<CodeInstruction> myCode, bool insertBefore, int skipCallsCount)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int lineShift;
            if (insertBefore)
                lineShift = -(insertCodeNearThisMethodCall.GetParameters().Length + (insertCodeNearThisMethodCall.IsStatic ? 0 : 1));
            else
                lineShift = 1;

            int currentMethodCall = 0;
            for (int i = 0; i < codes.Count; i++)
                if (codes[i].Calls(insertCodeNearThisMethodCall))
                {
                    currentMethodCall++;
                    if (currentMethodCall <= skipCallsCount)
                        continue;

                    i += lineShift;
                    if (insertBefore == false && codes[i].opcode == OpCodes.Pop)
                        i++;

                    codes.InsertRange(i, myCode);
                    break;
                }

            return codes.AsEnumerable();
        }

        internal static IEnumerable<CodeInstruction> TranspilerReplaceAllAfterMask(IEnumerable<CodeInstruction> instructions, CodeInstructionMask checkMask, List<CodeInstruction> myCode, bool inclusive, int skipOccurences)
        {
            if (myCode == null || myCode.Count == 0)
                return instructions;

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            List<CodeInstruction> finalCodes = new List<CodeInstruction>();

            int currentOccurence = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (checkMask(codes[i]))
                {
                    currentOccurence++;
                    if (currentOccurence <= skipOccurences)
                        continue;
                    else
                    {
                        if (inclusive)
                            finalCodes.Add(codes[i]);

                        break;
                    }
                }

                finalCodes.Add(codes[i]);
            }

            finalCodes.AddRange(myCode);
            finalCodes.Add(new CodeInstruction(OpCodes.Ret));

            return finalCodes.AsEnumerable();
        }

        internal static IEnumerable<CodeInstruction> TranspilerReplaceAllBeforeMask(IEnumerable<CodeInstruction> instructions, CodeInstructionMask checkMask, List<CodeInstruction> myCode, bool inclusive, int skipOccurences)
        {
            if (myCode == null || myCode.Count == 0)
                return instructions;

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            List<CodeInstruction> finalCodes = myCode;

            int currentOccurence = 0;
            while (codes.Count > 0)
            {
                if (checkMask(codes[0]))
                {
                    currentOccurence++;
                    if (currentOccurence <= skipOccurences)
                        continue;
                    else
                    {
                        if (!inclusive)
                            codes.RemoveAt(0);

                        break;
                    }
                }

                codes.RemoveAt(0);
            }

            finalCodes.AddRange(codes);
            return finalCodes.AsEnumerable();
        }

        internal static IEnumerable<CodeInstruction> TranspilerInsertAtMask(IEnumerable<CodeInstruction> instructions, CodeInstructionMask checkMask, List<CodeInstruction> myCode, int occurence, bool insertBefore)
        {
            if (myCode == null || myCode.Count == 0)
            {
                Birdsong.Tweet("Trying to transpile with an empty myCode!");
                return instructions;
            }

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

            int currentOccurence = 0;
            int i = 0;
            while (i < codes.Count)
            {
                if (checkMask(codes[i]))
                {
                    currentOccurence++;
                    if (currentOccurence <= occurence)
                        continue;
                    else
                    {
                        codes.InsertRange(insertBefore ? i : i + 1, myCode);

                        break;
                    }
                }

                i++;
            }

            return codes.AsEnumerable();
        }

        internal static IEnumerable<CodeInstruction> TranspilerReplaceSegment(IEnumerable<CodeInstruction> instructions, CodeInstructionMask startSegmentMask, CodeInstructionMask endSegmentMask, List<CodeInstruction> myCode)
        {
            if (myCode == null || myCode.Count == 0)
            {
                Birdsong.Tweet("Trying to transpile with an empty myCode!");
                return instructions;
            }

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (startSegmentMask(codes[i]))
                {
                    while (endSegmentMask(codes[i]) == false)
                        codes.RemoveAt(i);

                    codes.InsertRange(i, myCode);
                    break;
                }
            }

            return codes.AsEnumerable();
        }

        internal static void LogILCodes(IEnumerable<CodeInstruction> instructions)
        {
            Birdsong.Tweet("IL CODE:");
            foreach (CodeInstruction instruction in instructions)
                Birdsong.Tweet(instruction.opcode, instruction.operand == null ? string.Empty : instruction.operand, instruction.labels.UnpackAsString());
        }
    }
}

namespace Roost
{
    //get members methods
    public static partial class Machine
    {
        public static MethodInfo GetMethodInvariant(this Type definingClass, string methodName)
        {
            return Vagabond.HarmonyMask.GetMethodInvariant(definingClass, methodName);
        }

        public static MethodInfo GetMethodInvariant(this Type definingClass, string methodName, params Type[] args)
        {
            return Vagabond.HarmonyMask.GetMethodInvariant(definingClass, methodName, args);
        }

        public static FieldInfo GetFieldInvariant(this Type definingClass, string fieldName)
        {
            return Vagabond.HarmonyMask.GetFieldInvariant(definingClass, fieldName);
        }

        public static PropertyInfo GetPropertyInvariant(this Type definingClass, string propertyName)
        {
            return Vagabond.HarmonyMask.GetPropertyInvariant(definingClass, propertyName);
        }
    }

    //patching methods
    public static partial class Machine
    {
        private const string DEFAULT_PATCH_ID = "theroostmachine";
        public static void Patch(MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null, string patchId = DEFAULT_PATCH_ID)
        {
            Vagabond.HarmonyMask.Patch(original, prefix, postfix, transpiler, finalizer, patchId);
        }

        public static IEnumerable<CodeInstruction> ReplaceMethodCall(this IEnumerable<CodeInstruction> original, MethodInfo methodToReplace, List<CodeInstruction> myCode, int methodCallNumber = 0)
        {
            return Vagabond.HarmonyMask.TranspilerReplaceMethodCall(original, methodToReplace, myCode, methodCallNumber);
        }

        public static IEnumerable<CodeInstruction> InsertBeforeMethodCall(this IEnumerable<CodeInstruction> original, MethodInfo nearMethodCall, List<CodeInstruction> myCode, int methodCallNumber = 0)
        {
            return Vagabond.HarmonyMask.TranspilerInsertAtMethod(original, nearMethodCall, myCode, true, methodCallNumber);
        }

        public static IEnumerable<CodeInstruction> InsertAfterMethodCall(this IEnumerable<CodeInstruction> original, MethodInfo nearMethodCall, List<CodeInstruction> myCode, int methodCallNumber = 0)
        {
            return Vagabond.HarmonyMask.TranspilerInsertAtMethod(original, nearMethodCall, myCode, false, methodCallNumber);
        }

        public static IEnumerable<CodeInstruction> ReplaceAfterMask(this IEnumerable<CodeInstruction> original, Vagabond.CodeInstructionMask codePointMask, List<CodeInstruction> myCode, bool inclusive, int occurencesNumber = 0)
        {
            return Vagabond.HarmonyMask.TranspilerReplaceAllAfterMask(original, codePointMask, myCode, inclusive, occurencesNumber);
        }

        public static IEnumerable<CodeInstruction> ReplaceBeforeMask(this IEnumerable<CodeInstruction> original, Vagabond.CodeInstructionMask codePointMask, List<CodeInstruction> myCode, bool inclusive, int occurencesNumber = 0)
        {
            return Vagabond.HarmonyMask.TranspilerReplaceAllBeforeMask(original, codePointMask, myCode, inclusive, occurencesNumber);
        }

        public static IEnumerable<CodeInstruction> InsertAtMask(this IEnumerable<CodeInstruction> instructions, Vagabond.CodeInstructionMask checkMask, List<CodeInstruction> myCode, bool insertBefore = true, int occurence = 0)
        {
            return Vagabond.HarmonyMask.TranspilerInsertAtMask(instructions, checkMask, myCode, occurence, insertBefore);
        }

        public static IEnumerable<CodeInstruction> ReplaceSegment(this IEnumerable<CodeInstruction> instructions, Vagabond.CodeInstructionMask startSegmentMask, Vagabond.CodeInstructionMask endSegmentMask, List<CodeInstruction> myCode)
        {
            return Vagabond.HarmonyMask.TranspilerReplaceSegment(instructions, startSegmentMask, endSegmentMask, myCode);
        }

        public static void LogILCodes(this IEnumerable<CodeInstruction> codes)
        {
            Vagabond.HarmonyMask.LogILCodes(codes);
        }
    }

    public enum PatchType { Postfix, Prefix }
    public enum AtTimeOfPower
    {

        QuoteSceneInit, MenuSceneInit, NewGame, TabletopSceneInit,
        RecipeRequirementsCheck, RecipeExecution,
        RecipePortals, RecipeVFX,
        OnPostImportCulture, OnPostImportDeck, OnPostImportElement, OnPostImportEnding, OnPostImportExpulsion, OnPostImportLegacy, OnPostImportLink,
        OnPostImportXTrigger, OnPostImportMutation, OnPostImportPortal, OnPostImportRecipe, OnPostImpostSetting, OnPostImportSlot, OnPostImportVerb
    }

    //times of power scheduling methods
    public static partial class Machine
    {
        public static void Schedule(this AtTimeOfPower time, Delegate action, PatchType patchType, string patchId = DEFAULT_PATCH_ID)
        {
            Vagabond.HarmonyMask.Unite(time, action, patchType, patchId);
        }

        public static void Schedule(this AtTimeOfPower time, Action action, PatchType patchType, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1>(this AtTimeOfPower time, Action<T1> action, PatchType patchType, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2>(this AtTimeOfPower time, Action<T1, T2> action, PatchType patchType, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2, T3>(this AtTimeOfPower time, Action<T1, T2, T3> action, PatchType patchType, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2, T3, T4>(this AtTimeOfPower time, Action<T1, T2, T3, T4> action, PatchType patchType, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2, T3, T4, T5>(this AtTimeOfPower time, Action<T1, T2, T3, T4, T5> action, PatchType patchType, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        //six variables is a reasonable maximum
        public static void Schedule<T1, T2, T3, T4, T5, T6>(this AtTimeOfPower time, Action<T1, T2, T3, T4, T5, T6> action, PatchType patchType, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule(this AtTimeOfPower time, Func<bool> func, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1>(this AtTimeOfPower time, Func<T1, bool> func, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2>(this AtTimeOfPower time, Func<T1, T2, bool> func, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2, T3>(this AtTimeOfPower time, Func<T1, T2, T3, bool> func, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2, T3, T4>(this AtTimeOfPower time, Func<T1, T2, T3, T4, bool> func, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2, T3, T4, T5>(this AtTimeOfPower time, Func<T1, T2, T3, T4, T5, bool> func, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        //six variables is a reasonable maximum 2
        public static void Schedule<T1, T2, T3, T4, T5, T6>(this AtTimeOfPower time, Func<T1, T2, T3, T4, T5, T6, bool> func, string patchId = DEFAULT_PATCH_ID)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }
    }
}