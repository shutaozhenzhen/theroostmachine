using System;
using System.Collections;
using System.Collections.Generic;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

using Roost.Twins;
using Roost.Twins.Entities;
using Roost.World.Recipes.Entities;

namespace Roost.World.Recipes
{
    public static class RecipeEffectsMaster
    {
        const string REF_REQS = "grandreqs";
        const string EXECUTE = "execute";
        const string REF_ELEMENT_XTRIGGERS = "xtriggers";
        static string[] orderedNativeExecutionProperties = new string[] { "mutations", "aspects", "deckeffects", "effects" };

        private static bool propertiesClaimed = false;

        internal static void Enact()
        {
            //in case player disables/enables the module several times, so it won't clog the log with "already claimed" messages
            if (propertiesClaimed == false)
            {
                Machine.ClaimProperty<Element, Dictionary<string, List<RefMorphDetails>>>(REF_ELEMENT_XTRIGGERS);

                //so, the actual execution of all new effects happens via the ORDERED_EFFECTS list that contains IRecipeExecutionEffect entities
                //so, technically, I don't need to claim anything else from these:
                Machine.ClaimProperty<Recipe, Dictionary<Funcine<int>, Funcine<int>>>(REF_REQS);
                Machine.ClaimProperty<Recipe, List<IRecipeExecutionEffect>>(EXECUTE);

                //but, as part of my overall design aimed to reduce the overall verbosity of jsons, 
                //I allow to define "vanilla" part of that list in recipe's "root" definition instead of forcing to always nest it inside the list
                //for that end I claim every property of RecipeEffectsGroup for the recipe, so they are imported as correct, new value types (expressions etc)
                //later on, after the loading is completed, I insert these properties into ordered effects list in vanilla's order
                Dictionary<string, Type> allRecipeEffectsProperties = new Dictionary<string, Type>();
                foreach (CachedFucineProperty<RecipeEffectsGroup> cachedProperty in TypeInfoCache<RecipeEffectsGroup>.GetCachedFucinePropertiesForType())
                    allRecipeEffectsProperties.Add(cachedProperty.LowerCaseName, cachedProperty.ThisPropInfo.PropertyType);
                Machine.ClaimProperties<Recipe>(allRecipeEffectsProperties);

                propertiesClaimed = true;
            }

            AtTimeOfPower.RecipeRequirementsCheck.Schedule<Recipe, AspectsInContext>(RefReqs, Enactors.World.patchId);
            AtTimeOfPower.RecipeExecution.Schedule<RecipeCompletionEffectCommand, Situation>(ExecuteEffectsWithReferences, PatchType.Prefix, Enactors.World.patchId);
            AtTimeOfPower.OnPostImportRecipe.Schedule<Recipe>(FlushEffectsToEffectsOrder, PatchType.Postfix, Enactors.World.patchId);

            Machine.Patch(typeof(Beachcomber.Usurper).GetMethodInvariant("InvokeGenericImporterForAbstractRootEntity"),
                prefix: typeof(RecipeEffectsMaster).GetMethodInvariant("ConvertLegacyMutationDefinitions"),
                patchId: Enactors.World.patchId);

            Machine.Patch(typeof(Sphere).GetMethodInvariant("NotifyTokensChangedForSphere"),
                postfix: typeof(RecipeEffectsMaster).GetMethodInvariant("NotifyTokensChangedForSphere"),
                patchId: Enactors.World.patchId);

            Machine.Patch(typeof(SituationStorageSphere).GetPropertyInvariant("AllowStackMerge").GetGetMethod(),
                prefix: typeof(RecipeEffectsMaster).GetMethodInvariant("AllowStackMerge"),
                patchId: Enactors.World.patchId);
        }

        private static void NotifyTokensChangedForSphere(SecretHistories.Constants.Events.SphereContentsChangedEventArgs args)
        {
            if (args.TokenAdded != null && args.Sphere != Watchman.Get<HornedAxe>().GetDefaultSphere())
                RecipeExecutionBuffer.StackTokens(args.Sphere);
        }

        private static bool AllowStackMerge(ref bool __result)
        {
            __result = true;
            return false;
        }

