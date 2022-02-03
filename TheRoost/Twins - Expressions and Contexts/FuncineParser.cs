using System;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Entities;
using SecretHistories.Spheres;
using SecretHistories.Fucine;
using SecretHistories.Enums;
using SecretHistories.UI;

using TheRoost.Twins.Entities;

namespace TheRoost.Twins
{
    public static class FuncineParser
    {
        static readonly char[] referenceOpening = new char[] { '[', '{', };
        static readonly char[] referenceClosing = new char[] { ']', '}', };
        static readonly char[] scopeSeparators = new char[] { '/', '\\' };
        static readonly char[] operationSigns = new char[] { '(', ')', '|', '&', '!', '~', '=', '<', '>', '^', '+', '-', '*', /*'/',*/ '%' };
        const char entityIdSeparator = ':';

        public static List<FuncineRef> LoadReferences(ref string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw Birdsong.Cack("Expression definition is empty");

            List<FuncineRef> references = new List<FuncineRef>();

            expression.Trim().ToLower();
            if (isSingleReferenceExpression(expression))
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

        static bool isSingleReferenceExpression(string expression)
        {
            //there's a miniscule problem wthat it won't distinguish things like 'verb/element' and 'element/2'
            //semi-solved by parser catching all-digits element names
            return (expression.IndexOfAny(referenceOpening) == -1 && expression.IndexOfAny(operationSigns) == -1
                && char.IsDigit(expression[0]) == false && expression.Any(char.IsLetter) == true
                && expression.StartsWith("true") == false
                && expression.StartsWith("false") == false);
        }

        public static void PopulateFucineReference(string referenceData, out string elementId, out Funcine<bool> filter, out FuncineRef.SphereTokensRef targetTokens, out FuncineRef.SpecialOperation special)
        {
            try
            {
                elementId = GetLastPathPart(ref referenceData);
                special = GetReferenceOp(ref elementId);

                if (elementId.Any(char.IsLetter) == false)
                    throw Birdsong.Cack("Wrong element id {0} (if this is intentional - don't use only digits, it confuses parser)", elementId);

                filter = default(Funcine<bool>);
                if (referenceData.Length > 0 && referenceClosing.Contains(referenceData[referenceData.Length - 1]))
                {
                    int filterOpening, filterClosing;
                    string filterData = GetBordersOfSeparatedArea(referenceData, out filterOpening, out filterClosing);

                    filter = new Funcine<bool>(filterData);
                    referenceData = referenceData.Remove(filterOpening);
                }

                targetTokens = GetSphereTokenRef(referenceData);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static FuncineRef.SpecialOperation GetReferenceOp(ref string elementId)
        {
            if (elementId == FuncineRef.opCountKeyword)
                return FuncineRef.SpecialOperation.CountCards;
            else if (elementId.StartsWith(FuncineRef.opMaxKeyword))
            {
                elementId = elementId.Split(entityIdSeparator)[1];
                return FuncineRef.SpecialOperation.SingleHighest;
            }
            else if (elementId.StartsWith(FuncineRef.opMinKeyword))
            {
                elementId = elementId.Split(entityIdSeparator)[1];
                return FuncineRef.SpecialOperation.SingleLowest;
            }

            return FuncineRef.SpecialOperation.None;
        }

        private readonly static Dictionary<string, FuncineRef.SphereTokensRef> _cachedSphereTokenRefs = new Dictionary<string, FuncineRef.SphereTokensRef>();
        public static FuncineRef.SphereTokensRef GetSphereTokenRef(string path)
        {
            path = path.NormalizeAsContextPath();

            if (_cachedSphereTokenRefs.ContainsKey(path) == false)
                _cachedSphereTokenRefs[path] = InterpretReferencePath(path);

            return _cachedSphereTokenRefs[path];
        }

        private static string NormalizeAsContextPath(this string path)
        {
            path = path.ToLower();

            if (path.Length > 0 && scopeSeparators.Contains(path[0]))
                path = path.Substring(1);

            foreach (char separator in scopeSeparators)
                path = path.Replace(separator, scopeSeparators[0]);

            return path;
        }

        public static FuncineRef.SphereTokensRef InterpretReferencePath(string path)
        {
            if (path.StartsWith("~"))
            {
                FucinePath fucinePath = new FucinePath(path);
                return () => Watchman.Get<HornedAxe>().GetSphereByPath(fucinePath).GetElementTokens();
            }

            string initial_path = path;

            string[] pathPart = GetNextPathPart(ref path).Split(entityIdSeparator);
            string referenceType = pathPart[0];
            string entityId = pathPart.Length > 1 ? pathPart[1] : string.Empty;

            switch (referenceType)
            {
                case "verb": return InterpretVerbPath(ref path, entityId, initial_path);
                case "deck": return () => Watchman.Get<SecretHistories.Infrastructure.DealersTable>().GetDrawPile(entityId).GetElementTokens();
                case "deck_forbidden": return () => Watchman.Get<SecretHistories.Infrastructure.DealersTable>().GetForbiddenPile(entityId).GetElementTokens();
                case "table": return () => Watchman.Get<HornedAxe>().GetDefaultSphere().GetElementTokens();
                case "extant": return TokenContextAccessors.GetExtantTokens;
                case "token": return TokenContextAccessors.GetLocalTokenAsTokens;
                case "": return TokenContextAccessors.GetLocalSphereTokens;
                default:
                    throw Birdsong.Cack("Unknown reference type '{0}' in '{1}'", referenceType, initial_path);
            }
        }

        private static FuncineRef.SphereTokensRef InterpretVerbPath(ref string path, string verbId, string initial_path)
        {
            string[] pathPart = GetNextPathPart(ref path).Split(entityIdSeparator);
            string sphere = pathPart[0];
            string subId = pathPart.Length > 1 ? pathPart[1] : string.Empty;

            if (string.IsNullOrWhiteSpace(verbId))
                switch (sphere)
                {
                    case "": return () => TokenContextAccessors.GetLocalSituation().GetElementTokensInSituation();
                    case "slot": return () => TokenContextAccessors.GetLocalSituation().GetSituationSlot(subId).GetElementTokens();
                    case "slots": return () => TokenContextAccessors.GetLocalSituation().GetSpheresByCategory(SphereCategory.Threshold).GetTokensFromSpheres();
                    case "storage": return () => TokenContextAccessors.GetLocalSituation().GetSituationStorage().GetElementTokens();
                    default:
                        throw Birdsong.Cack("Unknown situation sphere type '{0}' in {1}", sphere);
                }

            switch (sphere)
            {
                case "": return () => TokenContextAccessors.GetSituation(verbId).GetElementTokensInSituation();
                case "slot": return () => TokenContextAccessors.GetSituation(verbId).GetSituationSlot(subId).GetElementTokens();
                case "slots": return () => TokenContextAccessors.GetSituation(verbId).GetSpheresByCategory(SphereCategory.Threshold).GetTokensFromSpheres();
                case "storage": return () => TokenContextAccessors.GetSituation(verbId).GetSituationStorage().GetElementTokens();
                default:
                    throw Birdsong.Cack("Unknown situation sphere type '{0}' in {1}", sphere);
            }
        }

        private readonly static Dictionary<string, Func<Sphere>> _cachedSphereRefs = new Dictionary<string, Func<Sphere>>();
        public static Func<Sphere> GetSphereRef(string path)
        {
            path = path.NormalizeAsContextPath();

            if (_cachedSphereRefs.ContainsKey(path) == false)
                _cachedSphereRefs[path] = InterpretSphereReferencePath(path);

            return _cachedSphereRefs[path];
        }

        public static Func<Sphere> InterpretSphereReferencePath(string path)
        {
            if (path.StartsWith("~"))
            {
                FucinePath fucinePath = new FucinePath(path);
                return () => Watchman.Get<HornedAxe>().GetSphereByPath(fucinePath);
            }

            string initial_path = path;

            string[] pathPart = GetNextPathPart(ref path).Split(entityIdSeparator);
            string referenceType = pathPart[0];
            string entityId = pathPart.Length > 1 ? pathPart[1] : string.Empty;

            switch (referenceType)
            {
                case "table": return () => Watchman.Get<HornedAxe>().GetDefaultSphere();
                case "local": return () => TokenContextAccessors.GetLocalSituation().GetSituationStorage();
                case "verb": return () => TokenContextAccessors.GetSituation(entityId).GetSituationStorage();
                case "deck": return () => Watchman.Get<SecretHistories.Infrastructure.DealersTable>().GetDrawPile(entityId) as Sphere;
                case "deck_forbidden": return () => Watchman.Get<SecretHistories.Infrastructure.DealersTable>().GetForbiddenPile(entityId) as Sphere;
                default:
                    throw Birdsong.Cack("Unknown reference type '{0}' in '{1}'", referenceType, initial_path);
            }
        }

        private static string GetBordersOfSeparatedArea(string expression, out int openingPosition, out int closingPosition)
        {
            openingPosition = expression.IndexOfAny(referenceOpening);
            closingPosition = expression.IndexOfAny(referenceClosing);

            if (openingPosition == -1)
                return expression;

            if (closingPosition == -1)
                throw Birdsong.Cack("Reference in {0} is not closed", expression);

            string referenceData = expression.Substring(openingPosition + 1, closingPosition - openingPosition - 1);
            int innerOpeningsCount = referenceData.Split(referenceOpening).Length - 1;
            int openingsAccounted = 0;
            while (innerOpeningsCount > openingsAccounted)
            {
                for (; openingsAccounted < innerOpeningsCount; openingsAccounted++)
                {
                    closingPosition = expression.IndexOfAny(referenceClosing, closingPosition + 1);
                    if (closingPosition == -1)
                        throw Birdsong.Cack("Unclosed reference in {0}", expression);
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
            //as if someone will ever make an expression with 26 unique references...............................
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
