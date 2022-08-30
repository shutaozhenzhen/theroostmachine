using System;
using Roost;

public static class TheRoostMachine
{
    private static bool alreadyAssembled = false;

    public static void Initialise()
    {
        //Fallback handler could not load library E:/Games/Steam/steamapps/common/Cultist Simulator/cultistsimulator_Data/MonoBleedingEdge/data-28B2E008.dll
        //the error happens solely (to reiterate, SOLELY) because of Harmony.Patch() method; it's not a weird reference or wrapping problem or something
        //even an empty project trying to call for Harmony.Patch() of any content (or PatchAll()) causes the error
        //the only possiblity is that it's some kind of weird properties/framework version bug, but I've no idea about that

        if (alreadyAssembled == true)
            Birdsong.Tweet("Trying to initialise the Roost Machine for the second time (don't do that!)");
        else
            try
            {
                Birdsong.SetVerbosityFromConfig(Roost.Vagabond.ConfigMask.GetConfigValueSafe("verbosity", 1));

                //in case something breaks during the setup
                SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();

                Roost.Enactors.Beachcomber.Enact();
                Roost.Enactors.Vagabond.Enact();
                Roost.Enactors.Elegiast.Enact();
                Roost.Enactors.Twins.Enact();
                Roost.Enactors.World.Enact();

                SecretHistories.UI.Watchman.Get<SecretHistories.Services.Concursum>().ToggleSecretHistory();

                alreadyAssembled = true;
            }
            catch (Exception ex)
            {
                Birdsong.Tweet(ex);
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
            Roost.Beachcomber.Usurper.OverthrowNativeImportingButNotCompletely();
        }
    }

    internal static class Vagabond
    {
        internal static void Enact()
        {
            Roost.Vagabond.MenuMask.Enact();
            Roost.Vagabond.ConfigMask.Enact();
            Roost.Vagabond.CommandLine.Enact();
            Roost.Vagabond.CustomSavesMaster.Enact();
        }
    }

    internal static class Elegiast
    {
        internal static void Enact()
        {
            Roost.Elegiast.CustomAchievementsManager.Enact();
            Roost.Elegiast.RoostChronicler.Enact();
        }
    }

    internal static class Twins
    {
        internal static void Enact()
        {
            Roost.Twins.Crossroads.Enact();
        }
    }

    internal static class World
    {
        internal static void Enact()
        {
            Roost.World.Optimizations.Enact();

            Roost.World.Recipes.SituationWindowMaster.Enact();
            Roost.World.Recipes.RecipeEffectsMaster.Enact();
            Roost.World.Recipes.RecipeLinkMaster.Enact();

            Roost.World.Elements.ElementEffectsMaster.Enact();

            Roost.World.Slots.XAngelMaster.Enact();
            Roost.World.Slots.SlotEffectsMaster.Enact();
            Roost.World.Slots.RecipeMultipleSlotsMaster.Enact();

            Roost.World.RecipeCallbacksMaster.Enact();

            Roost.World.Beauty.StartupQuoteMaster.Enact();
            Roost.World.Beauty.MainMenuStyleMaster.Enact();
            Roost.World.Beauty.TableStyleMaster.Enact();

            Roost.World.Beauty.SlideshowBurnImagesMaster.Enact();
            Roost.World.Beauty.TMPSpriteManager.Enact();
            Roost.World.Beauty.CardStyleMaster.Enact();

            Roost.World.Audio.TrumpetLily.Enact();
        }
    }
}

namespace Roost
{
    ///accessor/wrapper class for all the features from all other modules
    ///partial, so each set of wrapping methods are defined in a corresponding file
    public static partial class Machine { }
}