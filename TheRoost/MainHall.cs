using System;
using System.Reflection;

using UnityEngine;
using SecretHistories.Fucine;

using TheRoost.Invocations;
using TheRoost.Hoard;

internal static class TheRoostMachine
{
    private static bool _alreadyAssembled = false;
    public static bool alreadyAssembled { get { return _alreadyAssembled; } }

    public static void Initialise()
    {
        if (alreadyAssembled)
            return;

        //in case something breaks during the setup
        SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();

        Invoke<Beachcomber>();
        Invoke<Elegiast>();
        Invoke<Vagabond>();

        SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
        _alreadyAssembled = true;
    }

    static void Invoke<T>()
    {
        typeof(T).GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
    }
}

namespace TheRoost
{
    public static class TheRoost
    {
        public static void Sing(string wrapMessage, params object[] data)
        {
            NoonUtility.LogWarning(String.Format(wrapMessage, data));
        }

        public static void Sing(params object[] data)
        {
            var str = string.Empty;
            foreach (object obj in data)
                str += (obj == null ? "null" : obj.ToString()) + ' ';

            NoonUtility.LogWarning(str);
        }

        public static void ClaimProperty<T>(string propertyName, Type propertyType) where T : AbstractEntity<T>
        {
            Beachcomber.ClaimProperty<T>(propertyName, propertyType);
        }

        public static T RetrieveProperty<T>(this IEntityWithId owner, string propertyName)
        {
            return Beachcomber.RetrieveProperty<T>(owner, propertyName);
        }

        public static void InfectFucineWith<T>() where T : AbstractEntity<T>
        {
            Beachcomber.InfectFucineWith<T>();
        }
    }
}