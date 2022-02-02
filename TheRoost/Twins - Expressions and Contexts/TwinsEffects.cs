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
        //Dictionary<SphereRef, GrandEffects>
        const string grandEffects = "@grand";

        static readonly EffectsBuffer storedRecipeEffects = new EffectsBuffer();

        private static bool propertiesClaimed = false;

        internal static void Enact()
        {
            AtTimeOfPower.RecipeRequirementsCheck.Schedule<Recipe, AspectsInContext, bool>(RefReqs, Enactors.Twins.patchId);
            AtTimeOfPower.RecipeExecution.Schedule<RecipeCompletionEffectCommand, Situation>(ExecuteEffectsWithReferences, PatchType.Prefix, Enactors.Twins.patchId);

            Machine.Patch(typeof(ElementStack).GetMethodInvariant("SetMutation"),
                postfix: typeof(ExpressionEffects).GetMethodInvariant("FixMutationsDisplay"));

            //in case player disables/enables the module several times, so it won't clog the log with "already claimed" messages
            if (propertiesClaimed == false)
            {
                Machine.ClaimProperty<Recipe, Dictionary<Funcine<int>, Funcine<int>>>(refReqs);
                Machine.ClaimProperty<Recipe, Dictionary<Funcine<bool>, Funcine<int>>>(refEffects);
                Machine.ClaimProperty<Recipe, Dictionary<Funcine<bool>, List<RefMutationEffect>>>(refMutations);
                Machine.ClaimProperty<Recipe, Dictionary<SphereRef, GrandEffects>>(grandEffects);

                TheRoost.Vagabond.CommandLine.AddCommand("ref", TwinsDebug.TestReference);
                TheRoost.Vagabond.CommandLine.AddCommand("exp", TwinsDebug.TestExpression);
                TheRoost.Vagabond.CommandLine.AddCommand("spheres", TwinsDebug.LogAllSpheres);
                TheRoost.Vagabond.CommandLine.AddCommand("sphere", TwinsDebug.SphereContent);
                propertiesClaimed = true;
            }
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
            //but I really need to know a *situation* instead of just aspects; and there is no easier way to go about it
            bool situationFound = false;
            foreach (Situation situation in Watchman.Get<HornedAxe>().GetRegisteredSituations())
                if (situation.GetAspects(true).AspectsEqual(aspectsinContext.AspectsInSituation))
                {
                    TokenContextAccessors.SetLocalSituation(situation);
                    situationFound = true;
                    break;
                }
            if (!situationFound)
                throw Birdsong.Droppings("Something strange happened. Cannot identify the current situation for requirements check.");

            bool result = true;

            //Birdsong.Sing("Checking @reqs for {0}", __instance.Id);
            foreach (KeyValuePair<Funcine<int>, Funcine<int>> req in reqs)
            {
                int presentValue = req.Key.result;
                int requiredValue = req.Value.result;

                //Birdsong.Sing("'{0}': '{1}' ---> '{2}': '{3}', {4}", req.Key.formula, req.Value.formula, presentValue, requiredValue, (requiredValue <= -1 && presentValue >= -requiredValue) || (requiredValue > -1 && presentValue < requiredValue) ? "not satisfied" : "satisfied");

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
            Recipe recipe = __instance.Recipe;

            ExecuteRefMutations(storage, recipe.RetrieveProperty<Dictionary<Funcine<bool>, List<RefMutationEffect>>>(refMutations));
            ExecuteRefEffects(storage, recipe.RetrieveProperty<Dictionary<Funcine<bool>, Funcine<int>>>(refEffects));
            ExecuteGrandEffects(recipe.RetrieveProperty<Dictionary<SphereRef, GrandEffects>>(grandEffects));

            storedRecipeEffects.Execute();
        }

        private static void ExecuteRefMutations(Sphere sphere, Dictionary<Funcine<bool>, List<RefMutationEffect>> mutations)
        {
            if (mutations == null)
                return;

            List<Token> tokens = sphere.GetElementTokens();

            foreach (Funcine<bool> filter in mutations.Keys)
            {
                List<Token> targets = tokens.FilterTokens(filter);

                if (targets.Count > 0) foreach (RefMutationEffect mutationEffect in mutations[filter])
                        storedRecipeEffects.ScheduleMutation(targets, mutationEffect.Mutate, mutationEffect.Level.result, mutationEffect.Additive);
            }
        }

        private static void ExecuteRefEffects(Sphere storage, Dictionary<Funcine<bool>, Funcine<int>> effects)
        {
            if (effects == null)
                return;

            List<Token> allTokens = storage.GetElementTokens();
            foreach (Funcine<bool> filter in effects.Keys)
            {
                int level = effects[filter].result;
                if (level < 0)
                {
                    List<Token> filteredTokens = allTokens.FilterTokens(filter);
                    while (level < 0 && filteredTokens.Count > 0)
                    {
                        storedRecipeEffects.ScheduleTokenRetirement(filteredTokens[UnityEngine.Random.Range(0, filteredTokens.Count)]);
                        level++;
                    }
                }
                else
                    storedRecipeEffects.ScheduleCreation(storage, filter.formula, level);
            }
        }

        private static void ExecuteGrandEffects(Dictionary<SphereRef, GrandEffects> grandEffects)
        {
            if (grandEffects == null)
                return;

            foreach (SphereRef sphere in grandEffects.Keys)
            {
                Sphere storage = sphere.target;
                if (storage == Assets.Scripts.Application.Entities.NullEntities.NullSphere.Create())
                    continue;

                ExecuteRefMutations(storage, grandEffects[sphere].mutations);
                ExecuteRefEffects(storage, grandEffects[sphere].effects);
            }
        }
    }

    public class EffectsBuffer
    {
        private readonly List<Token> tokensToRetire = new List<Token>();
        private readonly Dictionary<Sphere, AspectsDictionary> elementsToCreate = new Dictionary<Sphere, AspectsDictionary>();
        private static readonly Dictionary<CachedMutationEffect, List<Token>> mutationsToExecute = new Dictionary<CachedMutationEffect, List<Token>>();

        public void Execute()
        {
            foreach (CachedMutationEffect mutation in mutationsToExecute.Keys)
                foreach (Token onToken in mutationsToExecute[mutation])
                    mutation.Apply(onToken);
            mutationsToExecute.Clear();

            Context context = new Context(Context.ActionSource.SituationEffect);
            foreach (Sphere sphere in elementsToCreate.Keys)
                foreach (string element in elementsToCreate[sphere].Keys)
                    sphere.ModifyElementQuantity(element, elementsToCreate[sphere][element], context);
            elementsToCreate.Clear();

            foreach (Token token in tokensToRetire)
                token.Retire(RetirementVFX.None);
            tokensToRetire.Clear();
        }

        public void ScheduleCreation(Sphere sphere, string element, int amount)
        {
            if (elementsToCreate.ContainsKey(sphere) == false)
                elementsToCreate[sphere] = new AspectsDictionary();

            elementsToCreate[sphere][element] = elementsToCreate[sphere].AspectValue(element) + amount;
        }

        public void ScheduleTokenRetirement(Token token)
        {
            tokensToRetire.Add(token);
        }

        public void ScheduleMutation(Token token, string mutate, int level, bool additive)
        {
            CachedMutationEffect mutation = new CachedMutationEffect(mutate, level, additive);
            if (mutationsToExecute.ContainsKey(mutation) == false)
                mutationsToExecute[mutation] = new List<Token>();
            mutationsToExecute[mutation].Add(token);
        }

        public void ScheduleMutation(List<Token> tokens, string mutate, int level, bool additive)
        {
            CachedMutationEffect mutation = new CachedMutationEffect(mutate, level, additive);
            if (mutationsToExecute.ContainsKey(mutation) == false)
                mutationsToExecute[mutation] = new List<Token>();
            mutationsToExecute[mutation].AddRange(tokens);
        }

        private struct CachedMutationEffect
        {
            string mutate; int level; bool additive;
            public CachedMutationEffect(string mutate, int level, bool additive) { this.mutate = mutate; this.level = level; this.additive = additive; }
            public void Apply(Token onToken) { onToken.Payload.SetMutation(mutate, level, additive); }
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

    public class GrandEffects : AbstractEntity<GrandEffects>
    {
        [FucineValue]
        public Dictionary<Funcine<bool>, List<RefMutationEffect>> mutations { get; set; }
        [FucineValue]
        public Dictionary<Funcine<bool>, Funcine<int>> effects { get; set; }

        public GrandEffects(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }
    }
}
