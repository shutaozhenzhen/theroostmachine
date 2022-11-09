using System.Collections.Generic;
using System.Linq;

using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Spheres;
using SecretHistories.Spheres.Angels;
using SecretHistories.UI;
using SecretHistories.Core;
using SecretHistories.Abstract;
using SecretHistories.Fucine;
using SecretHistories.States;
using SecretHistories.Commands.SituationCommands;
using SecretHistories.Assets.Scripts.Application.Tokens.TravelItineraries;

using Roost.Twins;
using Roost.Twins.Entities;
using Roost.World.Recipes;

namespace Roost.World.Slots
{
    static class SlotEffectsMaster
    {
        const string ASPECT_SLOTS = "aspectslots";
        const string ASPECT_SLOT_USE_QUANTITY = "quantityMatters";

        const string SLOT_ENTRANCE_REQS = "filter";
        const string SLOT_PRESENCE_REQS = "presenceReqs";

        internal static void Enact()
        {
            //aspects can add slots
            Machine.ClaimProperty<Element, List<SphereSpec>>(ASPECT_SLOTS);
            Machine.ClaimProperty<SphereSpec, bool>(ASPECT_SLOT_USE_QUANTITY, false, false);
            //slot's presence can be determined by reqs
            Machine.ClaimProperty<SphereSpec, Dictionary<FucineExp<int>, FucineExp<int>>>(SLOT_PRESENCE_REQS);
            Machine.Patch(
                original: typeof(Sphere).GetMethodInvariant(nameof(Sphere.GetChildSpheresSpecsToAddIfThisTokenAdded)),
                prefix: typeof(SlotEffectsMaster).GetMethodInvariant(nameof(GetSlotsForVerb)));

            //greedy slots grab tokens based on even chance
            Machine.Patch(
                original: typeof(GreedyAngel).GetMethodInvariant("FindStackForSlotSpecificationInSphere"),
                prefix: typeof(SlotEffectsMaster).GetMethodInvariant(nameof(TryGrabStackTrulyRandom)));

            //tokens are checked against an additional expression filter before going in the slot
            Machine.ClaimProperty<SphereSpec, FucineExp<bool>>(SLOT_ENTRANCE_REQS, false, FucineExp<bool>.UNDEFINED);
            Machine.Patch(
                original: typeof(SphereSpec).GetMethodInvariant(nameof(SphereSpec.CheckPayloadAllowedHere)),
                prefix: typeof(SlotEffectsMaster).GetMethodInvariant(nameof(SlotFilterSatisfied)));

            Machine.Patch(
                original: typeof(StartingState).GetMethodInvariant("PopulateRecipeSlots"),
                prefix: typeof(SlotEffectsMaster).GetMethodInvariant(nameof(PopulateRecipeSlots)));
        }


        private static bool PopulateRecipeSlots(Situation situation)
        {
            List<SphereSpec> slots = new List<SphereSpec>();

            Crossroads.MarkLocalSituation(situation);
            foreach (SphereSpec sphere in situation.FallbackRecipe.Slots)
                if (sphere.SuitsVerbAndSatisfiedReqs(situation.VerbId))
                    slots.Add(sphere);
            Crossroads.ResetCache();

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

        private static bool TryGrabStackTrulyRandom(SphereSpec slotSpec, Sphere sphereToSearch, ref Token __result)
        {
            List<Token> tokens = sphereToSearch.GetElementTokens();
            Crossroads.MarkAllLocalTokens(tokens);
            var candidateTokens = tokens.Where(token => token.CanBePulled() && slotSpec.CheckPayloadAllowedHere(token.Payload).MatchType == SlotMatchForAspectsType.Okay);
            Crossroads.ResetCache();

            __result = candidateTokens.SelectSingleToken();

            return false;
        }

        private static bool SlotFilterSatisfied(SphereSpec __instance, ITokenPayload payload, ref ContainerMatchForStack __result)
        {
            Token token = payload.GetToken();
            Crossroads.MarkLocalToken(token);
            FucineExp<bool> filter = __instance.RetrieveProperty<FucineExp<bool>>(SLOT_ENTRANCE_REQS);
            bool filterFailed = !filter.isUndefined && filter.value == false;
            Crossroads.ResetCache();

            if (filterFailed)
            {
                __result = new ContainerMatchForStack(new List<string>(), SlotMatchForAspectsType.InvalidToken);
                return false;
            }

            return true;
        }

        private static bool GetSlotsForVerb(Token t, string verbId, ref List<SphereSpec> __result, Sphere __instance)
        {
            __result = new List<SphereSpec>();
            Compendium compendium = Watchman.Get<Compendium>();

            Situation situation = __instance.GetContainer() as Situation;
            if (situation != null)
                Crossroads.MarkLocalSituation(situation);
            else //shouldn't happen; but just in case 
                Crossroads.MarkLocalSphere(t.Sphere);
            Crossroads.MarkLocalToken(t);

            foreach (SphereSpec slot in compendium.GetEntityById<Element>(t.PayloadEntityId).Slots)
                if (slot.SuitsVerbAndSatisfiedReqs(verbId))
                    __result.Add(slot);

            AspectsDictionary aspects = t.GetAspects(false);
            foreach (string aspectId in aspects.Keys)
            {
                List<SphereSpec> slots = compendium.GetEntityById<Element>(aspectId).RetrieveProperty(ASPECT_SLOTS) as List<SphereSpec>;

                if (slots == null)
                    continue;

                foreach (SphereSpec slot in slots)
                    if (slot.SuitsVerbAndSatisfiedReqs(verbId))
                    {
                        __result.Add(slot);

                        if (slot.RetrieveProperty<bool>(ASPECT_SLOT_USE_QUANTITY))
                            for (int n = 1; n < aspects[aspectId]; n++)
                                __result.Add(slot.Duplicate(n));
                    }
            }

            Crossroads.ResetCache();

            return false;
        }

        private static SphereSpec Duplicate(this SphereSpec original, int n)
        {
            SphereSpec copy = new SphereSpec(original.SphereType, $"{original.Id}_{n}");
            foreach (CachedFucineProperty<SphereSpec> cachedFucineProperty in TypeInfoCache<SphereSpec>.GetCachedFucinePropertiesForType())
                if (cachedFucineProperty.LowerCaseName != "id")
                    cachedFucineProperty.SetViaFastInvoke(copy, cachedFucineProperty.GetViaFastInvoke(original));
            foreach (KeyValuePair<string, object> customProperty in original.GetCustomProperties())
                copy.SetCustomProperty(customProperty.Key, customProperty.Value);

            return copy;
        }

        private static bool SuitsVerbAndSatisfiedReqs(this SphereSpec slot, string verbId)
        {
            if (!string.IsNullOrWhiteSpace(slot.ActionId))
            {
                if (slot.ActionId[slot.ActionId.Length - 1] == '*')
                {
                    string wildString = slot.ActionId.Remove(slot.ActionId.Length - 1);
                    if (!verbId.StartsWith(wildString))
                        return false;
                }
                else
                {
                    if (verbId != slot.ActionId)
                        return false;
                }
            }

            Dictionary<FucineExp<int>, FucineExp<int>> presenceReqs = slot.RetrieveProperty(SLOT_PRESENCE_REQS) as Dictionary<FucineExp<int>, FucineExp<int>>;
            if (presenceReqs != null)
                return RecipeEffectsMaster.CheckGrandReqs(presenceReqs);

            return true;
        }
    }
}
