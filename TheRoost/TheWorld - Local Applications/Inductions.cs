using System;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Commands.SituationCommands;

namespace Roost.World.Recipes.Inductions
{
    public static class InductionsExtensions
    {
        public static Action<Situation, Recipe, Expulsion> SpawnNewSituation;
        private static Situation currentSituation;
        internal static void Enact()
        {
            Machine.Patch(typeof(AttemptAspectInductionCommand).GetMethodInvariant("Execute"),
                prefix: typeof(InductionsExtensions).GetMethodInvariant("StoreSituation"));

            Machine.Patch(typeof(AttemptAspectInductionCommand).GetMethodInvariant("PerformAspectInduction"),
                prefix: typeof(InductionsExtensions).GetMethodInvariant("PerformAspectInduction"));

            SpawnNewSituation = Delegate.CreateDelegate(typeof(Action<Situation, Recipe, Expulsion>), typeof(Situation).GetMethodInvariant("SpawnNewSituation")) as Action<Situation, Recipe, Expulsion>;
        }

        private static void StoreSituation(Situation situation)
        {
            currentSituation = situation;
        }

        private static bool PerformAspectInduction(Element aspectElement, Situation situation)
        {
            AspectsInContext aspectsInContext = Watchman.Get<HornedAxe>().GetAspectsInContext(situation.GetAspects(true));
            foreach (LinkedRecipeDetails linkedRecipeDetails in aspectElement.Induces)
                if (Watchman.Get<IDice>().Rolld100(null) <= linkedRecipeDetails.Chance)
                {
                    Recipe recipe = Watchman.Get<Compendium>().GetEntityById<Recipe>(linkedRecipeDetails.Id);
                    if (recipe.RequirementsSatisfiedBy(aspectsInContext))
                        SpawnNewSituation(currentSituation, recipe, linkedRecipeDetails.Expulsion);
                }

            return false;
        }
    }
}


