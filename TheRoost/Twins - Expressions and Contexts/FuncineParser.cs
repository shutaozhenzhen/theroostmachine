using System;
using System.Collections.Generic;
using System.Linq;

using TheRoost.Twins.Entities;

namespace TheRoost.Twins
{
    public static class FuncineParser
    {
        public static readonly char[] referenceOpening = new char[] { '[', '{', };
        public static readonly char[] referenceClosing = new char[] { ']', '}', };
        public static readonly char[] scopeSeparators = new char[] { '\\', '/' };

        public static List<FucineRef> LoadReferences(ref string expression)
        {
            List<FucineRef> references = new List<FucineRef>();

            expression.Trim();

            //NCalc parses stuff like "-1" as booleans, apparently, because it understands minus sign as logical negation???!!!???!11??!! i am at loss of words
            if (expression[0] == '-')
                expression = expression.Insert(0, "0");

            if (expression.IndexOfAny(referenceOpening) == -1 && char.IsDigit(expression[0]) == false)
                expression = string.Concat(referenceOpening[0], expression, referenceClosing[0]);

            int openingPosition, closingPosition;
            string referenceData = GetBordersOfSeparatedArea(expression, out openingPosition, out closingPosition);
            while (openingPosition > -1)
            {
                string referenceId = GenerateUniqueReferenceId(references.Count);
                FucineRef reference = LoadReference(referenceData, referenceId);

                bool referenceIsUnique = true;
                foreach (FucineRef olderReference in references)
                    if (reference.Equals(olderReference))
                    {
                        referenceIsUnique = false;
                        referenceId = olderReference.idInExpression;
                        break;
                    }

                if (referenceIsUnique)
                    references.Add(reference);

                expression = expression.Remove(openingPosition, closingPosition - openingPosition + 1).Insert(openingPosition, referenceId);
                referenceData = GetBordersOfSeparatedArea(expression, out openingPosition, out closingPosition);
            }

            return references;
        }

        public static FucineRef LoadReference(string referenceData, string referenceId)
        {
            string elementId = GetLastPathPart(ref referenceData);

            Funcine<bool> filter = default(Funcine<bool>);
            if (referenceData.Length > 0 && referenceClosing.Contains(referenceData[referenceData.Length - 1]))
            {
                int filterOpening, filterClosing;
                string filterData = GetBordersOfSeparatedArea(referenceData, out filterOpening, out filterClosing);

                filter = new Funcine<bool>(filterData);
                referenceData = referenceData.Remove(filterOpening);
            }

            string targetPath = InterpretReferencePath(ref referenceData);
            return new FucineRef(referenceId, elementId, targetPath, filter);
        }

        private static string GetBordersOfSeparatedArea(string expression, out int openingPosition, out int closingPosition)
        {
            openingPosition = expression.IndexOfAny(referenceOpening);
            closingPosition = expression.IndexOfAny(referenceClosing);

            if (openingPosition == -1)
                return expression;

            if (closingPosition == -1)
                throw Birdsong.Caw("Reference in {0} is not closed", expression);

            string referenceData = expression.Substring(openingPosition + 1, closingPosition - openingPosition - 1);
            int innerOpeningsCount = referenceData.Split(referenceOpening).Length - 1;
            int openingsAccounted = 0;
            while (innerOpeningsCount > openingsAccounted)
            {
                for (; openingsAccounted < innerOpeningsCount; openingsAccounted++)
                {
                    closingPosition = expression.IndexOfAny(referenceClosing, closingPosition + 1);
                    if (closingPosition == -1)
                        throw Birdsong.Caw("Unclosed reference in {0}", expression);
                }

                referenceData = expression.Substring(openingPosition + 1, closingPosition - openingPosition - 1);
                innerOpeningsCount = referenceData.Split(referenceOpening).Length - 1;
            }

            return referenceData;
        }

