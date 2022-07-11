using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

namespace Roost.World
{
    public static class Scribe
    {
        private static readonly Dictionary<string, string> _textLevers = new Dictionary<string, string>();
        private static readonly Func<object, object> _currentLevers = typeof(Character).GetFieldInvariant("_previousCharacterHistoryRecords").GetValue;
        private static readonly Func<object, object> _futureLevers = typeof(Character).GetFieldInvariant("_inProgressHistoryRecords").GetValue;

        private const string SET_LEVERS_CURRENT = "setLeversCurrent";
        private const string SET_LEVERS_FUTURE = "setLeversFuture";
        internal static void Enact()
        {
            Machine.ClaimProperty<Recipe, Dictionary<string, string>>(SET_LEVERS_CURRENT);
            Machine.ClaimProperty<Recipe, Dictionary<string, string>>(SET_LEVERS_FUTURE);

            AtTimeOfPower.RecipeExecution.Schedule<RecipeCompletionEffectCommand, Situation>(RecipeEffectLevers, PatchType.Postfix);
            AtTimeOfPower.CompendiumLoad.Schedule(ResetTextLevers, PatchType.Prefix);
        }

        internal static void RecipeEffectLevers(RecipeCompletionEffectCommand __instance, Situation situation)
        {
            AspectsDictionary aspects = situation.GetSingleSphereByCategory(SphereCategory.SituationStorage)?.GetTotalAspects(true);

            Dictionary<string, string> setLeversCurrent = __instance.Recipe.RetrieveProperty(SET_LEVERS_CURRENT) as Dictionary<string, string>;
            if (setLeversCurrent != null)
                foreach (string lever in setLeversCurrent.Keys)
                    if (lever == "")
                        RemoveLeverForCurrentPlaythrough(lever);
                    else
                    {
                        string refinedString = RefineString(setLeversCurrent[lever], aspects);
                        SetLeverForCurrentPlaythrough(lever, refinedString);
                    }

            Dictionary<string, string> setLeversFuture = __instance.Recipe.RetrieveProperty(SET_LEVERS_CURRENT) as Dictionary<string, string>;
            if (setLeversFuture != null)
                foreach (string lever in setLeversFuture.Keys)
                    if (lever == "")
                        RemoveLeverForNextPlaythrough(lever);
                    else
                    {
                        string refinedString = RefineString(setLeversFuture[lever], aspects);
                        SetLeverForNextPlaythrough(lever, refinedString);
                    }
        }

        internal static void SetLever(Dictionary<string, string> levers, string lever, string value)
        {
            levers[lever] = value;
        }

        internal static void RemoveLever(Dictionary<string, string> levers, string lever)
        {
            levers.Remove(lever);
        }

        internal static void ClearLevers(Dictionary<string, string> levers)
        {
            levers.Clear();
        }

        internal static void SetLeverForCurrentPlaythrough(string lever, string value)
        {
            SetLever(_currentLevers(Watchman.Get<Stable>().Protag()) as Dictionary<string, string>, lever, value);
        }

        internal static void SetLeverForNextPlaythrough(string lever, string value)
        {
            SetLever(_futureLevers(Watchman.Get<Stable>().Protag()) as Dictionary<string, string>, lever, value);
        }

        internal static string GetLeverForCurrentPlaythrough(string lever)
        {
            return Watchman.Get<Stable>().Protag().GetPastLegacyEventRecord(lever);
        }

        internal static string GetLeverForNextPlaythrough(string lever)
        {
            return Watchman.Get<Stable>().Protag().GetFutureLegacyEventRecord(lever);
        }

        internal static void RemoveLeverForCurrentPlaythrough(string lever)
        {
            RemoveLever(_currentLevers(Watchman.Get<Stable>().Protag()) as Dictionary<string, string>, lever);
        }

        internal static void RemoveLeverForNextPlaythrough(string lever)
        {
            RemoveLever(_futureLevers(Watchman.Get<Stable>().Protag()) as Dictionary<string, string>, lever);
        }

        internal static void ClearLeversForCurrentPlaythrough()
        {
            ClearLevers(_currentLevers(Watchman.Get<Stable>().Protag()) as Dictionary<string, string>);
        }

        internal static void ClearLeversForNextPlaythrough()
        {
            ClearLevers(_futureLevers(Watchman.Get<Stable>().Protag()) as Dictionary<string, string>);
        }

        internal static Dictionary<string, string> GetLeversForCurrentPlaythrough()
        {
            return Watchman.Get<Stable>().Protag().PreviousCharacterHistoryRecords;
        }

        internal static Dictionary<string, string> GetLeversForNextPlaythrough()
        {
            return Watchman.Get<Stable>().Protag().InProgressHistoryRecords;
        }

        internal static void AddTextLever(string lever, string value)
        {
            Birdsong.Sing(lever, value);
            _textLevers[lever] = value;
        }

        internal static void ResetTextLevers()
        {
            _textLevers.Clear();
        }

        public static string RefineString(string str, AspectsDictionary aspects)
        {
            if (string.IsNullOrWhiteSpace(str))
                return str;

            foreach (string lever in _textLevers.Keys)
            {
                string leverdata = GetLeverForCurrentPlaythrough(lever) ?? _textLevers[lever];
                str = str.Replace(lever, leverdata);
            }

            if (str.Contains("@") == false)
                return str;

            string[] parts = str.Split('@');

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

            return result.Trim();
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

namespace Roost.World.Entities
{
    [FucineImportable("levers")]
    public class LeverData : AbstractEntity<LeverData>
    {
        [FucineDict] public Dictionary<string, string> textLevers { get; set; }
        //finally, your entity needs to implement two methods of AbstractEntity<T> - constructor and OnPostImportForSpecificEntity()
        //both of them can remain empty but the second one is sometimes useful - it's called right after all entities are imported
        public LeverData(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            Birdsong.Sing(textLevers);
            foreach (KeyValuePair<string, string> textLever in textLevers)
                Scribe.AddTextLever(textLever.Key, textLever.Value);
        }
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

        public static void SetLeverForNextPlaythrough(string lever, string value)
        {
            Roost.World.Scribe.SetLeverForNextPlaythrough(lever, value);
        }

        public static string GetLeverForCurrentPlaythrough(string lever)
        {
            return Roost.World.Scribe.GetLeverForCurrentPlaythrough(lever);
        }

        public static string GetLeverForNextPlaythrough(string lever)
        {
            return Roost.World.Scribe.GetLeverForNextPlaythrough(lever);
        }

        public static void RemoveLeverForCurrentPlaythrough(string lever)
        {
            Roost.World.Scribe.RemoveLeverForCurrentPlaythrough(lever);
        }

        public static void RemoveLeverForNextPlaythrough(string lever)
        {
            Roost.World.Scribe.RemoveLeverForNextPlaythrough(lever);
        }

        public static Dictionary<string, string> GetLeversForCurrentPlaythrough()
        {
            return World.Scribe.GetLeversForCurrentPlaythrough();
        }

        public static Dictionary<string, string> GetLeversForNextPlaythrough()
        {
            return World.Scribe.GetLeversForCurrentPlaythrough();
        }

        public static void ClearLeversForCurrentPlaythrough()
        {
            World.Scribe.ClearLeversForCurrentPlaythrough();
        }

        public static void ClearLeversForNextPlaythrough()
        {
            World.Scribe.ClearLeversForNextPlaythrough();
        }
    }
}