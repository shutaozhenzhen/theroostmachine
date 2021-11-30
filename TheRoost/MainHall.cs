using System;
using System.Reflection;

using TheRoost;
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

        Beachcomber.Enact();
        Elegiast.Enact();
        Vagabond.Enact();
        TheWorld.Enact();
        Birdsong.Enact();

        _alreadyAssembled = true;
        SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
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