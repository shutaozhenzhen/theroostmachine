using System;
using System.Collections.Generic;
using System.Reflection;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Infrastructure;

namespace TheRoost.Twins
{
    public static class EventManager
    {
        public static Array AllTimesOfPower { get { return Enum.GetValues(typeof(AtTimeOfPower)); } }
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
                TheRoostMachine.Patch(methodsToPatch[time], prefix: patchMethod.Method, patchId: patchId);
            else if (patchType == PatchType.Postfix)
                TheRoostMachine.Patch(methodsToPatch[time], postfix: patchMethod.Method, patchId: patchId);
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

    public static partial class Birdsong
    {
        public static void Schedule(this AtTimeOfPower time, Delegate action, PatchType patchType, string patchId = TheRoostMachine.defaultPatchId)
        {
            TheRoost.Twins.EventManager.Unite(time, action, patchType, patchId);
        }

        public static void Schedule(this AtTimeOfPower time, Action action, PatchType patchType, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1>(this AtTimeOfPower time, Action<T1> action, PatchType patchType, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2>(this AtTimeOfPower time, Action<T1, T2> action, PatchType patchType, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2, T3>(this AtTimeOfPower time, Action<T1, T2, T3> action, PatchType patchType, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2, T3, T4>(this AtTimeOfPower time, Action<T1, T2, T3, T4> action, PatchType patchType, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule<T1, T2, T3, T4, T5>(this AtTimeOfPower time, Action<T1, T2, T3, T4, T5> action, PatchType patchType, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        //six variables is a reasonable maximum
        public static void Schedule<T1, T2, T3, T4, T5, T6>(this AtTimeOfPower time, Action<T1, T2, T3, T4, T5, T6> action, PatchType patchType, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(action as Delegate, patchType, patchId);
        }

        public static void Schedule(this AtTimeOfPower time, Func<bool> func, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1>(this AtTimeOfPower time, Func<T1, bool> func, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2>(this AtTimeOfPower time, Func<T1, T2, bool> func)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2, T3>(this AtTimeOfPower time, Func<T1, T2, T3, bool> func, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2, T3, T4>(this AtTimeOfPower time, Func<T1, T2, T3, T4, bool> func, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        public static void Schedule<T1, T2, T3, T4, T5>(this AtTimeOfPower time, Func<T1, T2, T3, T4, T5, bool> func, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }

        //six variables is a reasonable maximum 2
        public static void Schedule<T1, T2, T3, T4, T5, T6>(this AtTimeOfPower time, Func<T1, T2, T3, T4, T5, T6, bool> func, string patchId = TheRoostMachine.defaultPatchId)
        {
            time.Schedule(func as Delegate, PatchType.Prefix);
        }
    }
}