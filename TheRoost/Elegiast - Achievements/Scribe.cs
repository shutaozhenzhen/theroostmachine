using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

namespace Roost.Elegiast
{
    public static class Scribe
    {

        private static readonly HashSet<string> _textLevers = new HashSet<string>();
        private static readonly Dictionary<string, string> _defaultValues = new Dictionary<string, string>();
        internal static void AddTextLever(string lever)
        {
            _textLevers.Add(lever);
            if (_defaultValues.ContainsKey(lever) == false)
                _defaultValues[lever] = lever;
        }

        internal static void AddLeverDefaultValue(string lever, string value)
        {
            _defaultValues.Remove(lever);
            _defaultValues.Add(lever, value);
        }

        internal static void ResetRegisteredLevers()
        {
            _defaultValues.Clear();
            _textLevers.Clear();
        }

        private static readonly Func<object, object> _currentLevers = typeof(Character).GetFieldInvariant("_previousCharacterHistoryRecords").GetValue;
        private static readonly Func<object, object> _futureLevers = typeof(Character).GetFieldInvariant("_inProgressHistoryRecords").GetValue;

        private static void SetLever(Dictionary<string, string> levers, string lever, string value)
        {
            levers[lever] = value;
        }

        private static string GetLever(Dictionary<string, string> levers, string lever)
        {
            if (levers.TryGetValue(lever, out string result) == false)
                _defaultValues.TryGetValue(lever, out result);

            return result;
        }

        private static void RemoveLever(Dictionary<string, string> levers, string lever)
        {
            levers.Remove(lever);
        }

        private static void ClearLevers(Dictionary<string, string> levers)
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
            return GetLever(_currentLevers(Watchman.Get<Stable>().Protag()) as Dictionary<string, string>, lever);
        }

        internal static string GetLeverForNextPlaythrough(string lever)
        {
            return GetLever(_futureLevers(Watchman.Get<Stable>().Protag()) as Dictionary<string, string>, lever);
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

        public static string RefineString(string str, AspectsDictionary aspects)
        {
            if (string.IsNullOrWhiteSpace(str))
                return str;

            foreach (string lever in _textLevers)
            {
                string leverdata = GetLeverForCurrentPlaythrough(lever);
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
                {
                    result += specialEffects[refinementText](refinementAspect, aspects);
                    return true;
                }

                if (refinementText.Contains(":"))
                {
                    string[] refWithArgs = refinementText.Split(':');
                    if (specialEffectsWithArgs.ContainsKey(refWithArgs[0]))
                    {
                        result += specialEffectsWithArgs[refWithArgs[0]](refWithArgs, aspects, refinementAspect);
                        return true;
                    }
                }

                result += refinementText;

                return true;
            }

            return false;
        }

        private static readonly Dictionary<string, Func<string, AspectsDictionary, string>> specialEffects =
            new Dictionary<string, Func<string, AspectsDictionary, string>>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "$id", (aspectId, aspects) => aspectId },
            { "$label", (aspectId, aspects) => Watchman.Get<Compendium>().GetEntityById<Element>(aspectId).Label },
            { "$description", (aspectId, aspects) => Watchman.Get<Compendium>().GetEntityById<Element>(aspectId).Description },
            { "$icon", (aspectId, aspects) => Watchman.Get<Compendium>().GetEntityById<Element>(aspectId).Icon },
            { "$sprite", (aspectId, aspects) => "<sprite name =" + Watchman.Get<Compendium>().GetEntityById<Element>(aspectId).Icon + ">" },
            { "$value", (aspectId, aspects) => aspects.AspectValue(aspectId).ToString() },

        };
        private static readonly Dictionary<string, Func<string[], AspectsDictionary, string, string>> specialEffectsWithArgs =
            new Dictionary<string, Func<string[], AspectsDictionary, string, string>>(StringComparer.InvariantCultureIgnoreCase)
        {
                //args[0] is the name of the effect 
            { "$labelOf", (args, aspects, fromRefinementAspect) => Watchman.Get<Compendium>().GetEntityById<Element>(args[1]).Label },
            { "$descriptionOf", (args, aspects, fromRefinementAspect) => Watchman.Get<Compendium>().GetEntityById<Element>(args[1]).Description },
            { "$iconOf", (args, aspects, fromRefinementAspect) => Watchman.Get<Compendium>().GetEntityById<Element>(args[1]).Icon },
            { "$sprite", (args, aspects, fromRefinementAspect)=> "<sprite name =" + Watchman.Get<Compendium>().GetEntityById<Element>(args[1]).Icon + ">" },
            { "$valueOf", (args, aspects, fromRefinementAspect) => aspects.AspectValue(args[1]).ToString() },
        };
    }
}

namespace Roost
{
    public static partial class Machine
    {
        public static void SetLeverForCurrentPlaythrough(string lever, string value)
        {
            Roost.Elegiast.Scribe.SetLeverForCurrentPlaythrough(lever, value);
        }

        public static void SetLeverForNextPlaythrough(string lever, string value)
        {
            Roost.Elegiast.Scribe.SetLeverForNextPlaythrough(lever, value);
        }

        public static string GetLeverForCurrentPlaythrough(string lever)
        {
            return Roost.Elegiast.Scribe.GetLeverForCurrentPlaythrough(lever);
        }

        public static string GetLeverForNextPlaythrough(string lever)
        {
            return Roost.Elegiast.Scribe.GetLeverForNextPlaythrough(lever);
        }

        public static void RemoveLeverForCurrentPlaythrough(string lever)
        {
            Roost.Elegiast.Scribe.RemoveLeverForCurrentPlaythrough(lever);
        }

        public static void RemoveLeverForNextPlaythrough(string lever)
        {
            Roost.Elegiast.Scribe.RemoveLeverForNextPlaythrough(lever);
        }

        public static Dictionary<string, string> GetLeversForCurrentPlaythrough()
        {
            return Roost.Elegiast.Scribe.GetLeversForCurrentPlaythrough();
        }

        public static Dictionary<string, string> GetLeversForNextPlaythrough()
        {
            return Roost.Elegiast.Scribe.GetLeversForCurrentPlaythrough();
        }

        public static void ClearLeversForCurrentPlaythrough()
        {
            Roost.Elegiast.Scribe.ClearLeversForCurrentPlaythrough();
        }

        public static void ClearLeversForNextPlaythrough()
        {
            Roost.Elegiast.Scribe.ClearLeversForNextPlaythrough();
        }
    }
}