        //Recipe.OnPostImportForSpecificEntity()
        private static void FlushEffectsToEffectsOrder(Recipe __instance)
        {
            List<IRecipeExecutionEffect> nativeEffects = new List<IRecipeExecutionEffect>();
            foreach (string executionProperty in orderedNativeExecutionProperties)
                if (__instance.HasCustomProperty(executionProperty))
                {
                    nativeEffects.Add(__instance.RetrieveProperty(executionProperty) as IRecipeExecutionEffect);
                    __instance.RemoveProperty(executionProperty);
                }

            List<IRecipeExecutionEffect> effectsList = __instance.RetrieveProperty<List<IRecipeExecutionEffect>>(EXECUTE);
            if (effectsList == null)
            {
                effectsList = new List<IRecipeExecutionEffect>();
                __instance.SetProperty(EXECUTE, effectsList);
            }
            effectsList.AddRange(nativeEffects);
        }

        //Usurper.InvokeGenericImporterForAbstractRootEntity()
        private static void ConvertLegacyMutationDefinitions(IEntityWithId entity, EntityData entityData)
        {
            if (entity is Recipe)
                if (entityData.ValuesTable.ContainsKey("mutations") && entityData.ValuesTable["mutations"] is ArrayList)
                {
                    ArrayList oldMutations = entityData.ValuesTable["mutations"] as ArrayList;
                    EntityData newMutations = new EntityData();

                    foreach (EntityData data in oldMutations)
                    {
                        string key = data.ValuesTable["filter"].ToString();
                        if (newMutations.ValuesTable.ContainsKey(key) == false)
                            newMutations.ValuesTable[key] = new ArrayList();

                        (newMutations.ValuesTable[key] as ArrayList).Add(data);
                        data.ValuesTable.Remove("filter");
                    }

                    entityData.ValuesTable["mutations"] = newMutations;
                }
        }

        private static void ExecuteEffectsWithReferences(RecipeCompletionEffectCommand __instance, Situation situation)
        {
            TokenContextAccessors.SetLocalSituation(situation);
            Sphere storage = situation.GetSingleSphereByCategory(SphereCategory.SituationStorage);
            List<IRecipeExecutionEffect> effectGroupsInOrder = __instance.Recipe.RetrieveProperty(EXECUTE) as List<IRecipeExecutionEffect>;
            foreach (IRecipeExecutionEffect executionEffect in effectGroupsInOrder)
            {
                executionEffect.Execute(storage, situation);
                RecipeExecutionBuffer.Execute();
            }
        }

        public static bool TryGetRefXTriggers(string elementId, out Dictionary<string, List<RefMorphDetails>> xtriggers)
        {
            xtriggers = Watchman.Get<Compendium>().GetEntityById<Element>(elementId).RetrieveProperty(REF_ELEMENT_XTRIGGERS) as Dictionary<string, List<RefMorphDetails>>;
            return xtriggers != null;
        }

        private static bool RefReqs(Recipe __instance, AspectsInContext aspectsinContext)
        {
            Dictionary<Funcine<int>, Funcine<int>> reqs = __instance.RetrieveProperty(REF_REQS) as Dictionary<Funcine<int>, Funcine<int>>;
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
                throw Birdsong.Cack("Something strange happened. Cannot identify the current situation for requirements check.");

            //Birdsong.Sing("Checking _reqs for {0}", __instance.Id);
            foreach (KeyValuePair<Funcine<int>, Funcine<int>> req in reqs)
            {
                int presentValue = req.Key.value;
                int requiredValue = req.Value.value;

                //Birdsong.Sing("'{0}': '{1}' ---> '{2}': '{3}', {4}", req.Key.formula, req.Value.formula, presentValue, requiredValue, (requiredValue <= -1 && presentValue >= -requiredValue) || (requiredValue > -1 && presentValue < requiredValue) ? "not satisfied" : "satisfied");

                if (requiredValue <= -1)
                {
                    if (presentValue >= -requiredValue)
                        return false;
                }
                else
                {
                    if (presentValue < requiredValue)
                        return false;
                }
            }

            return true;
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
    }
}