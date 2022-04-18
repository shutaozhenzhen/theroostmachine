using System;
using System.Collections.Generic;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Commands.SituationCommands;
using SecretHistories.Core;
using SecretHistories.Fucine;

namespace Roost.World.Recipes
{
    public static class RecipeLinkMaster
    {
        public static Action<Situation, Recipe, Expulsion, FucinePath> SpawnNewSituation;
        private static Situation currentSituation;
        private static List<Recipe> xtriggerLinks = new List<Recipe>();

        internal static void Enact()
        {
            Machine.Patch(typeof(AttemptAspectInductionCommand).GetMethodInvariant("Execute"),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant("StoreSituationFromWhichInductionHappens"));

            Machine.Patch(typeof(AttemptAspectInductionCommand).GetMethodInvariant("PerformAspectInduction"),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant("PerformAspectInduction"));

            Machine.Patch(typeof(RecipeConductor).GetMethodInvariant("GetLinkedRecipe"),
                prefix: typeof(RecipeLinkMaster).GetMethodInvariant("CheckXtriggerLinks"));


            SpawnNewSituation = Delegate.CreateDelegate(typeof(Action<Situation, Recipe, Expulsion, FucinePath>), typeof(Situation).GetMethodInvariant("SpawnNewSituation")) as Action<Situation, Recipe, Expulsion, FucinePath>;
        }

        //AttemptAspectInductionCommand.Execute()
        private static void StoreSituationFromWhichInductionHappens(Situation situation)
        {
            currentSituation = situation;
        }

        //AttemptAspectInductionCommand.PerformAspectInduction()
        private static bool PerformAspectInduction(Element aspectElement, Situation situation)
        {
            AspectsInContext aspectsInContext = Watchman.Get<HornedAxe>().GetAspectsInContext(situation.GetAspects(true), null);
            foreach (LinkedRecipeDetails linkedRecipeDetails in aspectElement.Induces)
                if (Watchman.Get<IDice>().Rolld100(null) <= linkedRecipeDetails.Chance)
                {
                    Recipe recipe = Watchman.Get<Compendium>().GetEntityById<Recipe>(linkedRecipeDetails.Id);
                    if (recipe.RequirementsSatisfiedBy(aspectsInContext))

                        SpawnNewSituation(currentSituation, recipe, linkedRecipeDetails.Expulsion, FucinePath.Current());
                }

            return false;
        }

        //RecipeConductor.GetLinkedRecipe() prefix
        private static bool CheckXtriggerLinks(ref Recipe __result, AspectsInContext ____aspectsInContext)
        {
            foreach (Recipe recipe in xtriggerLinks)
                if (recipe.RequirementsSatisfiedBy(____aspectsInContext))
                {
                    __result = recipe;
                    break;
                }

            xtriggerLinks.Clear();
            return __result == null;
        }

        internal static void PushXtriggerLink(Recipe recipe, int orderShift)
        {
            xtriggerLinks.Insert(Math.Min(xtriggerLinks.Count, xtriggerLinks.Count + orderShift), recipe);
        }
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static void PushXtriggerLink(Recipe recipe, int orderShift = 0)
        {
            Roost.World.Recipes.RecipeLinkMaster.PushXtriggerLink(recipe, orderShift);
        }

        public static void PushXtriggerLink(string recipeId, int orderShift = 0)
        {
            Recipe recipe = Watchman.Get<Compendium>().GetEntityById<Recipe>(recipeId);
            if (recipeId == null)
                Birdsong.Sing("Trying to push non-existed recipe link '{0}'", recipeId);
            else
                PushXtriggerLink(recipe, orderShift);
        }
    }
}


