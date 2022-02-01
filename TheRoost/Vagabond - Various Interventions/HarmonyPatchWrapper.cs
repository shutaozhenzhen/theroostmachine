using System;
using System.Reflection;
using System.Collections.Generic;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Infrastructure;

using HarmonyLib;

namespace TheRoost.Vagabond
{
    internal static class HarmonyMask
    {
        private static readonly Dictionary<string, Harmony> patchers = new Dictionary<string, Harmony>();


        public static void Patch(MethodBase original, MethodInfo prefix, MethodInfo postfix, MethodInfo transpiler, MethodInfo finalizer, string patchId)
        {
            if (original == null)
            {
                Birdsong.Sing("Trying to patch null method!");
                return;
            }
            if (prefix == null && postfix == null && transpiler == null && finalizer == null)
            {
                Birdsong.Sing("All patches for {0}() are null!", original.Name);
                return;
            }

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
                Birdsong.Sing("Harmony patch '{0}' isn't present in the Roost Machine");
            else if (Harmony.HasAnyPatches(patchId))
                patchers[patchId].UnpatchAll(patchId);
        }

        public static bool HasAnyPatches(string patchId)
        {
            return Harmony.HasAnyPatches(patchId);
        }

        static List<BindingFlags> bindingFlagsPriority = new List<BindingFlags> { 
            (BindingFlags.Instance | BindingFlags.Public), 
            (BindingFlags.Instance | BindingFlags.NonPublic),
            (BindingFlags.Static | BindingFlags.Public),
            (BindingFlags.Static | BindingFlags.NonPublic),
        };

        public static MethodInfo GetMethodInvariant(this Type definingClass, string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName))
                Birdsong.Sing("Trying to find whitespace method for class {0} (don't!)", definingClass.Name);

            MethodInfo method;
            foreach (BindingFlags flag in bindingFlagsPriority)
            {
                method = definingClass.GetMethod(methodName, flag);
                if (method != null)
                    return method;
            }

            Birdsong.Sing("Method {0} not found in class {1}", methodName, definingClass.Name);
            return null;
        }

        public static FieldInfo GetFieldInvariant(this Type definingClass, string fieldName)
        {
            FieldInfo field;
            foreach (BindingFlags flag in bindingFlagsPriority)
            {
                field = definingClass.GetField(fieldName, flag);
                if (field != null)
                    return field;
            }

            Birdsong.Sing("Field {0} not found in class {1}", fieldName, definingClass.Name);
            return null;
        }

        public static PropertyInfo GetPropertyInvariant(this Type definingClass, string propertyName)
        {
            PropertyInfo property;
            foreach (BindingFlags flag in bindingFlagsPriority)
            {
                property = definingClass.GetProperty(propertyName, flag);
                if (property != null)
                    return property;
            }

            Birdsong.Sing("Property {0} not found in class {1}", propertyName, definingClass.Name);
            return null;
        }

        private static readonly Dictionary<AtTimeOfPower, MethodBase> methodsToPatch = new Dictionary<AtTimeOfPower, MethodBase>()
        {
 { AtTimeOfPower.MainMenuLoaded, typeof(MenuScreenController).GetMethodInvariant("InitialiseServices") },
 { AtTimeOfPower.NewGameStarted, typeof(MenuScreenController).GetMethodInvariant("BeginNewSaveWithSpecifiedLegacy") },
 { AtTimeOfPower.TabletopLoaded, typeof(GameGateway).GetMethodInvariant("Start") },

 { AtTimeOfPower.RecipeRequirementsCheck, typeof(Recipe).GetMethodInvariant("RequirementsSatisfiedBy") },

 { AtTimeOfPower.RecipeExecution, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("Execute") },
 { AtTimeOfPower.RecipeMutations, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("RunMutationEffects") },
 { AtTimeOfPower.RecipeXtriggers, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("RunXTriggers") },
 { AtTimeOfPower.RecipeDeckEffects, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("RunDeckEffect") },
 { AtTimeOfPower.RecipeEffects, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("RunRecipeEffects") },
 { AtTimeOfPower.RecipeVerbManipulations, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("RunVerbManipulations") },
 { AtTimeOfPower.RecipePurges, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("RunElementPurges") },
 { AtTimeOfPower.RecipePortals, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("OpenPortals") },
 { AtTimeOfPower.RecipeVfx, typeof(RecipeCompletionEffectCommand).GetMethodInvariant("DoRecipeVfx") },
        };

        internal static void Unite(AtTimeOfPower time, Delegate patchMethod, PatchType patchType, string patchId)
        {
            if (patchType == PatchType.Prefix)
                Machine.Patch(methodsToPatch[time], prefix: patchMethod.Method, patchId: patchId);
            else if (patchType == PatchType.Postfix)
                Machine.Patch(methodsToPatch[time], postfix: patchMethod.Method, patchId: patchId);
            else
                Birdsong.Sing("Trying to schedule method {0} at Time of Power '{1}' with patch type '{2}' - which is not a valid PatchType (not a prefix, not a postfix). No fooling around with the Times of Power, please.");
        }
    }
}

