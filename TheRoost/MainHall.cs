using System;
using System.Reflection;

using TheRoostManchine;
using HarmonyLib;

internal static class TheRoostMachine
{
    private static bool _alreadyAssembled = false;
    public static bool alreadyAssembled { get { return _alreadyAssembled; } }

    static readonly Harmony harmony = new Harmony("theroostmachine");

    public static void Initialise()
    {
        if (alreadyAssembled)
            return;

        //in case something breaks during the setup
        SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();

        Invoke<Beachcomber>();
        Invoke<Elegiast>();
        Invoke<Vagabond>();
        Invoke<TheWorld>();

        SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
        _alreadyAssembled = true;
    }

    //want to keep Invoke methods private, so use this roundabout way to init the modules
    static void Invoke<T>()
    {
        typeof(T).GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
    }

    public static void Patch(MethodInfo original, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null)
    {
        harmony.Patch(original,
            prefix: prefix == null ? null : new HarmonyMethod(prefix),
            postfix: postfix == null ? null : new HarmonyMethod(postfix),
            transpiler: transpiler == null ? null : new HarmonyMethod(transpiler),
            finalizer: finalizer == null ? null : new HarmonyMethod(finalizer));
    }

}