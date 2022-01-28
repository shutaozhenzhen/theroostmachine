using System;
using TheRoost;

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
                alreadyAssembled = true;

                //in case something breaks during the setup
                SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();

                TheRoost.Enactors.Beachcomber.Enact();
                TheRoost.Enactors.Elegiast.Enact();
                TheRoost.Enactors.Vagabond.Enact();
                TheRoost.Enactors.Twins.Enact();
                TheRoost.Enactors.InaamKapigiginlupirGarkieCryppys.Enact();

                SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex);
            }
    }
}

namespace TheRoost.Enactors
{
    internal static class Beachcomber
    {
        internal static void Enact()
        {
            TheRoost.Beachcomber.CuckooLoader.Enact();
            TheRoost.Beachcomber.Usurper.OverthrowNativeImporting();
            TheRoost.Beachcomber.BugsPicker.Fix();
        }
    }

    internal static class Vagabond
    {
        internal static void Enact()
        {
            TheRoost.Vagabond.MenuMask.Enact();
            TheRoost.Vagabond.ConfigMask.Enact();
            TheRoost.Vagabond.CommandLine.Enact();
        }
    }

    internal static class Elegiast
    {
        public const string enabledSettingId = "ElegiastEnabled";
        public const string patchId = "theroostmachine.elegiast";
        private static bool propertiesClaimed = false;

        internal static void Enact()
        {
            //we claim properties (and add debug commands) regardless of enable/disable state of the module
            //so they won't clog the log with "ALREADY CLAIMED" messages if the module is disabled-enabled several times
            //I don't quite like how it's currently - how scattered everything is in particular
            //but we'll see
            if (!propertiesClaimed)
            {
                TheRoost.Elegiast.CustomAchievementsManager.ClaimProperties();
                propertiesClaimed = true;
            }

            if (Machine.GetConfigValue<int>(enabledSettingId, 1) == 1)
                TheRoost.Elegiast.CustomAchievementsManager.Enact();
        }
    }

    internal static class Twins
    {
        public const string enabledSettingId = "ExpressionsEnabled";
        public const string patchId = "theroostmachine.twins";
        private static bool propertiesClaimed = false;

        internal static void Enact()
        {
            if (!propertiesClaimed)
            {
                TheRoost.Twins.ExpressionEffects.ClaimProperties();
                TheRoost.Twins.TokenContextManager.AddDebugCommads();
                propertiesClaimed = true;
            }

            if (Machine.GetConfigValue<int>(enabledSettingId, 1) == 1)
                TheRoost.Twins.ExpressionEffects.Enact();
        }
    }

    //everything module-less comes (will come) here
    internal static class InaamKapigiginlupirGarkieCryppys
    {
        internal static void Enact()
        {
            LocalApplications.Legerdemain.Enact();
        }
    }
}

namespace TheRoost
{
    ///accessor/wrapper class for all the features from all other modules
    ///partial, so each set of wrapping methods are defined in a corresponding file
    public partial class Machine { }
}