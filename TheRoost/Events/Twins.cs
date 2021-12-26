using System;
using System.Collections.Generic;
using System.Reflection;

using SecretHistories.Infrastructure;

namespace TheRoost
{
    public enum AtTimeOfPower
    {
        MainMenuLoaded, TabletopLoaded, NewGameStarted
    }

    public static class Twins
    {
        static Dictionary<AtTimeOfPower, InjectedAction> timesofpower;
        public delegate void InjectedAction();

        public static void Schedule(this AtTimeOfPower time, InjectedAction action)
        {
            if (timesofpower == null)
            {
                timesofpower = new Dictionary<AtTimeOfPower, InjectedAction>();
                foreach (AtTimeOfPower t in Enum.GetValues(typeof(AtTimeOfPower)))
                    timesofpower.Add(t, null);
            }

            timesofpower[time] -= action;
            timesofpower[time] += action;
        }

        public static void Unschedule(AtTimeOfPower time, InjectedAction action)
        {
            if (timesofpower == null)
            {
                timesofpower = new Dictionary<AtTimeOfPower, InjectedAction>();
                foreach (AtTimeOfPower t in Enum.GetValues(typeof(AtTimeOfPower)))
                    timesofpower.Add(t, null);
            }

            timesofpower[time] -= action;
        }

        internal static void Unite()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            if (timesofpower == null)
            {
                timesofpower = new Dictionary<AtTimeOfPower, InjectedAction>();
                foreach (AtTimeOfPower t in Enum.GetValues(typeof(AtTimeOfPower)))
                    timesofpower.Add(t, null);
            }

            TheRoostMachine.Patch(
                original: typeof(MenuScreenController).GetMethod("InitialiseServices", BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: AtTimeOfPower.MainMenuLoaded.GetImplementation());

            TheRoostMachine.Patch(
                original: typeof(MenuScreenController).GetMethod("BeginNewSaveWithSpecifiedLegacy", BindingFlags.Instance | BindingFlags.Public),
                postfix: AtTimeOfPower.NewGameStarted.GetImplementation());

            TheRoostMachine.Patch(
                original: typeof(GameGateway).GetMethod("Start", BindingFlags.Instance | BindingFlags.Public),
                postfix: AtTimeOfPower.TabletopLoaded.GetImplementation());
        }

        static MethodInfo GetImplementation(this AtTimeOfPower time)
        {
            return typeof(Twins).GetMethod(time.ToString(), BindingFlags.Static | BindingFlags.NonPublic);
        }

        static void InvokeTime(AtTimeOfPower time)
        {
            if (timesofpower[time] != null)
                timesofpower[time].Invoke();
        }

        static void MainMenuLoaded()
        {
            InvokeTime(AtTimeOfPower.MainMenuLoaded);
        }

        static void TabletopLoaded()
        {
            InvokeTime(AtTimeOfPower.TabletopLoaded);
        }

        static void NewGameStarted()
        {
            InvokeTime(AtTimeOfPower.NewGameStarted);
        }
    }
}
