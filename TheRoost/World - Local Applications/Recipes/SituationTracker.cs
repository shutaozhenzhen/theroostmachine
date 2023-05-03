using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using SecretHistories.UI;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Entities.NullEntities;
using SecretHistories.States;


namespace Roost.World.Recipes
{
    static class SituationTracker
    {
        private static List<Situation> currentSituations = new List<Situation>() { NullSituation.Create() };
        public static Situation currentSituation => currentSituations[currentSituations.Count - 1];

        internal static void Enact()
        {
            Machine.Patch(
                original: Machine.GetMethod<RequiresExecutionState>(nameof(SituationState.Enter)),
                prefix: typeof(SituationTracker).GetMethodInvariant(nameof(PushCurrentSituation)));

            Machine.Patch(
                 original: Machine.GetMethod<RequiresExecutionState>(nameof(SituationState.Exit)),
                 prefix: typeof(SituationTracker).GetMethodInvariant(nameof(PopCurrentSituation)));
        }

        private static void PushCurrentSituation(Situation situation)
        {
            currentSituations.Add(situation);
        }

        private static void PopCurrentSituation(Situation situation)
        {
            if (currentSituations[currentSituations.Count - 1] != situation)
                Birdsong.TweetLoud("uh-uh something very secretly unexpected happened contact the authorities");

            currentSituations.RemoveAt(currentSituations.Count - 1);
        }

    }
}
