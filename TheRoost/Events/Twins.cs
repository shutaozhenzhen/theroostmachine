using System;
using System.Collections.Generic;
using System.Reflection;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Infrastructure;

namespace TheRoost.Twins
{
    internal static class EventManager
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

        internal static void Enact()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            //we want all mods to finish the scheduling before we patch so we apply patches a frame after everything is loaded
            Rooster.Schedule(new Action(ApplyEventPatches), new UnityEngine.WaitForEndOfFrame());
        }

        private static void ApplyEventPatches()
        {
            foreach (AtTimeOfPower time in AllTimesOfPower)
            {
                ///Birdsong.Sing(time);
                foreach (Delegate patchGroup in prefixes[time])
                    foreach (Delegate patchMethod in patchGroup.GetInvocationList())
                        TheRoostMachine.Patch(methodsToPatch[time], prefix: patchMethod.Method);

                foreach (Delegate patchGroup in postfixes[time])
                    foreach (Delegate patchMethod in patchGroup.GetInvocationList())
                        TheRoostMachine.Patch(methodsToPatch[time], postfix: patchMethod.Method);
            }
        }

        public static void Schedule(AtTimeOfPower time, Delegate action, PatchType patchType)
        {
            if (patchType == PatchType.Prefix)
                prefixes[time].Add(action);
            else
                postfixes[time].Add(action);
        }

        private static PatchAtTimeCollection prefixes = new PatchAtTimeCollection();
        private static PatchAtTimeCollection postfixes = new PatchAtTimeCollection();
        class PatchAtTimeCollection : Dictionary<AtTimeOfPower, List<Delegate>>
        {
            public PatchAtTimeCollection()
                : base()
            {
                foreach (AtTimeOfPower t in EventManager.AllTimesOfPower)
                    this[t] = new List<Delegate>();
            }
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
        public static void Schedule(this AtTimeOfPower time, Delegate action, PatchType patchType)
        {
            TheRoost.Twins.EventManager.Schedule(time, action, patchType);
        }

        public static void Schedule(this AtTimeOfPower time, Action action, PatchType patchType)
        {
            time.Schedule(action as Delegate, patchType);
        }

        public static void Schedule<T1>(this AtTimeOfPower time, Action<T1> action, PatchType patchType)
        {
            time.Schedule(action as Delegate, patchType);
        }

        public static void Schedule<T1, T2>(this AtTimeOfPower time, Action<T1, T2> action, PatchType patchType)
        {
            time.Schedule(action as Delegate, patchType);
        }

        public static void Schedule<T1, T2, T3>(this AtTimeOfPower time, Action<T1, T2, T3> action, PatchType patchType)
        {
            time.Schedule(action as Delegate, patchType);
        }

        public static void Schedule<T1, T2, T3, T4>(this AtTimeOfPower time, Action<T1, T2, T3, T4> action, PatchType patchType)
        {
            time.Schedule(action as Delegate, patchType);
        }

        public static void Schedule<T1, T2, T3, T4, T5>(this AtTimeOfPower time, Action<T1, T2, T3, T4, T5> action, PatchType patchType)
        {
            time.Schedule(action as Delegate, patchType);
        }

        //six variables is a reasonable maximum
        public static void Schedule<T1, T2, T3, T4, T5, T6>(this AtTimeOfPower time, Action<T1, T2, T3, T4, T5, T6> action, PatchType patchType)
        {
            time.Schedule(action as Delegate, patchType);
        }

        public static void ScheduleBreak(this AtTimeOfPower time, Func<bool> func, PatchType patchType)
        {
            time.Schedule(func as Delegate, patchType);
        }

        public static void ScheduleBreak<T1>(this AtTimeOfPower time, Func<T1, bool> func, PatchType patchType)
        {
            time.Schedule(func as Delegate, patchType);
        }

        public static void ScheduleBreak<T1, T2>(this AtTimeOfPower time, Func<T1, T2, bool> func, PatchType patchType)
        {
            time.Schedule(func as Delegate, patchType);
        }

        public static void ScheduleBreak<T1, T2, T3>(this AtTimeOfPower time, Func<T1, T2, T3, bool> func, PatchType patchType)
        {
            time.Schedule(func as Delegate, patchType);
        }

        public static void ScheduleBreak<T1, T2, T3, T4>(this AtTimeOfPower time, Func<T1, T2, T3, T4, bool> func, PatchType patchType)
        {
            time.Schedule(func as Delegate, patchType);
        }

        public static void ScheduleBreak<T1, T2, T3, T4, T5>(this AtTimeOfPower time, Func<T1, T2, T3, T4, T5, bool> func, PatchType patchType)
        {
            time.Schedule(func as Delegate, patchType);
        }

        public static void ScheduleBreak<T1, T2, T3, T4, T5, T6>(this AtTimeOfPower time, Func<T1, T2, T3, T4, T5, T6, bool> func, PatchType patchType)
        {
            time.Schedule(func as Delegate, patchType);
        }
    }
}