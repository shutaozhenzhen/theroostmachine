using System.Collections;
using System.Collections.Generic;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

using TheRoost.Twins.Entities;

namespace TheRoost.Twins
{
    public static class ExpressionEffects
    {
        //Dictionary<Funcine<int>, Funcine<int>> - both parts are basically numbers which are checked by normal CS req rules
        const string refReqs = "@reqs";
        //Dictionary<string, Funcine<int>> - left side is just an element id, right side is an amount
        const string refEffects = "@effects";
        //Dictionary<Funcine<bool>, List<RefMutationEffect>> - right side is a filter by which affected tokens are selected, left is list of mutation effects;
        //mutation effects are identical to normal ones, not counting WeirdSpecs quirks, with the only difference of Level being Funcine<int>
        const string refMutations = "@mutations";

        internal static void ClaimProperties()
        {
            Machine.ClaimProperty<Recipe, Dictionary<Funcine<int>, Funcine<int>>>(refReqs);
            Machine.ClaimProperty<Recipe, Dictionary<Funcine<bool>, Funcine<int>>>(refEffects);
            Machine.ClaimProperty<Recipe, Dictionary<Funcine<bool>, List<RefMutationEffect>>>(refMutations);

            TheRoost.Vagabond.CommandLine.AddCommand("ref", TokenContextAccessors.TestReference);
            TheRoost.Vagabond.CommandLine.AddCommand("exp", TokenContextAccessors.TestExpression);
            TheRoost.Vagabond.CommandLine.AddCommand("spheres", TokenContextAccessors.LogAllSpheres);
            TheRoost.Vagabond.CommandLine.AddCommand("sphere", TokenContextAccessors.SphereContent);
        }

        internal static void Enact()
        {
            AtTimeOfPower.RecipeRequirementsCheck.Schedule<Recipe, AspectsInContext, bool>(RefReqs, Enactors.Twins.patchId);
            AtTimeOfPower.RecipeExecution.Schedule<RecipeCompletionEffectCommand, Situation>(ExecuteEffectsWithReferences, PatchType.Prefix, Enactors.Twins.patchId);

            Machine.Patch(typeof(ElementStack).GetMethodInvariant("SetMutation"),
                postfix: typeof(ExpressionEffects).GetMethodInvariant("FixMutationsDisplay"));
        }

        private static void FixMutationsDisplay(ElementStack __instance)
        {
            Context context = new Context(Context.ActionSource.SituationEffect);
            var sphereContentsChangedEventArgs = new SecretHistories.Constants.Events.SphereContentsChangedEventArgs(__instance.Token.Sphere, context);
            sphereContentsChangedEventArgs.TokenChanged = __instance.Token;
            __instance.Token.Sphere.NotifyTokensChangedForSphere(sphereContentsChangedEventArgs);
        }

        private static bool RefReqs(Recipe __instance, AspectsInContext aspectsinContext, bool __result)
        {
            Dictionary<Funcine<int>, Funcine<int>> reqs = __instance.RetrieveProperty<Dictionary<Funcine<int>, Funcine<int>>>(refReqs);
            if (reqs == null)
                return true;

            //what I am about to do here should be illegal (and will be at some point of time in the bright future of humankind)
            //but I really need to know a *situation* instead of just aspects; and there is no easier way to find it
            bool situaitionFound = false;
            foreach (Situation situation in Watchman.Get<HornedAxe>().GetRegisteredSituations())
            {
                if (situation.GetAspects(true).AspectsEqual(aspectsinContext.AspectsInSituation))
                {
                    TokenContextAccessors.SetLocalSituation(situation);
                    situaitionFound = true;
                    break;
                }
            }

            if (!situaitionFound)
                throw Birdsong.Droppings("Something strange happened. Cannot identify the current situation for requirements check.");

            bool result = true;

            Birdsong.Sing("Checking @reqs for {0}", __instance.Id);
            foreach (KeyValuePair<Funcine<int>, Funcine<int>> req in reqs)
            {
                int presentValue = req.Key.result;
                int requiredValue = req.Value.result;

                Birdsong.Sing("'{0}': '{1}' ---> '{2}': '{3}', {4}", req.Key.formula, req.Value.formula, presentValue, requiredValue, (requiredValue <= -1 && presentValue >= -requiredValue) || (requiredValue > -1 && presentValue < requiredValue) ? "not satisfied" : "satisfied");

                if (requiredValue <= -1)
                {
                    if (presentValue >= -requiredValue)
                    {
                        result = false;
                        break;
                    }
                }
                else
                {
                    if (presentValue < requiredValue)
                    {
                        result = false;
                        break;
                    }
                }
            }

            __result = result;
            return result;
        }

