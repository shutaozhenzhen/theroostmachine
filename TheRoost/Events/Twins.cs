using System;
using System.Collections.Generic;
using System.Reflection;

using SecretHistories.Infrastructure;

namespace TheRoost
{
    [Flags]
    public enum PatchType { Postfix = 0, Prefix = 1 }
    [Flags]
    public enum AtTimeOfPower { MainMenuLoaded = 0, NewGameStarted = 2, TabletopLoaded = 4, }

    public static class Twins
    {
        static Dictionary<int, InjectedAction> timesofpower;
        public delegate void InjectedAction();

        public static Dictionary<AtTimeOfPower, MethodBase> methodsToPatch = new Dictionary<AtTimeOfPower, MethodBase>() { 
        { AtTimeOfPower.MainMenuLoaded, typeof(MenuScreenController).GetMethod("InitialiseServices", BindingFlags.Instance | BindingFlags.NonPublic) },
        { AtTimeOfPower.NewGameStarted, typeof(MenuScreenController).GetMethod("BeginNewSaveWithSpecifiedLegacy", BindingFlags.Instance | BindingFlags.Public) },
        { AtTimeOfPower.TabletopLoaded, typeof(GameGateway).GetMethod("Start", BindingFlags.Instance | BindingFlags.Public) }
    };

        internal static void Unite()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            CheckTimesInit();

            foreach (AtTimeOfPower time in Enum.GetValues(typeof(AtTimeOfPower)))
            {
                string patchMethodName = time.ToString();
                TheRoostMachine.Patch(methodsToPatch[time],
                    prefix: typeof(Twins).GetMethod(patchMethodName + "Prefix", BindingFlags.NonPublic | BindingFlags.Static),
                    postfix: typeof(Twins).GetMethod(patchMethodName + "Postfix", BindingFlags.NonPublic | BindingFlags.Static));
            }
        }

        public static void Schedule(this AtTimeOfPower time, InjectedAction action, PatchType moment = PatchType.Postfix)
        {
            CheckTimesInit();
            int signature = (int)time | (int)moment;
            timesofpower[signature] -= action;
            timesofpower[signature] += action;
        }

        public static void Unschedule(AtTimeOfPower time, InjectedAction action, PatchType moment = PatchType.Postfix)
        {
            CheckTimesInit();
            int signature = (int)time | (int)moment;
            timesofpower[signature] -= action;
        }

        private static void CheckTimesInit()
        {
            if (timesofpower == null)
            {
                timesofpower = new Dictionary<int, InjectedAction>();
                foreach (AtTimeOfPower t in Enum.GetValues(typeof(AtTimeOfPower)))
                    foreach (PatchType m in Enum.GetValues(typeof(PatchType)))
                    {
                        int signature = (int)t | (int)m;
                        timesofpower.Add(signature, null);
                    }
            }
        }

        private static void InvokeTime(AtTimeOfPower time, PatchType moment)
        {
            int signature = (int)time | (int)moment;
            if (timesofpower[signature] != null)
                timesofpower[signature].Invoke();
        }

        static void MainMenuLoadedPrefix() { InvokeTime(AtTimeOfPower.MainMenuLoaded, PatchType.Prefix); }
        static void MainMenuLoadedPostfix() { InvokeTime(AtTimeOfPower.MainMenuLoaded, PatchType.Postfix); }

        static void NewGameStartedPrefix() { InvokeTime(AtTimeOfPower.NewGameStarted, PatchType.Prefix); }
        static void NewGameStartedPostfix() { InvokeTime(AtTimeOfPower.NewGameStarted, PatchType.Postfix); }

        static void TabletopLoadedPrefix() { InvokeTime(AtTimeOfPower.NewGameStarted, PatchType.Prefix); }
        static void TabletopLoadedPostfix() { InvokeTime(AtTimeOfPower.NewGameStarted, PatchType.Postfix); }
    }

    public class TimeSpec<TInstance, TResult>
    {
        public TResult methodResult;
        public TInstance instance;
        public bool continueExecution = true;
        private readonly Dictionary<string, object> variables = new Dictionary<string, object>();

        public TimeSpec(TInstance instanceObject, TResult result)
        {
            this.instance = instanceObject;
            this.methodResult = result;
        }

        public T Unpack<T>(string variableName)
        {
            return (T)variables[variableName];
        }
    }
}
