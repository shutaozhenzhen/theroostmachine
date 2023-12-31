﻿using System.Collections.Generic;
using System.Linq;

using SecretHistories.Entities;
using SecretHistories.Entities.NullEntities;
using SecretHistories.Enums;
using SecretHistories.Spheres;
using SecretHistories.UI;
using SecretHistories.Core;
using SecretHistories.Abstract;
using SecretHistories.Fucine;
using SecretHistories.States;
using SecretHistories.Commands.SituationCommands;
using SecretHistories;

using Roost.Twins;
using Roost.Twins.Entities;
using Roost.World.Recipes;

namespace Roost.World.Slots
{
    static class SlotPresenceReqsMaster
    {
        const string ASPECT_SLOTS = "aspectSlots";
        const string ASPECT_SLOT_USE_QUANTITY = "quantityMatters";

        const string SLOT_ENTRANCE_REQS = "filter";
        const string SLOT_PRESENCE_REQS = nameof(SphereSpec.IfAspectsPresent);

        const string SLOT_CONTRIBUTES_TO_PRESENCE = "addsSlots";

        internal static void Enact()
        {
            //aspects can add slots
            Machine.ClaimProperty<Element, List<SphereSpec>>(ASPECT_SLOTS);
            Machine.ClaimProperty<SphereSpec, bool>(ASPECT_SLOT_USE_QUANTITY, false, false);
            Machine.ClaimProperty<SphereSpec, bool>(SLOT_CONTRIBUTES_TO_PRESENCE, false, false);

            //slot's presence can be determined by expressions
            Machine.ClaimProperty<SphereSpec, Dictionary<FucineExp<int>, FucineExp<int>>>(SLOT_PRESENCE_REQS);

            Machine.Patch(
                original: typeof(SituationDominion).GetMethodInvariant("AddDependentSpheresForToken"),
                prefix: typeof(SlotPresenceReqsMaster).GetMethodInvariant(nameof(AddDependentSpheres)));

            Machine.Patch(
                original: typeof(SituationDominion).GetMethodInvariant("SphereIsDependent"),
                prefix: typeof(SlotPresenceReqsMaster).GetMethodInvariant(nameof(SphereIsDependent)));

            Machine.Patch(
                 original: typeof(StartingState).GetMethodInvariant(nameof(PopulateRecipeSlots)),
                 prefix: typeof(SlotPresenceReqsMaster).GetMethodInvariant(nameof(PopulateRecipeSlots)));

            LockDominionsOnThresholdFlush.Enact();
        }

        private static bool AddDependentSpheres(Sphere sphere, IManifestable ____manifestable, SituationDominion __instance, List<Sphere> ____spheres)
        {
            Situation situation = ____manifestable as Situation;

            if (situation == null)
                return false;

            List<Sphere> alreadyActiveSpheres = new List<Sphere>(____spheres);
            List<SphereSpec> spheresToCreate = situation.DetermineActiveSlots(alreadyActiveSpheres);

            foreach (SphereSpec sphereSpec in spheresToCreate)
            {
                Sphere activeSphere = __instance.TryCreateOrRetrieveSphere(sphereSpec);

                //if sphere is newly created (ie it wasn't present in dominion's spheres before)
                //then its presence is dependent on the modified sphere (and we will retire it once the owner sphere is emptied)
                if (!alreadyActiveSpheres.Contains(activeSphere))
                    activeSphere.OwnerSphereIdentifier = sphere.Id;
            }

            return false;
        }

        private static List<SphereSpec> DetermineActiveSlots(this Situation situation, List<Sphere> alreadyActiveSpheres)
        {
            Token tokenInFirstSlot = GetTokenInFirstSlot(situation);

            if (!tokenInFirstSlot.IsValid())
                return new List<SphereSpec>();

            List<SphereSpec> result = new List<SphereSpec>();
            Compendium compendium = Watchman.Get<Compendium>();

            string verbId = situation.VerbId;

            Crossroads.ResetCache();
            Crossroads.MarkLocalSituation(situation);

            foreach (SphereSpec slot in compendium.GetEntityById<Element>(tokenInFirstSlot.PayloadEntityId).Slots)
                if (slot.SuitsVerbAndSatisfiedReqs(verbId))
                    result.Add(slot);

            AspectsDictionary aspects = tokenInFirstSlot.GetAspects(false);

            foreach (Sphere sphere in alreadyActiveSpheres)
                if (sphere.GoverningSphereSpec.RetrieveProperty<bool>(SLOT_CONTRIBUTES_TO_PRESENCE))
                    aspects.ApplyMutations(sphere.GetTotalAspects(true));

            var aspectsOrdered = aspects.OrderBy(aspect => aspect.Key);

            foreach (var aspect in aspectsOrdered)
            {
                List<SphereSpec> slots = compendium.GetEntityById<Element>(aspect.Key).RetrieveProperty(ASPECT_SLOTS) as List<SphereSpec>;

                if (slots == null)
                    continue;

                foreach (SphereSpec slot in slots)
                    if (slot.SuitsVerbAndSatisfiedReqs(verbId))
                    {
                        result.Add(slot);

                        if (slot.RetrieveProperty<bool>(ASPECT_SLOT_USE_QUANTITY))
                            for (int n = 1; n < aspect.Value; n++)
                                result.Add(slot.Duplicate(n));
                    }
            }

            return result;
        }

