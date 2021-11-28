using System;
using System.Reflection;

using TheRoost;

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
        //Invoke<TheRoost.Nowhere.TheLeak>();

        SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
        _alreadyAssembled = true;
    }

    static void Invoke<T>()
    {
        typeof(T).GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
    }
}