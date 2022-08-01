using System;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Fucine;
using SecretHistories.Enums;

using Roost.Twins.Entities;

namespace Roost.Twins
{
    public static class TwinsParser
    {
        const char referenceOpening = '[';
        const char referenceClosing = ']';
        const char filterOpening = '{';
        const char filterClosing = '}';

        public static List<FucineRef> LoadReferencesForExpression(ref string expression)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(expression))
                    throw Birdsong.Cack("Expression is empty");

                expression = expression.Trim();
                if (isSingleReferenceExpression(expression))
                    expression = string.Concat(referenceOpening, expression, referenceClosing);

                List<FucineRef> references = new List<FucineRef>();

                int openingPosition, closingPosition;
                string referenceData = GetBordersOfSeparatedArea(expression, out openingPosition, out closingPosition, referenceOpening, referenceClosing);
                while (openingPosition > -1)
                {
                    string referenceId = GenerateUniqueReferenceId(references.Count);
                    FucineRef reference = new FucineRef(referenceData, referenceId);

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
                    referenceData = GetBordersOfSeparatedArea(expression, out openingPosition, out closingPosition, referenceOpening, referenceClosing);
                }

                return references;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        static bool isSingleReferenceExpression(string expression)
        {
            return expression.IndexOf(referenceOpening) == -1
                && expression.Any(char.IsLetter) == true
                && expression.Contains("(") == false && expression.Contains(")") == false
                && expression.Equals("true", StringComparison.InvariantCultureIgnoreCase) == false
                && expression.Equals("false", StringComparison.InvariantCultureIgnoreCase) == false;
        }

        public static void ParseFucineRef(string data, out FucinePath targetPath, out FucineExp<bool> filter, out FucineNumberGetter target)
        {
            const char partsSeparator = ':';
            data = data.Trim().ToLower();


            GetBordersOfSeparatedArea(data, out int openingPosition, out int closingPosition, filterOpening, filterClosing);
            FucineExp<bool> separatedFilter = default(FucineExp<bool>);
            if (openingPosition > -1)
            {
                string filterData = data.Substring(openingPosition + 1, closingPosition - openingPosition - 1);
                data = data.Remove(openingPosition, closingPosition - openingPosition + 1);
                data = data.Remove(data.IndexOf(partsSeparator), 1); //removing a single ':' for the removed filter
                separatedFilter = new FucineExp<bool>(filterData);
            }

            string[] pathParts = data.Split(partsSeparator);
            switch (pathParts.Length)
            {
                case 1:
                    targetPath = new FucinePath(Crossroads.currentScope);
                    filter = default(FucineExp<bool>);
                    target = ParseTokenValueRef(pathParts[0]);
                    break;
                case 2:
                    targetPath = ParseSpherePath(pathParts[0]);
                    filter = default(FucineExp<bool>);
                    target = ParseTokenValueRef(pathParts[1]);
                    break;
                case 3:
                    targetPath = ParseSpherePath(pathParts[0]);
                    filter = new FucineExp<bool>(pathParts[1]);
                    target = ParseTokenValueRef(pathParts[2]);
                    break;
                case 0:
                    throw Birdsong.Cack($"Malformed reference '{data}' - appears to be empty");
                default:
                    throw Birdsong.Cack($"Malformed reference '{data}' - too many parts (possibly a separation symbol in an entity id?)");
            }

            if (separatedFilter.isUndefined == false)
                filter = separatedFilter;
        }

        public static FucinePath ParseSpherePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return FucinePath.Current();

            path = path.Trim();
            bool pathIsMultiPath = path.Contains("+") || path[path.Length - 1] == referenceClosing;
            if (pathIsMultiPath)
            {
                ParsePathPlusLimit(ref path, out int amount);
                ParsePathTargetCategories(ref path, out List<SphereCategory> acceptedCategories, out List<SphereCategory> excludedCategories);

                return new FucinePathPlus(path, amount, acceptedCategories, excludedCategories);
            }

