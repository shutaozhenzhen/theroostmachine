using System;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Entities;
using SecretHistories.Spheres;
using SecretHistories.Fucine;
using SecretHistories.Enums;
using SecretHistories.UI;

using Roost.Twins.Entities;

namespace Roost.Twins
{
    public static class FuncineParser
    {
        static readonly char[] segmentOpening = new char[] { '[', '{', };
        static readonly char[] segmentClosing = new char[] { ']', '}', };
        static readonly char[] operationSigns = new char[] { '(', ')', '|', '&', '!', '~', '=', '<', '>', '^', '+', '-', '*', /*'/',*/ '%' };

        public static List<FuncineRef> LoadReferences(ref string expression)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(expression))
                    throw Birdsong.Cack("Expression is empty");

                List<FuncineRef> references = new List<FuncineRef>();

                expression.Trim().ToLower();
                if (isSingleReferenceExpression(expression))
                    expression = string.Concat(segmentOpening[0], expression, segmentClosing[0]);

                int openingPosition, closingPosition;
                string referenceData = GetBordersOfSeparatedArea(expression, out openingPosition, out closingPosition);
                while (openingPosition > -1)
                {
                    string referenceId = GenerateUniqueReferenceId(references.Count);
                    FuncineRef reference = ParseFuncineRef(referenceData, referenceId);

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
            catch (Exception ex)
            {
                throw ex;
            }
        }

        static bool isSingleReferenceExpression(string expression)
        {
            return expression.IndexOfAny(segmentOpening) == -1 && expression.IndexOfAny(operationSigns) == -1
                && char.IsDigit(expression[0]) == false && expression.Any(char.IsLetter) == true
                && expression.Equals("true", StringComparison.InvariantCultureIgnoreCase) == false
                && expression.Equals("false", StringComparison.InvariantCultureIgnoreCase) == false;
        }

        public static FuncineRef ParseFuncineRef(string path, string referenceId)
        {
            const char partsSeparator = ':';

            string[] pathParts = path.Split(partsSeparator);

            if (pathParts.Length == 0)
                throw Birdsong.Cack($"Malformed reference '{path}' - appears to be empty");
            else if (pathParts.Length == 1)
                return new FuncineRef(referenceId, TokenContextAccessors.currentLocal, default(Funcine<bool>), ParseTokenValueRef(pathParts[0]));
            else if (pathParts.Length == 2)
                return new FuncineRef(referenceId, ParseFuncineSpherePath(pathParts[0]), default(Funcine<bool>), ParseTokenValueRef(pathParts[1]));
            else if (pathParts.Length == 3)
                return new FuncineRef(referenceId, ParseFuncineSpherePath(pathParts[0]), new Funcine<bool>(pathParts[1]), ParseTokenValueRef(pathParts[2]));
            else
                throw Birdsong.Cack($"Malformed reference '{path}' - too many parts (possibly a separation symbol in an entity id?)");
        }

        public static FucinePath ParseFuncineSpherePath(string path)
        {
            const char multiPathSign = '+';
            const char excludeCategorySign = '-';
            const char categorySeparator = ',';

            if (PathisMultiPath())
            {

                List<SphereCategory> acceptedCategories = null;
                List<SphereCategory> excludedCategories = null;
                int categoriesStart = path.LastIndexOfAny(segmentOpening);
                if (categoriesStart != -1)
                {
                    int categoriesEnd = path.LastIndexOfAny(segmentClosing);
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
                            throw Birdsong.Cack($"Unknown sphere category '{category.Substring(1)}'");

                        acceptedCategories.Add(parsedCategory);
                    }
                }

                int amount = 0;
                int lastPlusPosition = path.LastIndexOf(multiPathSign);
                if (lastPlusPosition > -1)
                {
                    string endPathPart = path.Substring(lastPlusPosition, path.Length - lastPlusPosition);
                    if (endPathPart.Length == 1)
                        return new FucineMultiPath(path.Substring(0, path.Length - 1), 0);

                    endPathPart = endPathPart.Substring(1);

                    if (int.TryParse(endPathPart, out amount) == false)
                        throw Birdsong.Cack($"Can't parse FicineMultiPath sphere limit {endPathPart}");

                    path = path.Remove(lastPlusPosition);
                }

                return new FucineMultiPath(path, amount, acceptedCategories, excludedCategories);
            }

            return new FucinePath(path);

            bool PathisMultiPath()
            {
                return segmentClosing.Contains(path[path.Length - 1]) || path.Contains(multiPathSign);

            }
        }

        public static TokenValueRef ParseTokenValueRef(string data)
        {
            const char specialOpSymbol = '$';
            const char partsSeparator = '/';
            string[] parts = data.Trim().Split(partsSeparator);

            if (parts.Length == 0)
                throw Birdsong.Cack($"Malformed token value reference '{data}' - appears to be empty");
            else if (parts.Length == 1)
            {
                //special operation, doesn't require area and target
                if (parts[0][0] == specialOpSymbol)
                {
                    string specialOpName = parts[0].Substring(1);
                    TokenValueRef.ValueOperation specialOp;
                    if (Enum.TryParse(specialOpName, true, out specialOp))
                        return new TokenValueRef(null, TokenValueRef.ValueArea.Special, specialOp);
                    else
                        throw Birdsong.Cack($"Unknown special token value reference '{parts[0]}'");
                }

                //only target is defined, area and operation are default
                return new TokenValueRef(parts[0], TokenValueRef.ValueArea.Aspect, TokenValueRef.ValueOperation.Sum);
            }
            else if (parts.Length == 2)
            {
                TokenValueRef.ValueArea area; TokenValueRef.ValueOperation operation;

                //everything is defined, trying to parse area and operation
                string target = parts[1];
                string opData = parts[0];
                foreach (string areaName in Enum.GetNames(typeof(TokenValueRef.ValueArea)))
                    if (opData.StartsWith(areaName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        Enum.TryParse(areaName, true, out area);

                        opData = opData.Substring(areaName.Length);
                        if (Enum.TryParse(opData, true, out operation))
                            return new TokenValueRef(target, area, operation);

                        throw Birdsong.Cack($"Unknown token value reference operation '{opData}'");
                    }

                throw Birdsong.Cack($"Unknown token value reference area in '{opData}'");
            }
            else
                throw Birdsong.Cack($"Malformed token value reference '{data}' - too many parts (possible separation symbol in the target id?)");
        }

        private static string GetBordersOfSeparatedArea(string expression, out int openingPosition, out int closingPosition)
        {
            openingPosition = expression.IndexOfAny(segmentOpening);
            closingPosition = expression.IndexOfAny(segmentClosing);

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
                    closingPosition = expression.IndexOfAny(segmentClosing, closingPosition + 1);
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