        public static string InterpretReferencePath(ref string path)
        {
            if (path.StartsWith("~"))
            {
                foreach (char separator in scopeSeparators)
                    path = path.Replace(separator, '/');

                return path;
            }

            if (path.Length > 0 && scopeSeparators.Contains(path[0]))
                path = path.Substring(1);

            path = path.ToLower();
            string sphereArea = GetNextPathPart(ref path);

            switch (sphereArea)
            {
                case "verb":
                case "verbs": return InterpretVerbReferencePath(ref path);
                case "deck":
                case "decks": return InterpretDeckReferencePath(ref path);
                case "table":
                case "tabletop": return "~/tabletop";
                case "extant": return TokenContextManager.EXTANT_PATH;
                case "token": return TokenContextManager.LOCAL_TOKEN_PATH;
                case "situation": return TokenContextManager.LOCAL_SITUATION_PATH;
                case "": return TokenContextManager.LOCAL_SPHERE_PATH;
                default:
                    Birdsong.Sing("Unknown sphere area {0}", sphereArea);
                    return "~/" + sphereArea;
            }
        }

        private static string InterpretVerbReferencePath(ref string path)
        {
            string verbId = GetNextPathPart(ref path);
            string sphere = GetNextPathPart(ref path);

            switch (sphere)
            {
                case "storage": return TokenContextManager.StoragePathForVerb(verbId);
                case "slots": return TokenContextManager.SlotsPathForVerb(verbId);
                case "slot":
                    string slotId = GetNextPathPart(ref path);
                    return TokenContextManager.SingleSlotPathForVerb(verbId, slotId);
                case "":
                case "all": return TokenContextManager.PathForVerb(verbId);
                default:
                    Birdsong.Sing("Unknown verb sphere {0}", sphere);
                    return TokenContextManager.PathForVerb(verbId) + "/unknown";
            }
        }

        private static string InterpretDeckReferencePath(ref string path)
        {
            string entityId = GetNextPathPart(ref path);
            string sphere = GetNextPathPart(ref path);

            string fucinePath = "~/" + entityId;

            switch (sphere)
            {
                case "forbidden": return fucinePath + "_forbidedn";
                case "draw":
                case "": return fucinePath + "_draw";
                default:
                    Birdsong.Sing("Unknown deck sphere {0}", sphere);
                    return fucinePath + "/unknown";
            }
        }

        private static string GetNextPathPart(ref string path)
        {
            path = path.Trim();
            if (path == string.Empty)
                return string.Empty;

            if (scopeSeparators.Contains(path[0]))
                path = path.Substring(1);

            string result;
            int splitPoint = path.IndexOfAny(scopeSeparators);
            if (splitPoint == -1)
            {
                result = path;
                path = string.Empty;
            }
            else
            {
                result = path.Remove(splitPoint);
                path = path.Substring(Math.Min(splitPoint + 1, path.Length));
            }

            return result;
        }

        private static string GetLastPathPart(ref string path)
        {
            path = path.Trim();
            if (path == string.Empty)
                return string.Empty;

            if (scopeSeparators.Contains(path[path.Length - 1]))
                path = path.Remove(path.Length - 1);

            string result;
            int splitPoint = path.LastIndexOfAny(scopeSeparators);
            if (splitPoint == -1)
            {
                result = path;
                path = string.Empty;
            }
            else
            {
                result = path.Substring(splitPoint + 1);
                path = path.Remove(Math.Min(splitPoint, path.Length));
            }

            return result;
        }

        private static string GenerateUniqueReferenceId(int number)
        {
            //0 = "A"; 25 = "Z"; 26 = "AA"; 27 = "AB"; 51 = "AZ"; 52 = "AAA"; etc
            string result = string.Empty;
            while (number > 25)
            {
                result += (number % 26).AsLetter();
                number -= (26 + (number % 26));
            }

            result += number.AsLetter();
            return new string(result.ToCharArray().Reverse().ToArray());
        }

        private static char AsLetter(this int number)
        {
            return (char)(number + 65);
        }
    }
}
