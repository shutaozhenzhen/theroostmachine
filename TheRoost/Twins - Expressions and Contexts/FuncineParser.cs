using System;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Fucine;
using SecretHistories.Enums;

using TheRoost.Twins.Entities;

namespace TheRoost.Twins
{
    public static class FuncineParser
    {
        static readonly char[] referenceOpening = new char[] { '[', '{', };
        static readonly char[] referenceClosing = new char[] { ']', '}', };
        static readonly char[] scopeSeparators = new char[] { '/', '\\' };
        static readonly char[] operationSigns = new char[] { '(', ')', '|', '&', '!', '~', '=', '<', '>', '^', '+', '-', '*', '/', '%' };
        const char entityIdSeparator = ':';

        public static List<FuncineRef> LoadReferences(ref string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw Birdsong.Droppings("Expression definition is empty");

            List<FuncineRef> references = new List<FuncineRef>();

            expression.Trim();
            if (expression.IndexOfAny(operationSigns) == -1 && expression.IndexOfAny(referenceOpening) == -1
                && expression.StartsWith("true") == false && expression.StartsWith("false") == false)
                expression = string.Concat(referenceOpening[0], expression, referenceClosing[0]);

            int openingPosition, closingPosition;
            string referenceData = GetBordersOfSeparatedArea(expression, out openingPosition, out closingPosition);
            while (openingPosition > -1)
            {
                string referenceId = GenerateUniqueReferenceId(references.Count);
                FuncineRef reference = new FuncineRef(referenceData, referenceId);

                bool referenceIsUnique = true;
                foreach (FuncineRef olderReference in references)
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

        public static void PopulateFucineReference(string referenceData, out string elementId, out Funcine<bool> filter, out FuncineRef.SphereTokenGet targetSphere)
        {
            elementId = GetLastPathPart(ref referenceData);

            filter = default(Funcine<bool>);
            if (referenceData.Length > 0 && referenceClosing.Contains(referenceData[referenceData.Length - 1]))
            {
                int filterOpening, filterClosing;
                string filterData = GetBordersOfSeparatedArea(referenceData, out filterOpening, out filterClosing);

                filter = new Funcine<bool>(filterData);
                referenceData = referenceData.Remove(filterOpening);
            }

            targetSphere = GetSphereGetter(referenceData);
        }

        private readonly static Dictionary<string, FuncineRef.SphereTokenGet> _cachedSphereGetters = new Dictionary<string, FuncineRef.SphereTokenGet>();
        public static FuncineRef.SphereTokenGet GetSphereGetter(string path)
        {
            path = path.NormalizeAsContextPath();

            if (_cachedSphereGetters.ContainsKey(path) == false)
                _cachedSphereGetters[path] = InterpretReferencePath(path);

            return _cachedSphereGetters[path];
        }

        public static string NormalizeAsContextPath(this string path)
        {
            path = path.ToLower();

            if (path.Length > 0 && scopeSeparators.Contains(path[0]))
                path = path.Substring(1);

            foreach (char separator in scopeSeparators)
                path = path.Replace(separator, scopeSeparators[0]);

            return path;
        }

        public static FuncineRef.SphereTokenGet InterpretReferencePath(string path)
        {
            if (path.StartsWith("~"))
                return () => TokenContextAccessors.GetSphereTokensByPath(new FucinePath(path));

            string[] pathPart = GetNextPathPart(ref path).Split(entityIdSeparator);
            string referenceType = pathPart[0];
            string entityId = pathPart.Length > 1 ? pathPart[1] : string.Empty;

            switch (referenceType)
            {
                case "verb": return InterpretVerbPath(ref path, entityId);
                case "deck": return () => TokenContextAccessors.GetDeckTokens(entityId);
                case "deck_forbidden": return () => TokenContextAccessors.GetDeckForbiddenTokens(entityId);
                case "table": return TokenContextAccessors.GetTableTokens;
                case "extant": return TokenContextAccessors.GetExtantTokens;
                case "token": return TokenContextAccessors.GetLocalTokenAsTokens;
                case "": return TokenContextAccessors.GetLocalSphereTokens;
                default:
                    throw Birdsong.Droppings("Unknown reference type {0}", referenceType);
            }
        }

        private static FuncineRef.SphereTokenGet InterpretVerbPath(ref string path, string verbId)
        {
            string[] pathPart = GetNextPathPart(ref path).Split(entityIdSeparator);
            string sphere = pathPart[0];
            string subId = pathPart.Length > 1 ? pathPart[1] : string.Empty;

            if (string.IsNullOrWhiteSpace(verbId))
                switch (sphere)
                {
                    case "": return () => TokenContextAccessors.GetLocalSituation().GetElementTokensInSituation();
                    case "slot": return () => TokenContextAccessors.GetLocalSituation().GetSituationSlot(subId).GetElementTokens();
                    case "slots": return () => TokenContextAccessors.GetLocalSituation().GetSpheresByCategory(SphereCategory.Threshold).GetSpheresTokens();
                    case "storage": return () => TokenContextAccessors.GetLocalSituation().GetSituationStorageTokens();
                    default:
                        throw Birdsong.Droppings("Unknown situation sphere type {0}", sphere);
                }

            switch (sphere)
            {
                case "": return () => TokenContextAccessors.GetSituation(verbId).GetElementTokensInSituation();
                case "slot": return () => TokenContextAccessors.GetSituation(verbId).GetSituationSlot(subId).GetElementTokens();
                case "slots": return () => TokenContextAccessors.GetSituation(verbId).GetSpheresByCategory(SphereCategory.Threshold).GetSpheresTokens();
                case "storage": return () => TokenContextAccessors.GetSituation(verbId).GetSituationStorageTokens();
                default:
                    throw Birdsong.Droppings("Unknown situation sphere type {0}", sphere);
            }
        }

        private static string GetBordersOfSeparatedArea(string expression, out int openingPosition, out int closingPosition)
        {
            openingPosition = expression.IndexOfAny(referenceOpening);
            closingPosition = expression.IndexOfAny(referenceClosing);

            if (openingPosition == -1)
                return expression;

            if (closingPosition == -1)
                throw Birdsong.Droppings("Reference in {0} is not closed", expression);

            string referenceData = expression.Substring(openingPosition + 1, closingPosition - openingPosition - 1);
            int innerOpeningsCount = referenceData.Split(referenceOpening).Length - 1;
            int openingsAccounted = 0;
            while (innerOpeningsCount > openingsAccounted)
            {
                for (; openingsAccounted < innerOpeningsCount; openingsAccounted++)
                {
                    closingPosition = expression.IndexOfAny(referenceClosing, closingPosition + 1);
                    if (closingPosition == -1)
                        throw Birdsong.Droppings("Unclosed reference in {0}", expression);
                }

                referenceData = expression.Substring(openingPosition + 1, closingPosition - openingPosition - 1);
                innerOpeningsCount = referenceData.Split(referenceOpening).Length - 1;
            }

            return referenceData;
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

        public static string GetLastPathPart(ref string path)
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
