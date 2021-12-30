using System.Reflection;
using TheRoost;
using HarmonyLib;

public static class TheRoostMachine
{
    private static bool _alreadyAssembled = false;
    public static bool alreadyAssembled { get { return _alreadyAssembled; } }
    private static readonly Harmony harmony = new Harmony("theroostmachine");

    public static void Initialise()
    {
        if (alreadyAssembled)
        {
            Birdsong.Sing("Trying to initialise the Roost Machine for the second time (don't!)");
            return;
        }

        //in case something breaks during the setup
        SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();

        TheRoost.Beachcomber.CustomLoader.Claim();
        TheRoost.Twins.EventManager.Unite(); //events
        TheRoost.Elegiast.CustomAchievements.Remember();
        Vagabond.Enter(); //command line
        Birdsong.Enact(); //little something for everyone

        _alreadyAssembled = true;
        SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
    }

    public static void Patch(MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null)
    {
        if (original == null)
            Birdsong.Sing("Trying to patch null method!");
        if (prefix == null && postfix == null && transpiler == null && finalizer == null)
            Birdsong.Sing("All patches for {0}() are null!", original.Name);

        harmony.Patch(original,
            prefix: prefix == null ? null : new HarmonyMethod(prefix),
            postfix: postfix == null ? null : new HarmonyMethod(postfix),
            transpiler: transpiler == null ? null : new HarmonyMethod(transpiler),
            finalizer: finalizer == null ? null : new HarmonyMethod(finalizer));
    }

}