namespace TheRoost
{
    public enum PatchType { Postfix, Prefix }
    public enum AtTimeOfPower
    {
        MainMenuLoaded, NewGameStarted, TabletopLoaded,
        RecipeRequirementsCheck, RecipeExecution,
        RecipeMutations, RecipeXtriggers, RecipeDeckEffects, RecipeEffects, RecipeVerbManipulations, RecipePurges, RecipePortals, RecipeVfx,
    }

    public static partial class Machine
    {
        private const string defaultPatchId = "theroostmachine";
        public static void Patch(MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null, string patchId = defaultPatchId)
        {
            Vagabond.HarmonyMask.Patch(original, prefix, postfix, transpiler, finalizer, patchId);
        }

        public static MethodInfo GetMethodInvariant(this Type definingClass, string methodName)
        {
            return Vagabond.HarmonyMask.GetMethodInvariant(definingClass, methodName);
        }

        public static FieldInfo GetFieldInvariant(this Type definingClass, string fieldName)
        {
            return Vagabond.HarmonyMask.GetFieldInvariant(definingClass, fieldName);
        }

        public static PropertyInfo GetPropertyInvariant(this Type definingClass, string propertyName)
        {
            return Vagabond.HarmonyMask.GetPropertyInvariant(definingClass, propertyName);
        }

        public static void Schedule(this AtTimeOfPower time, Delegate action, PatchType patchType, string patchId = defaultPatchId)
        {
            Vagabond.HarmonyMask.Unite(time, action, patchType, patchId);
        }

        public static void Schedule(this AtTimeOfPower time, Action action, PatchType patchType, string patchId = defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1>(this AtTimeOfPower time, Action<T1> action, PatchType patchType, string patchId = defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2>(this AtTimeOfPower time, Action<T1, T2> action, PatchType patchType, string patchId = defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2, T3>(this AtTimeOfPower time, Action<T1, T2, T3> action, PatchType patchType, string patchId = defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2, T3, T4>(this AtTimeOfPower time, Action<T1, T2, T3, T4> action, PatchType patchType, string patchId = defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2, T3, T4, T5>(this AtTimeOfPower time, Action<T1, T2, T3, T4, T5> action, PatchType patchType, string patchId = defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        //six variables is a reasonable maximum
        public static void Schedule<T1, T2, T3, T4, T5, T6>(this AtTimeOfPower time, Action<T1, T2, T3, T4, T5, T6> action, PatchType patchType, string patchId = defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule(this AtTimeOfPower time, Func<bool> func, string patchId = defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1>(this AtTimeOfPower time, Func<T1, bool> func, string patchId = defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2>(this AtTimeOfPower time, Func<T1, T2, bool> func)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2, T3>(this AtTimeOfPower time, Func<T1, T2, T3, bool> func, string patchId = defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2, T3, T4>(this AtTimeOfPower time, Func<T1, T2, T3, T4, bool> func, string patchId = defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2, T3, T4, T5>(this AtTimeOfPower time, Func<T1, T2, T3, T4, T5, bool> func, string patchId = defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        //six variables is a reasonable maximum 2
        public static void Schedule<T1, T2, T3, T4, T5, T6>(this AtTimeOfPower time, Func<T1, T2, T3, T4, T5, T6, bool> func, string patchId = defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }
    }
}