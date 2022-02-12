using System;
using Roost;

public static class TheRoostMachine
{
    private static bool alreadyAssembled = false;

    public static void Initialise()
    {
        if (alreadyAssembled == true)
            Birdsong.Sing("Trying to initialise the Roost Machine for the second time (don't do that!)");
        else
            try
            {
                Birdsong.currentVerbosity = (VerbosityLevel)SecretHistories.UI.Watchman.Get<Config>().GetConfigValueAsInt("verbosity");
                //in case something breaks during the setup
                SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();

                Roost.Enactors.Beachcomber.Enact();
                Roost.Enactors.Elegiast.Enact();
                Roost.Enactors.Vagabond.Enact();
                Roost.Enactors.Twins.Enact();
                Roost.Enactors.World.Enact();

                SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();

                alreadyAssembled = true;
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex);
            }
    }
}

namespace Roost.Enactors
{
    internal static class Beachcomber
    {
        internal static void Enact()
        {
            Roost.Beachcomber.Cuckoo.Enact();
            Roost.Beachcomber.Usurper.OverthrowNativeImporting();
        }
    }

    internal static class Vagabond
    {
        internal static void Enact()
        {
            Roost.Vagabond.MenuMask.Enact();
            Roost.Vagabond.ConfigMask.Enact();
            Roost.Vagabond.CommandLine.Enact();
        }
    }

    internal static class Twins
    {
        internal static void Enact()
        {
            Roost.Twins.TokenContextAccessors.Enact();
        }
    }

    internal static class Elegiast
    {
        public const string enabledSettingId = "ElegiastEnabled";
        public const string patchId = "theroostmachine.elegiast";

        internal static void Enact()
        {
            if (Machine.GetConfigValue<int>(enabledSettingId, 1) == 1)
                Roost.Elegiast.CustomAchievementsManager.Enact();
        }
    }

    internal static class World
    {
        public const string enabledSettingId = "ExpressionsEnabled";
        public const string patchId = "theroostmachine.theworld";

        internal static void Enact()
        {
          if (Machine.GetConfigValue<int>(enabledSettingId, 1) == 1)
              Roost.World.Recipes.RecipeEffectsExtension.Enact();

            Roost.World.Recipes.Legerdemain.Enact();
            Roost.World.Elements.CardVFXMaster.Enact();
            Roost.World.Recipes.Inductions.InductionsExtensions.Enact();
            Roost.World.BugsPicker.Fix();
        }
    }
}

namespace Roost
{
    ///accessor/wrapper class for all the features from all other modules
    ///partial, so each set of wrapping methods are defined in a corresponding file
    public partial class Machine { }
}