            return new FucinePath(path);
        }

        private static void ParsePathPlusLimit(ref string path, out int amount)
        {
            const char multiPathSign = '+';

            amount = 1; //if no limit is specified at all (only categories), default limit is 1
            int lastPlusPosition = path.LastIndexOf(multiPathSign);
            if (lastPlusPosition > -1)
            {
                string endPathPart = path.Substring(lastPlusPosition, path.Length - lastPlusPosition).Substring(1);
                path = path.Remove(lastPlusPosition);

                if (endPathPart.Length == 0) //limit amount isn't specified, default to 0 (unlimited)
                {
                    amount = 0;
                    return;
                }

                if (int.TryParse(endPathPart, out amount) == false)
                    throw Birdsong.Cack($"Can't parse FucinePathPlus sphere limit {endPathPart}");
            }
        }

        private static void ParsePathTargetCategories(ref string path, out List<SphereCategory> acceptedCategories, out List<SphereCategory> excludedCategories)
        {
            const char excludeCategorySign = '-';
            const char categorySeparator = ',';

            acceptedCategories = null;
            excludedCategories = null;
            int categoriesStart = path.LastIndexOf(referenceOpening);
            if (categoriesStart != -1)
            {
                int categoriesEnd = path.LastIndexOf(referenceClosing);
                if (categoriesEnd == -1)
                    throw Birdsong.Cack($"Unclosed category definition in path {path}");
                string[] categoryData = path.Substring(categoriesStart + 1, categoriesEnd - categoriesStart - 1).Split(categorySeparator);

                path = path.Remove(categoriesStart);

                acceptedCategories = new List<SphereCategory>();
                excludedCategories = new List<SphereCategory>();
                foreach (string category in categoryData)
                {
                    SphereCategory parsedCategory;
                    if (category[0] == excludeCategorySign)
                    {
                        if (Enum.TryParse(category.Substring(1), true, out parsedCategory) == false)
                            throw Birdsong.Cack($"Unknown sphere category '{category.Substring(1)}'");

                        excludedCategories.Add(parsedCategory);
                        continue;
                    }

                    if (Enum.TryParse(category, true, out parsedCategory) == false)
                        throw Birdsong.Cack($"Unknown sphere category '{category}'");

                    acceptedCategories.Add(parsedCategory);
                }
            }
        }

        public static FucineNumberGetter ParseTokenValueRef(string data)
        {
            const char specialOpSymbol = '$';
            const char partsSeparator = '/';
            string[] parts = data.Trim().Split(partsSeparator);

            switch (parts.Length)
            {
                case 0:
                    throw Birdsong.Cack($"Malformed token value reference '{data}' - appears to be empty");
                case 1:
                    //special operation, doesn't require area and target
                    if (parts[0][0] == specialOpSymbol)
                    {
                        string specialOpName = parts[0].Substring(1);
                        FucineNumberGetter.ValueOperation specialOp;
                        if (Enum.TryParse(specialOpName, true, out specialOp))
                            return new FucineNumberGetter(null, FucineNumberGetter.ValueArea.NoArea, specialOp);
                        else
                            throw Birdsong.Cack($"Unknown special token value reference '{parts[0]}'");
                    }

                    //only target is defined, area and operation are default
                    return new FucineNumberGetter(parts[0], FucineNumberGetter.ValueArea.Aspect, FucineNumberGetter.ValueOperation.Sum);
                case 2:
                    string target = parts[1];

                    //everything is defined, trying to parse area and operation
                    FucineNumberGetter.ValueArea area; FucineNumberGetter.ValueOperation operation;
                    string opData = parts[0];

                    foreach (string areaName in Enum.GetNames(typeof(FucineNumberGetter.ValueArea)))
                        if (opData.StartsWith(areaName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            Enum.TryParse(areaName, true, out area);

                            opData = opData.Substring(areaName.Length);
                            if (opData.Length == 0)
                                return new FucineNumberGetter(target, area, FucineNumberGetter.ValueOperation.Sum);
                            if (Enum.TryParse(opData, true, out operation))
                                return new FucineNumberGetter(target, area, operation);

                            throw Birdsong.Cack($"Unknown token value reference operation '{opData}' in '{data}'");
                        }

                    area = FucineNumberGetter.ValueArea.Aspect;
                    if (Enum.TryParse(opData, true, out operation))
                        return new FucineNumberGetter(target, area, operation);

                    throw Birdsong.Cack($"Unknown token value reference area/operation '{opData}' in '{data}'");
                default:
                    throw Birdsong.Cack($"Malformed token value reference '{data}' - too many parts (possible separation symbol in the target id?)");
            }
        }

        private static string GetBordersOfSeparatedArea(string expression, out int openingPosition, out int closingPosition, char segmentOpening, char segmentClosing)
        {
            openingPosition = expression.IndexOf(segmentOpening);
            closingPosition = expression.IndexOf(segmentClosing);

            if (openingPosition == -1)
                return expression;

            if (closingPosition == -1)
                throw Birdsong.Cack($"Reference in expression '{expression}' is not closed");

            string referenceData = expression.Substring(openingPosition + 1, closingPosition - openingPosition - 1);
            int innerOpeningsCount = referenceData.Split(segmentOpening).Length - 1;
            int openingsAccounted = 0;
            while (innerOpeningsCount > openingsAccounted)
            {
                for (; openingsAccounted < innerOpeningsCount; openingsAccounted++)
                {
                    closingPosition = expression.IndexOf(segmentClosing, closingPosition + 1);
                    if (closingPosition == -1)
                        throw Birdsong.Cack($"Reference in expression '{expression}' is not closed");
                }

                referenceData = expression.Substring(openingPosition + 1, closingPosition - openingPosition - 1);
                innerOpeningsCount = referenceData.Split(segmentOpening).Length - 1;
            }

            return referenceData;
        }

        private static string GenerateUniqueReferenceId(int number)
        {
            //0 = "A"; 25 = "Z"; 26 = "AA"; 27 = "AB"; 51 = "AZ"; 52 = "AAA"; etc
            //as if someone will ever make an expression with 26 unique references...............................
            string result = string.Empty;
            while (number > 25)
            {
                result += ToLetter(number % 26);
                number -= (26 + (number % 26));
            }

            result += ToLetter(number);
            return new string(result.ToCharArray().Reverse().ToArray());

            char ToLetter(int charNumber)
            {
                return (char)(charNumber + 65);
            }
        }
    }
}
