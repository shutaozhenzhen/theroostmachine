using System;
using System.Collections.Generic;
using System.Linq;
using SecretHistories.Entities;
using SecretHistories.Logic;
using SecretHistories.UI;
using Roost.Twins.Entities;

namespace Roost.World.Recipes
{
    static class RecipeRandomWildcardMaster
    {
        const string RANDOM_PICK = "randomPick";
        const string VALIDATION_CHANCES = "chances";

        internal static void Enact()
        {
            Machine.ClaimProperty<LinkedRecipeDetails, bool>(RANDOM_PICK, defaultValue: false);
            Machine.ClaimProperty<LinkedRecipeDetails, Dictionary<String, FucineExp<int>>>(VALIDATION_CHANCES);
            Machine.Patch(
                original: typeof(LinkedRecipeDetails).GetMethodInvariant(nameof(LinkedRecipeDetails.GetRecipeWhichCanExecuteInContext)),
                prefix: typeof(RecipeRandomWildcardMaster).GetMethodInvariant(nameof(HandleRandomPick)));
        }

        static bool ChanceRoll(this Recipe recipe, Dictionary<string, FucineExp<int>> Chances)
        {
            if (!Chances.TryGetValue(recipe.Id, out FucineExp<int> chance)) 
                return true;

            return Watchman.Get<IDice>().Rolld100() <= chance.value;
        }

        static bool HandleRandomPick(AspectsInContext aspectsInContext, Character character, LinkedRecipeDetails __instance, List<Recipe> ____possibleMatchesRecipes, ref Recipe __result)
        {
            if (!__instance.RetrieveProperty<bool>(RANDOM_PICK))
                return true;

            __result = NullRecipe.Create();

            if (!__instance.ShouldAlwaysSucceed() && Watchman.Get<IDice>().Rolld100() > ChallengeArbiter.GetArbitratedChance(__instance, aspectsInContext.AspectsInSituation))
                return false;

            Dictionary<string, FucineExp<int>> chances = __instance.RetrieveProperty<Dictionary<string, FucineExp<int>>>(VALIDATION_CHANCES);

            List<Recipe> validRecipes = new List<Recipe>();

            if (chances == null)
            {
                foreach (Recipe recipe in ____possibleMatchesRecipes)
                    if (recipe.CanExecuteInContext(aspectsInContext, character))
                        validRecipes.Add(recipe);
            }
            else
            {
                foreach (Recipe recipe in ____possibleMatchesRecipes)
                    if (recipe.ChanceRoll(chances) && recipe.CanExecuteInContext(aspectsInContext, character))
                        validRecipes.Add(recipe);
            }

            if (validRecipes.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, validRecipes.Count);
                __result = validRecipes[randomIndex];
            }

            return false;
        }
    }
}
