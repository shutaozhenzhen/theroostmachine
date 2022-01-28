using System;
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;
using TheRoost;

public static class TheRoostMachine
{
    public static bool alreadyAssembled { get { return _alreadyAssembled; } }
    private static bool _alreadyAssembled = false;

    private static readonly Dictionary<string, Harmony> patchers = new Dictionary<string, Harmony>();
    public const string defaultPatchId = "theroostmachine";

    public static void Initialise()
    {
        if (alreadyAssembled)
        {
            Birdsong.Sing("Trying to initialise the Roost Machine for the second time (don't!)");
            return;
        }

        try
        {
            _alreadyAssembled = true;

            //in case something breaks during the setup
            SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();

            TheRoost.Vagabond.RoostConfig.Enact();
            TheRoost.Beachcomber.CuckooLoader.Enact();
            TheRoost.Vagabond.MenuManager.Enact();
            TheRoost.Elegiast.CustomAchievementsManager.Enact();

            SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
        }
        catch (Exception ex)
        {
            Birdsong.Sing(ex);
        }
    }

    public static void Patch(MethodBase original,
        MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null,
        string patchId = defaultPatchId)
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
}