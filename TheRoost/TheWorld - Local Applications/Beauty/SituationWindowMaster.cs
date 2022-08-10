using System;
using System.Collections.Generic;

using Assets.Scripts.Application.UI.Situation;
using SecretHistories.UI;
using SecretHistories.Manifestations;
using SecretHistories.Entities;
using SecretHistories.Abstract;
using SecretHistories.Constants.Events;
using SecretHistories.Assets.Scripts.Application.Entities;
using SecretHistories.Assets.Scripts.Application.Infrastructure.Events;
using SecretHistories.Spheres;
using SecretHistories.Services;
using SecretHistories.Core;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Roost.World.Recipes
{
    internal static class SituationWindowMaster
    {
        internal static void Enact()
        {
            Machine.Patch(
                original: typeof(CardManifestation).GetMethodInvariant("Initialise"),
                postfix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(CardWithAspectIconOnTheTable)));

            Machine.Patch(
                original: typeof(TextRefiner).GetMethodInvariant(nameof(TextRefiner.RefineString)),
                prefix: typeof(SituationWindowMaster).GetMethodInvariant(nameof(OverrideRecipeRefinement)));
        }

        //allow aspects to appear as a normal cards 
        private static void CardWithAspectIconOnTheTable(IManifestable manifestable, Image ___artwork)
        {
            if (Machine.GetEntity<Element>(manifestable.EntityId).IsAspect)
                ___artwork.sprite = ResourcesManager.GetSpriteForAspect(manifestable.Icon);
        }

        private static bool OverrideRecipeRefinement(string stringToRefine, AspectsDictionary ____aspectsInContext, ref string __result)
        {
            __result = Elegiast.Scribe.RefineString(stringToRefine, ____aspectsInContext);
            return false;
        }
    }
}