        private static Token GetTokenInFirstSlot(Situation situation)
        {
            List<Sphere> candidateThresholds = situation.GetSpheresByCategory(SphereCategory.Threshold);
            if (candidateThresholds.Count == 0)
                return NullToken.Create();

            candidateThresholds.Reverse();
            foreach (Sphere candidateThreshold in candidateThresholds)
                if (candidateThreshold.OwnerSphereIdentifier == null)
                {
                    Token token = candidateThreshold.GetElementTokens().FirstOrDefault();

                    if (token != null)
                        return token;

                    break;
                }

            return NullToken.Create();
        }

        private static bool SphereIsDependent(bool __result)
        {
            __result = false;
            return false;
        }

        private static bool PopulateRecipeSlots(Situation situation)
        {
            List<SphereSpec> slots = new List<SphereSpec>();

            Crossroads.ResetCache();
            Crossroads.MarkLocalSituation(situation);

            foreach (SphereSpec sphere in situation.FallbackRecipe.Slots)
                if (sphere.SuitsVerbAndSatisfiedReqs(situation.VerbId))
                    slots.Add(sphere);

            if (slots.Count == 0)
            {
                ClearDominionCommand command2 = new ClearDominionCommand(SituationDominionEnum.RecipeThresholds.ToString(), SphereRetirementType.Graceful);
                situation.AddCommand(command2);
                return false;
            }

            PopulateDominionCommand command = new PopulateDominionCommand(SituationDominionEnum.RecipeThresholds.ToString(), slots);
            situation.AddCommand(command);
            return false;
        }

        private static bool SuitsVerbAndSatisfiedReqs(this SphereSpec slot, string verbId)
        {
            if (!string.IsNullOrWhiteSpace(slot.ActionId))
                if (!NoonExtensions.WildcardMatchId(verbId, slot.ActionId))
                    return false;

            Dictionary<FucineExp<int>, FucineExp<int>> presenceReqs = slot.RetrieveProperty(SLOT_PRESENCE_REQS) as Dictionary<FucineExp<int>, FucineExp<int>>;
            if (presenceReqs != null)
                if (!GrandReqsMaster.CheckGrandReqs(presenceReqs))
                    return false;

            return true;
        }

        private static SphereSpec Duplicate(this SphereSpec original, int n)
        {
            SphereSpec copy = new SphereSpec(original.SphereType, $"{original.Id}_{n}");
            foreach (CachedFucineProperty<SphereSpec> cachedFucineProperty in TypeInfoCache<SphereSpec>.GetCachedFucinePropertiesForType())
                cachedFucineProperty.SetViaFastInvoke(copy, cachedFucineProperty.GetViaFastInvoke(original));

            foreach (KeyValuePair<string, object> customProperty in original.GetCustomProperties())
                copy.SetCustomProperty(customProperty.Key, customProperty.Value);

            return copy;
        }
    }

    internal static class LockDominionsOnThresholdFlush
    {
        internal static void Enact()
        {
            Machine.Patch<FlushTokensToCategoryCommand>(
                original: nameof(FlushTokensToCategoryCommand.Execute),
                prefix: typeof(LockDominionsOnThresholdFlush).GetMethodInvariant(nameof(LockDominions)),
                postfix: typeof(LockDominionsOnThresholdFlush).GetMethodInvariant(nameof(UnlockDominions)));

            Machine.Patch<SituationDominion>(
                original: nameof(SituationDominion.OnTokensChangedForSphere),
                prefix: typeof(LockDominionsOnThresholdFlush).GetMethodInvariant(nameof(DontTouchSpheresIfLocked)));
        }

        static bool DominionsLocked = false;
        private static void LockDominions()
        {
            DominionsLocked = true;
        }

        private static void UnlockDominions()
        {
            DominionsLocked = false;
        }

        private static bool DontTouchSpheresIfLocked()
        {
            if (DominionsLocked)
                return false;

            return true;
        }

    }
}
