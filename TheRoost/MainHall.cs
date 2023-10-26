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
            Birdsong.TweetLoud("Trying to initialise the Roost Machine for the second time (don't do that!)");
        else
            try
            {
                Birdsong.SetVerbosityFromConfig(Roost.Vagabond.ConfigMask.GetConfigValueSafe("verbosity", 1));

                Roost.Enactors.Beachcomber.Enact();
                Roost.Enactors.Vagabond.Enact();
                Roost.Enactors.Elegiast.Enact();
                Roost.Enactors.Twins.Enact();
                Roost.Enactors.World.Enact();

                alreadyAssembled = true;
            }
            catch (Exception ex)
            {
                Birdsong.TweetLoud(ex);
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
            Roost.Beachcomber.CuckooJr.Enact();
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

            Roost.Vagabond.Saves.CustomSavesMaster.Enact();
            Roost.Vagabond.Saves.CheckpointMaster.Enact();
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
            Roost.World.Verbs.VerbUniquenessMaster.Enact();
            Roost.World.Verbs.VerbStickySlotsMaster.Enact();

            Roost.World.Recipes.GrandReqsMaster.Enact();
            Roost.World.Recipes.RecipeEffectsMaster.Enact();
            Roost.World.Recipes.RecipeLinkMaster.Enact();
            Roost.World.Recipes.RecipeNotesMaster.Enact();
            Roost.World.Recipes.RecipeRandomWildcardMaster.Enact();
            Roost.World.Recipes.RecipeCallbacksMaster.Enact();
            Roost.World.Recipes.SituationTracker.Enact();

            Roost.World.Elements.ElementRandomDecay.Enact();
            Roost.World.Elements.ElementVFXMaster.Enact();
            Roost.World.Elements.StackDisplaceMaster.Enact();
            Roost.World.Elements.StackShroudMaster.Enact();
            Roost.World.Elements.StackNoStackMaster.Enact();
            Roost.World.Elements.ElementHiddenAutoPurger.Enact();

            Roost.World.Slots.XAngelMaster.Enact();
            Roost.World.Slots.SlotPresenceReqsMaster.Enact();
            Roost.World.Slots.SlotEntranceReqsMaster.Enact();
            Roost.World.Slots.GreedySlotsMaster.Enact();

            Roost.World.Slots.RecipeMultipleSlotsMaster.Enact();

            Roost.World.Beauty.StartupQuoteMaster.Enact();
            Roost.World.Beauty.MainMenuStyleMaster.Enact();
            Roost.World.Beauty.TableStyleMaster.Enact();

            Roost.World.Beauty.BurnImages.SlideshowBurnImagesMaster.Enact();
            Roost.World.Beauty.TMPSpriteManager.Enact();
            Roost.World.Beauty.CardStyleMaster.Enact();
            Roost.World.Beauty.ElementStackRefiner.Enact();

            Roost.World.Audio.TrumpetLily.Enact();

            //Roost.World.Elements.ElementSlotMaster.Enact();
        }
    }
}

namespace Roost
{
    ///accessor/wrapper class for all the features from all other modules
    ///partial, so each set of wrapping methods are defined in a corresponding file
    public static partial class Machine { }
}