using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Services;

namespace Roost.World
{
    static class Scribe
    {
        internal static void Enact()
        {
            Machine.Patch(
                original: typeof(TextRefiner).GetMethodInvariant(nameof(TextRefiner.RefineString)),
                prefix: typeof(Scribe).GetMethodInvariant(nameof(OverrideRecipeRefinement)));

            Machine.Patch(
                original: typeof(TokenDetailsWindow).GetMethodInvariant("SetElementCard"),
                prefix: typeof(Scribe).GetMethodInvariant(nameof(StoreElementAspects)));

            Machine.Patch(
                original: typeof(AbstractDetailsWindow).GetMethodInvariant("ShowText"),
                prefix: typeof(Scribe).GetMethodInvariant(nameof(RefineElementTexts)));


        }

        internal static void SetLeverPast(string lever, string value)
        {
            Watchman.Get<Stable>().Protag().SetOrOverwritePastLegacyEventRecord(lever, value);
        }

        internal static void SetLeverFuture(string lever, string value)
        {
            Watchman.Get<Stable>().Protag().SetOrOverwritePastLegacyEventRecord(lever, value);
        }

        internal static string GetLeverPast(string lever)
        {
            return Watchman.Get<Stable>().Protag().GetPastLegacyEventRecord(lever);
        }

        internal static string GetLeverFuture(string lever)
        {
            return Watchman.Get<Stable>().Protag().GetFutureLegacyEventRecord(lever);
        }

        private static readonly List<string> textLevers = new List<string>();
        internal static void MarkTextLever(string levers)
        {
            textLevers.Add(levers);
        }

        private static readonly List<string> permanentLevers = new List<string>();
        internal static void MarkPermanentLever(string levers)
        {
            permanentLevers.Add(levers);
        }

        private static bool OverrideRecipeRefinement(string stringToRefine, AspectsDictionary ____aspectsInContext, ref string __result)
        {
            __result = RefineString(stringToRefine, ____aspectsInContext);
            return false;
        }

        private static void StoreElementAspects(ElementStack stack, Element element)
        {
            currentAspects.Clear();
            currentAspects.CombineAspects(element.Aspects);
            currentAspects.ApplyMutations(stack.Mutations);
        }

        static readonly AspectsDictionary currentAspects = new AspectsDictionary();
        private static void RefineElementTexts(ref string desc, AbstractDetailsWindow __instance)
        {
            if (!(__instance is TokenDetailsWindow))
                return;

            desc = RefineString(desc, currentAspects);
        }

        private static string RefineString(string str, AspectsDictionary aspects)
        {
            string[] parts = str.Split('@');
            Birdsong.Sing(parts.Length);
            if ((parts.Length - 1) % 2 == 1)
                return str + "[Looks like there's a refinement with no @ terminator here]";

            //part before any refinements are applied
            string result = parts[0];

            for (int n = 1; n < parts.Length; n += 2)
            {
                string[] refinements = parts[n].Split('#');
                for (int i = 1; i < refinements.Length; i++)
                    if (TryAddRefinement(ref result, refinements[i], aspects))
                        break;

                //and non-refined parts in-between
                result += parts[n + 1];
            }

            foreach (string lever in textLevers)
                result = result.Replace(lever, GetLeverPast(lever));

            return result;
        }

        private static bool TryAddRefinement(ref string result, string refinement, AspectsDictionary aspects)
        {
            string[] arguments = refinement.Split('|');
            string refinementAspect = arguments[0].Trim().ToLower();
            int refinementAmount; string refinementText;

            if (arguments.Length == 2)
            {
                refinementAmount = 1;
                refinementText = arguments[1];
            }
            else if (arguments.Length == 3)
            {
                if (int.TryParse(arguments[1], out refinementAmount) == false)
                {
                    result += $"[Incorrect value for refinement {refinementAspect}";
                    return true;
                }

                refinementText = arguments[2];
            }
            else
            {
                result += "[Incorrect amount of arguments in refinement]";
                return true;
            }

            if (aspects.AspectValue(refinementAspect) >= refinementAmount)
            {
                if (specialEffects.ContainsKey(refinementText))
                    result += specialEffects[refinementText](refinementAspect, aspects);
                else
                    result += refinementText;

                return true;
            }

            return false;
        }

        private static readonly Dictionary<string, Func<string, AspectsDictionary, string>> specialEffects = new Dictionary<string, Func<string, AspectsDictionary, string>>()
        {
            { "$label", (aspectId, aspects) => Watchman.Get<Compendium>().GetEntityById<Element>(aspectId).Label },
            { "$value", (aspectId, aspects) => aspects.AspectValue(aspectId).ToString() },
            { "$icon", (aspectId, aspects) => "<sprite name=" + aspectId + ">"},
        };
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static void SetLeverPast(string lever, string value)
        {
            Roost.World.Scribe.SetLeverPast(lever, value);
        }

        public static string GetLeverPast(string lever)
        {
            return Roost.World.Scribe.GetLeverPast(lever);
        }

        public static void SetLeverFuture(string lever, string value)
        {
            Roost.World.Scribe.SetLeverFuture(lever, value);
        }

        public static string GetLeverFuture(string lever)
        {
            return Roost.World.Scribe.GetLeverFuture(lever);
        }

        public static void MarkTextLever(string lever)
        {
            Roost.World.Scribe.MarkTextLever(lever);
        }

        public static void MarkPermanentLever(string lever)
        {
            Roost.World.Scribe.MarkPermanentLever(lever);
        }
    }
}