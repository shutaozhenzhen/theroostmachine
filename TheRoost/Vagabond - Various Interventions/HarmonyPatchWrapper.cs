using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

using SecretHistories.UI;
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
                throw Birdsong.Cack($"Failed to find method '{name}' with parameters '{args.LogCollection()}' in '{definingClass.Name}', reason: {ex.FormatException()}");
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
 { AtTimeOfPower.GameOverSceneInit, typeof(GameOverScreenController).GetMethodInvariant("OnEnable") },
 { AtTimeOfPower.NewGameSceneInit, typeof(NewGameScreenController).GetMethodInvariant("Start") },

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
 { AtTimeOfPower.CompendiumLoad, typeof(CompendiumLoader).GetMethodInvariant("PopulateCompendium") }
        };

        internal static void Unite(AtTimeOfPower time, Delegate patchMethod, PatchType patchType, string patchId)
        {
            if (patchType == PatchType.Prefix)
                Machine.Patch(methodsToPatch[time], prefix: patchMethod.Method, patchId: patchId);
            else if (patchType == PatchType.Postfix)
                Machine.Patch(methodsToPatch[time], postfix: patchMethod.Method, patchId: patchId);
            else
                Birdsong.Tweet($"Trying to schedule method {patchMethod.Method.Name} at Time of Power '{time}' with patch type '{patchType}' - which is not a valid PatchType (not a prefix, not a postfix). No fooling around with the Times of Power, please.");
        }

        internal static IEnumerable<CodeInstruction> TranspilerInsertAtMethod(IEnumerable<CodeInstruction> instructions, MethodInfo referenceMethodCall, List<CodeInstruction> myCode, bool deleteOriginalMethod, int skipCallsCount)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            //will only work for simple parameters, not other method calls
            int lineShift = -(referenceMethodCall.GetParameters().Length + (referenceMethodCall.IsStatic ? 0 : 1));

            int currentMethodCall = 0;
            for (int i = 0; i < codes.Count; i++)
                if (codes[i].Calls(referenceMethodCall))
                {
                    currentMethodCall++;
                    if (currentMethodCall <= skipCallsCount)
                        continue;

                    i += lineShift;

                    if (deleteOriginalMethod)
                    {
                        while (!codes[i].Calls(referenceMethodCall))
                            codes.RemoveAt(i);
                        codes.RemoveAt(i);
                    }

                    codes.InsertRange(i, myCode);
                    break;
                }

            return codes.AsEnumerable();
        }

        internal static IEnumerable<CodeInstruction> TranspilerReplaceSegment(IEnumerable<CodeInstruction> instructions, CodeInstructionMask startSegmentMask, CodeInstructionMask endSegmentMask, List<CodeInstruction> myCode, bool removeStart, bool removeEnd, int startShift)
        {
            if (myCode == null || myCode.Count == 0)
            {
                Birdsong.Tweet(VerbosityLevel.Essential, 1, "Trying to transpile with an empty myCode!");
                return instructions;
            }

            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (startSegmentMask(codes[i]))
                {
                    i += startShift;

                    if (removeStart)
                        codes.RemoveAt(i);
                    else
                        i++;

                    while (i < (codes.Count - 1) && endSegmentMask(codes[i]) == false)
                        codes.RemoveAt(i);

                    if (removeEnd)
                        codes.RemoveAt(i);

                    codes.InsertRange(i, myCode);
                    return codes.AsEnumerable();
                }
            }

            Birdsong.Tweet(VerbosityLevel.Essential, 1, "Incorrect mask in transpiler!");
            return null;
        }

        internal static void LogILCodes(IEnumerable<CodeInstruction> instructions)
        {
            Birdsong.Tweet("IL CODE:");
            foreach (CodeInstruction instruction in instructions)
                Birdsong.Tweet($"{instruction.opcode} {(instruction.operand == null ? string.Empty : $": {instruction.operand} ({instruction.operand.GetType().Name})")} {instruction.labels.LogCollection()}");
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

        public static MethodInfo GetMethod<T>(string methodName, params Type[] arguments)
        {
            if (arguments == null)
                return typeof(T).GetMethodInvariant(methodName);
            else
                return typeof(T).GetMethodInvariant(methodName, arguments);
        }
    }

    //patching methods
    public static partial class Machine
    {
        private const string DEFAULT_PATCH_ID = "theroostmachine";

        public static void Patch<T>(string methodName, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null, string patchId = DEFAULT_PATCH_ID)
        {
            MethodInfo original = typeof(T).GetMethodInvariant(methodName);
            Vagabond.HarmonyMask.Patch(original, prefix, postfix, transpiler, finalizer, patchId);
        }

        public static void Patch(MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null, string patchId = DEFAULT_PATCH_ID)
        {
            Vagabond.HarmonyMask.Patch(original, prefix, postfix, transpiler, finalizer, patchId);
        }

        public static IEnumerable<CodeInstruction> InsertBeforeMethodCall(this IEnumerable<CodeInstruction> original, MethodInfo referenceMethod, List<CodeInstruction> myCode, int methodCallNumber = 0)
        {
            return Vagabond.HarmonyMask.TranspilerInsertAtMethod(original, referenceMethod, myCode, false, methodCallNumber);
        }

        public static IEnumerable<CodeInstruction> ReplaceMethodCall(this IEnumerable<CodeInstruction> original, MethodInfo referenceMethod, List<CodeInstruction> myCode, int methodCallNumber = 0)
        {
            return Vagabond.HarmonyMask.TranspilerInsertAtMethod(original, referenceMethod, myCode, true, methodCallNumber);
        }

        public static IEnumerable<CodeInstruction> InsertBefore(this IEnumerable<CodeInstruction> original, Vagabond.CodeInstructionMask mask, List<CodeInstruction> myCode, int startShift = 0)
        {
            return Vagabond.HarmonyMask.TranspilerReplaceSegment(original, mask, code => true, myCode, false, false, startShift - 1);
        }

        public static IEnumerable<CodeInstruction> InsertAfter(this IEnumerable<CodeInstruction> original, Vagabond.CodeInstructionMask mask, List<CodeInstruction> myCode, int startShift = 0)
        {
            return Vagabond.HarmonyMask.TranspilerReplaceSegment(original, mask, code => true, myCode, false, false, startShift);
        }

        public static IEnumerable<CodeInstruction> ReplaceAfterMask(this IEnumerable<CodeInstruction> original, Vagabond.CodeInstructionMask mask, List<CodeInstruction> myCode, bool removeStart, int startShift = 0)
        {
            return Vagabond.HarmonyMask.TranspilerReplaceSegment(original, mask, code => false, myCode, removeStart, true, startShift);
        }

        public static IEnumerable<CodeInstruction> ReplaceBeforeMask(this IEnumerable<CodeInstruction> original, Vagabond.CodeInstructionMask mask, List<CodeInstruction> myCode, bool removeMask, int startShift = 0)
        {
            return Vagabond.HarmonyMask.TranspilerReplaceSegment(original, code => true, mask, myCode, true, removeMask, startShift);
        }

        public static IEnumerable<CodeInstruction> ReplaceInstruction(this IEnumerable<CodeInstruction> original, Vagabond.CodeInstructionMask mask, List<CodeInstruction> myCode, int startShift = 0)
        {
            return Vagabond.HarmonyMask.TranspilerReplaceSegment(original, mask, code => true, myCode, true, false, startShift);
        }

        public static IEnumerable<CodeInstruction> ReplaceSegment(this IEnumerable<CodeInstruction> instructions, Vagabond.CodeInstructionMask startSegmentMask, Vagabond.CodeInstructionMask endSegmentMask, List<CodeInstruction> myCode, bool replaceStart, bool replaceEnd, int startShift = 0)
        {
            return Vagabond.HarmonyMask.TranspilerReplaceSegment(instructions, startSegmentMask, endSegmentMask, myCode, replaceStart, replaceEnd, startShift);
        }

        public static void LogILCodes(this IEnumerable<CodeInstruction> codes)
        {
            Vagabond.HarmonyMask.LogILCodes(codes);
        }
    }

    public enum PatchType { Postfix, Prefix }
    public enum AtTimeOfPower
    {

        QuoteSceneInit, MenuSceneInit, NewGame, TabletopSceneInit, GameOverSceneInit, NewGameSceneInit,
        RecipeRequirementsCheck, RecipeExecution,
        RecipePortals, RecipeVFX,
        OnPostImportCulture, OnPostImportDeck, OnPostImportElement, OnPostImportEnding, OnPostImportExpulsion, OnPostImportLegacy, OnPostImportLink,
        OnPostImportXTrigger, OnPostImportMutation, OnPostImportPortal, OnPostImportRecipe, OnPostImpostSetting, OnPostImportSlot, OnPostImportVerb,
        CompendiumLoad
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