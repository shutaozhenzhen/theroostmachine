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
                original: typeof(TokenDetailsWindow).GetMethodInvariant("SetElementCard"),
                prefix: typeof(Scribe).GetMethodInvariant(nameof(StoreElementAspects)));

            //not refining element label since it's used in many contexts without the clear access to the stack's mutations
            Machine.Patch(
                original: typeof(AbstractDetailsWindow).GetMethodInvariant("ShowText"),
                prefix: typeof(Scribe).GetMethodInvariant(nameof(RefineElementTexts)));
        }

        internal static void SetLeverForCurrentPlaythrough(string lever, string value)
        {
            Watchman.Get<Stable>().Protag().SetOrOverwritePastLegacyEventRecord(lever, value);
        }

        internal static void SetLeverForNextPlaythrough(string lever, string value)
        {
            Watchman.Get<Stable>().Protag().SetOrOverwritePastLegacyEventRecord(lever, value);
        }

        internal static string GetLeverForCurrentPlaythrough(string lever)
        {
            return Watchman.Get<Stable>().Protag().GetPastLegacyEventRecord(lever);
        }

        internal static string GetLeverForNextPlaythrough(string lever)
        {
            return Watchman.Get<Stable>().Protag().GetFutureLegacyEventRecord(lever);
        }

        private static readonly List<string> textLevers = new List<string>();
        internal static void MarkTextLever(string levers)
        {
            textLevers.Add(levers);
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

            if ((parts.Length - 1) % 2 == 1)
                return str + "[Looks like there's a refinement with no @ terminator here]";

            //part before any refinements are applied
            string result = parts[0];

            foreach (string lever in textLevers)
                result = result.Replace(lever, GetLeverForCurrentPlaythrough(lever));

            for (int n = 1; n < parts.Length; n += 2)
            {
                string[] refinements = parts[n].Split('#');
                for (int i = 1; i < refinements.Length; i++)
                    if (TryAddRefinement(ref result, refinements[i], aspects))
                        break;

                //and non-refined parts in-between
                result += parts[n + 1];
            }

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

            if (string.IsNullOrWhiteSpace(refinementAspect) || aspects.AspectValue(refinementAspect) >= refinementAmount)
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
            { "$id", (aspectId, aspects) => aspectId },
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
        public static void SetLeverForCurrentPlaythrough(string lever, string value)
        {
            Roost.World.Scribe.SetLeverForCurrentPlaythrough(lever, value);
        }

        public static string GetLeverForCurrentPlaythrough(string lever)
        {
            return Roost.World.Scribe.GetLeverForCurrentPlaythrough(lever);
        }

        public static void SetLeverForNextPlaythrough(string lever, string value)
        {
            Roost.World.Scribe.SetLeverForNextPlaythrough(lever, value);
        }

        public static string GetLeverForNextPlaythrough(string lever)
        {
            return Roost.World.Scribe.GetLeverForNextPlaythrough(lever);
        }

        public static void MarkTextLever(string lever)
        {
            Roost.World.Scribe.MarkTextLever(lever);
        }
    }
}