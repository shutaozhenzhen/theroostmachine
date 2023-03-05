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
                Birdsong.TweetLoud($"Harmony patch '{patchId}' isn't present in the Roost Machine");
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
                Birdsong.TweetLoud($"Trying to find whitespace method for class {definingClass.Name} (don't!)");

            try
            {
                MethodInfo method = definingClass.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

                if (method == null)
                    throw Birdsong.Cack("Method not found");

                return method;
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"Failed to find method '{name}'  in '{definingClass.Name}', reason: {ex.FormatException()}");
            }
        }

        internal static MethodInfo GetMethodInvariant(Type definingClass, string name, params Type[] argTypes)
        {
            try
            {
                MethodInfo simplyFoundMethod = definingClass.GetMethod(name, argTypes);
                if (simplyFoundMethod != null)
                    return simplyFoundMethod;

                var allMethods = definingClass.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

                foreach (MethodInfo method in allMethods)
                    if (method.Name == name)
                    {
                        bool success = true;

                        ParameterInfo[] methodParameters = method.GetParameters();
                        for (int n = 0; n < methodParameters.Length; n++)
                            if (methodParameters[n].ParameterType != argTypes[n])
                            {
                                success = false;
                                break;
                            }

                        if (success)
                            return method;
                    }

                throw Birdsong.Cack("Method not found");
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"Failed to find method '{name}' with parameters '{argTypes.UnpackCollection()}' in '{definingClass.Name}', reason: {ex.FormatException()}");
            }
        }

        internal static FieldInfo GetFieldInvariant(this Type definingClass, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                Birdsong.TweetLoud($"Trying to find whitespace field for class {definingClass.Name} (don't!)");

            FieldInfo field = definingClass.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null)
                Birdsong.TweetLoud($"Field {name} is not found in class {definingClass.Name}");

            return field;
        }

        internal static PropertyInfo GetPropertyInvariant(this Type definingClass, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                Birdsong.TweetLoud($"Trying to find whitespace property for class {definingClass.Name} (don't!)");

            PropertyInfo property = definingClass.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

            if (property == null)
                Birdsong.TweetLoud($"Property {name} not found in class {definingClass.Name}");

            return property;
        }


        public static MethodBase GetMethod(this AtTimeOfPower time)
        {
            switch (time)
            {
                case AtTimeOfPower.QuoteSceneInit: return typeof(SplashScreen).GetMethodInvariant("Start");
                case AtTimeOfPower.MenuSceneInit: return typeof(MenuScreenController).GetMethodInvariant("InitialiseServices");
                case AtTimeOfPower.TabletopSceneInit: return typeof(GameGateway).GetMethodInvariant("PopulateEnvironment");
                case AtTimeOfPower.GameOverSceneInit: return typeof(GameOverScreenController).GetMethodInvariant("OnEnable");
                case AtTimeOfPower.NewGameSceneInit: return typeof(NewGameScreenController).GetMethodInvariant("Start");

                case AtTimeOfPower.NewGame: return typeof(MenuScreenController).GetMethodInvariant(nameof(MenuScreenController.BeginNewSaveWithSpecifiedLegacy));

                case AtTimeOfPower.RecipeRequirementsCheck: return typeof(Recipe).GetMethodInvariant(nameof(Recipe.RequirementsSatisfiedBy));

                case AtTimeOfPower.RecipeExecution: return typeof(RecipeCompletionEffectCommand).GetMethodInvariant("Execute", typeof(Situation));
                case AtTimeOfPower.RecipePortals: return typeof(RecipeCompletionEffectCommand).GetMethodInvariant("OpenPortals");

                case AtTimeOfPower.OnPostImportCulture: return typeof(Culture).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportDeck: return typeof(DeckSpec).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportElement: return typeof(Element).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportEnding: return typeof(Ending).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportExpulsion: return typeof(Expulsion).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportLegacy: return typeof(Legacy).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportLink: return typeof(LinkedRecipeDetails).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportXTrigger: return typeof(MorphDetails).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportMutation: return typeof(MutationEffect).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportPortal: return typeof(Portal).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportRecipe: return typeof(Recipe).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImpostSetting: return typeof(Setting).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportSlot: return typeof(SphereSpec).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.OnPostImportVerb: return typeof(Verb).GetMethodInvariant("OnPostImportForSpecificEntity");
                case AtTimeOfPower.CompendiumLoad: return typeof(CompendiumLoader).GetMethodInvariant("PopulateCompendium");
                default:
                    Birdsong.TweetLoud($"Corresponding method for time of power {time} isn't set; returning null");
                    return null;
            }
        }

        internal static void Unite(AtTimeOfPower time, Delegate patchMethod, PatchType patchType, string patchId)
        {
            if (patchType == PatchType.Prefix)
                Machine.Patch(time.GetMethod(), prefix: patchMethod.Method, patchId: patchId);
            else if (patchType == PatchType.Postfix)
                Machine.Patch(time.GetMethod(), postfix: patchMethod.Method, patchId: patchId);
            else
                Birdsong.TweetLoud($"Trying to schedule method {patchMethod.Method.Name} at Time of Power '{time}' with patch type '{patchType}' - which is not a valid PatchType (not a prefix, not a postfix). No fooling around with the Times of Power, please.");
        }

        internal static IEnumerable<CodeInstruction> TranspilerInsertAtMethod(IEnumerable<CodeInstruction> instructions, MethodInfo referenceMethodCall,
            List<CodeInstruction> myCode,
            bool deleteOriginalMethod, int skipCallsCount)
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

        //used for both replacing and inserting
        internal static IEnumerable<CodeInstruction> TranspilerReplaceSegment(IEnumerable<CodeInstruction> instructions,
            CodeInstructionMask startSegmentMask, CodeInstructionMask endSegmentMask,
            List<CodeInstruction> myCode, bool removeStart, bool removeEnd, int startShift)
        {
            if (myCode == null || myCode.Count == 0)
            {
                Birdsong.TweetLoud("Trying to transpile with an empty myCode!");
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

            Birdsong.TweetLoud("Incorrect mask in transpiler!");
            return null;
        }

        internal static void LogILCodes(IEnumerable<CodeInstruction> instructions)
        {
            Birdsong.TweetLoud("IL CODE:");
            foreach (CodeInstruction instruction in instructions)
                Birdsong.TweetLoud($"{instruction.opcode} {(instruction.operand == null ? string.Empty : $": {instruction.operand} ({instruction.operand.GetType().Name})")} {instruction.labels.UnpackCollection()}");
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
            if (args.Contains(null))
                Birdsong.TweetLoud($"Passed null as a desired parameter in GetMethod() for {definingClass.Name}.{methodName}()");

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
            if (arguments?.Length > 0)
                return typeof(T).GetMethodInvariant(methodName, arguments);
            else
                return typeof(T).GetMethodInvariant(methodName);
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

        public static Action<T1> CreateAction<T1>(this MethodInfo method)
        {
            return method.CreateDelegate(typeof(Action<T1>)) as Action<T1>;
        }

        public static Action<T1, T2> CreateAction<T1, T2>(this MethodInfo method)
        {
            return method.CreateDelegate(typeof(Action<T1, T2>)) as Action<T1, T2>;
        }

        public static Action<T1, T2, T3> CreateAction<T1, T2, T3>(this MethodInfo method)
        {
            return method.CreateDelegate(typeof(Action<T1, T2, T3>)) as Action<T1, T2, T3>;
        }
    }
}