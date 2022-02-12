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
    public static class RecipeEffectsExtension
    {
        const string REF_REQS = "grandreqs";
        const string ORDERED_EFFECTS = "furthermore";
        const string REF_ELEMENT_XTRIGGERS = "xtriggers";

        private static bool propertiesClaimed = false;

        internal static void Enact()
        {
            //in case player disables/enables the module several times, so it won't clog the log with "already claimed" messages
            if (propertiesClaimed == false)
            {
                //so, the actual execution of all new effects happens via the ORDERED_EFFECTS list that contains GrandEffects entities
                //so, technically, I don't need to claim anything else from these:
                Machine.ClaimProperty<Recipe, Dictionary<Funcine<int>, Funcine<int>>>(REF_REQS);
                Machine.ClaimProperty<Recipe, List<GrandEffects>>(ORDERED_EFFECTS);

                //but I don't want to force the user to wrap everything inside the list, so first batch of effects can be defined in recipe's main corebody
                //for that end I claim every property of GrandEffects for the recipe, so they are loaded correctly;
                //later on, after the loading is completed, I combine these properties into another GrandEffect
                //then I insert it into the ORDERED_LIST, so it's executed first
                Dictionary<string, Type> allGrandEffectsProperties = new Dictionary<string, Type>();
                foreach (CachedFucineProperty<GrandEffects> cachedProperty in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
                    allGrandEffectsProperties.Add(cachedProperty.LowerCaseName, cachedProperty.ThisPropInfo.PropertyType);
                Machine.ClaimProperties<Recipe>(allGrandEffectsProperties);

                Machine.ClaimProperty<Element, Dictionary<string, List<RefMorphDetails>>>(REF_ELEMENT_XTRIGGERS);

                propertiesClaimed = true;
            }

            AtTimeOfPower.RecipeRequirementsCheck.Schedule<Recipe, AspectsInContext, bool>(RefReqs, Enactors.World.patchId);
            AtTimeOfPower.RecipeExecution.Schedule<RecipeCompletionEffectCommand, Situation, bool>(ExecuteEffectsWithReferences, PatchType.Prefix, Enactors.World.patchId);
            AtTimeOfPower.OnPostImportRecipe.Schedule<Recipe>(FlushEffectsToEffectsOrder, PatchType.Postfix, Enactors.World.patchId);

            Machine.Patch(typeof(Beachcomber.Usurper).GetMethodInvariant("InvokeGenericImporterForAbstractRootEntity"),
                prefix: typeof(RecipeEffectsExtension).GetMethodInvariant("ConvertLegacyMutationDefinitions"), 
                patchId: Enactors.World.patchId);

            Machine.Patch(typeof(Sphere).GetMethodInvariant("NotifyTokensChangedForSphere"),
                postfix: typeof(RecipeEffectsExtension).GetMethodInvariant("NotifyTokensChangedForSphere"),
                patchId: Enactors.World.patchId);

            Machine.Patch(typeof(SituationStorageSphere).GetPropertyInvariant("AllowStackMerge").GetGetMethod(),
                prefix: typeof(RecipeEffectsExtension).GetMethodInvariant("AllowStackMerge"),
                patchId: Enactors.World.patchId);
        }

        private static void NotifyTokensChangedForSphere(SecretHistories.Constants.Events.SphereContentsChangedEventArgs args)
        {
            if (args.TokenAdded != null)
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
            List<GrandEffects> effectsList = __instance.RetrieveProperty<List<GrandEffects>>(ORDERED_EFFECTS);
            if (effectsList == null)
            {
                effectsList = new List<GrandEffects>();
                __instance.SetProperty(ORDERED_EFFECTS, effectsList);
            }

            GrandEffects firstEffectsBatch = new GrandEffects();
            bool atLeastOneExtendedEffect = false;
            foreach (CachedFucineProperty<GrandEffects> cachedProperty in TypeInfoCache<GrandEffects>.GetCachedFucinePropertiesForType())
                if (__instance.HasCustomProperty(cachedProperty.LowerCaseName))
                {
                    atLeastOneExtendedEffect = true;
                    cachedProperty.SetViaFastInvoke(firstEffectsBatch, __instance.RetrieveProperty(cachedProperty.LowerCaseName));
                }

            if (atLeastOneExtendedEffect)
                effectsList.Insert(0, firstEffectsBatch);
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

        private static void ExecuteEffectsWithReferences(RecipeCompletionEffectCommand __instance, Situation situation, bool __result)
        {
            TokenContextAccessors.SetLocalSituation(situation);
            Sphere storage = situation.GetSingleSphereByCategory(SphereCategory.SituationStorage);

            List<GrandEffects> effectGroupsInOrder = __instance.Recipe.RetrieveProperty(ORDERED_EFFECTS) as List<GrandEffects>;
            foreach (GrandEffects effectsGroup in effectGroupsInOrder)
            {
                effectsGroup.Run(situation, storage);
                RecipeExecutionBuffer.Execute();
            }
        }

        public static bool TryGetRefXTriggers(string elementId, out Dictionary<string, List<RefMorphDetails>> xtriggers)
        {
            xtriggers = Watchman.Get<Compendium>().GetEntityById<Element>(elementId).RetrieveProperty(REF_ELEMENT_XTRIGGERS) as Dictionary<string, List<RefMorphDetails>>;
            return xtriggers != null;
        }

        private static bool RefReqs(Recipe __instance, AspectsInContext aspectsinContext, bool __result)
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

            bool result = true;

            //Birdsong.Sing("Checking _reqs for {0}", __instance.Id);
            foreach (KeyValuePair<Funcine<int>, Funcine<int>> req in reqs)
            {
                int presentValue = req.Key.value;
                int requiredValue = req.Value.value;

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
    }
}