        private static bool AspectsEqual(this AspectsDictionary dictionary1, AspectsDictionary dictionary2)
        {
            if (dictionary1 == dictionary2) return true;
            if ((dictionary1 == null) || (dictionary2 == null)) return false;
            if (dictionary1.Count != dictionary2.Count) return false;

            foreach (string key in dictionary2.Keys)
                if (dictionary1.ContainsKey(key) == false || dictionary1[key] != dictionary2[key])
                    return false;

            return true;
        }

        private static void ExecuteEffectsWithReferences(RecipeCompletionEffectCommand __instance, Situation situation)
        {
            TokenContextAccessors.SetLocalSituation(situation);

            Sphere storage = situation.GetSingleSphereByCategory(SphereCategory.SituationStorage);

            RefMutations(__instance.Recipe, storage);
            RefEffects(__instance.Recipe, storage);
        }

        private static void RefEffects(Recipe recipe, Sphere storage)
        {
            Dictionary<Funcine<bool>, Funcine<int>> effects = recipe.RetrieveProperty<Dictionary<Funcine<bool>, Funcine<int>>>(refEffects);
            if (effects == null)
                return;

            List<Token> allTokens = storage.GetElementTokens();
            foreach (Funcine<bool> filter in effects.Keys)
            {
                int level = effects[filter].result;
                if (level < 0)
                    foreach (Token appropriateToken in allTokens.FilterTokens(filter))
                    {
                        appropriateToken.Retire();
                        if (++level == 0)
                            break;
                    }
                else
                    storage.ModifyElementQuantity(filter.formula, level, new Context(Context.ActionSource.SituationEffect));
            }
        }

        private static void RefMutations(Recipe recipe, Sphere sphere)
        {
            Dictionary<Funcine<bool>, List<RefMutationEffect>> mutations = recipe.RetrieveProperty<Dictionary<Funcine<bool>, List<RefMutationEffect>>>(refMutations);
            if (mutations == null)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (Funcine<bool> filter in mutations.Keys)
            {
                List<Token> targets = tokens.FilterTokens(filter);

                if (targets.Count > 0)
                    foreach (RefMutationEffect mutationEffect in mutations[filter])
                    {
                        int level = mutationEffect.Level.result;
                        foreach (Token token in targets)
                            token.Payload.SetMutation(mutationEffect.Mutate, level, mutationEffect.Additive);
                    }
            }
        }
    }
}

namespace TheRoost.Twins.Entities
{
    public class RefMutationEffect : AbstractEntity<RefMutationEffect>, IWeirdSpecEntity
    {
        [FucineValue(false)]
        public bool Additive { get; set; }
        [FucineStruct("1")]
        public Funcine<int> Level { get; set; }
        [FucineValue(ValidateAsElementId = true, DefaultValue = null)]
        public string Mutate { get; set; }

        public RefMutationEffect(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }

        public void WeirdSpec(Hashtable data)
        {
            if (Mutate == null)
            {
                foreach (object key in UnknownProperties.Keys)
                {
                    this.Mutate = key.ToString();
                    this.Level = new Funcine<int>(UnknownProperties[key].ToString());
                    break;
                }
                UnknownProperties.Remove(Mutate);
            }
        }
    }
}
