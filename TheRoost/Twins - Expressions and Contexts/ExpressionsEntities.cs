using System;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Spheres;

using Roost.World;

using NCalc;

namespace Roost.Twins.Entities
{
    public struct FucineExp<T> where T : IConvertible
    {
        readonly Expression expression;
        readonly FucineRef[] references;
        public readonly string formula;

        public const string UNDEFINED = "undefined";
        public FucineExp(string data)
        {
            if (data == UNDEFINED)
            {
                expression = null;
                references = null;
                formula = string.Empty;
                return;
            }

            this.formula = data;
            try
            {
                this.references = TwinsParser.LoadReferencesForExpressin(ref data).ToArray();
                this.expression = new Expression(Expression.Compile(data, false));
            }
            catch (Exception ex)
            {
                throw Birdsong.Cack($"Unable to parse expression {this.formula} - {ex.FormatException()}");
            }
        }

        public T value
        {
            get
            {
                foreach (FucineRef reference in references)
                    expression.Parameters[reference.idInExpression] = reference.value;

                object result = expression.Evaluate();
                return result.ConvertTo<T>();
            }
        }

        public static implicit operator FucineExp<T>(string formula) { return new FucineExp<T>(formula); }

        public bool isUndefined { get { return this.expression == null; } }

        public override string ToString()
        {
            if (isUndefined)
                return UNDEFINED;
            return "'" + this.formula + "' = " + this.value;
        }
    }

    public struct FucineRef
    {
        public readonly string idInExpression;

        public readonly FucinePath path;
        public readonly FucineExp<bool> filter;
        public readonly FucineNumberGetter valueGetter;

        public List<Sphere> targetSpheres { get { return Crossroads.GetSpheresByPath(path); } }
        public List<Token> tokens { get { return Crossroads.GetTokensByPath(path).FilterTokens(filter); } }
        public float value { get { return valueGetter.GetValueFromTokens(this.tokens); } }

        public FucineRef(string referenceData, string referenceId)
        {
            this.idInExpression = referenceId;
            TwinsParser.ParseFucineRef(referenceData, out path, out filter, out valueGetter);
        }

        public FucineRef(string referenceData)
        {
            idInExpression = null;
            TwinsParser.ParseFucineRef(referenceData, out path, out filter, out valueGetter);
        }

        public bool Equals(FucineRef otherReference)
        {
            return otherReference.path == this.path && otherReference.filter.formula == this.filter.formula && otherReference.valueGetter.Equals(this.valueGetter);
        }
    }

    public class FucinePathPlus : FucinePath
    {
        private readonly string fullPath;
        public readonly string sphereMask;
        public FucinePathPlus(string path, int maxSpheresToFind, List<SphereCategory> acceptable = null, List<SphereCategory> excluded = null) : base(path)
        {
            this.maxSpheresToFind = maxSpheresToFind;

            if (excluded == null && acceptable == null)
                acceptableCategories = defaultAcceptableCategories;
            else
            {
                acceptable = acceptable ?? allCategories;
                excluded = excluded ?? defaultExcludedCategories;
                acceptableCategories = acceptable.Except(excluded).ToList();

                if (acceptableCategories.SequenceEqual(defaultAcceptableCategories))
                    acceptableCategories = defaultAcceptableCategories;
            }

            //guaranteeing that equivalent paths will have the same id for caching
            fullPath = $"{path}[{string.Join(",", this.acceptableCategories)}]+{maxSpheresToFind}";
            if (this.IsWild()) //removing asterisk so IndexOf() checks are correct
                sphereMask = path.Substring(1);
            else
                sphereMask = path;
        }

        public bool AcceptsCategory(SphereCategory sphereCategory)
        {
            return acceptableCategories.Contains(sphereCategory);
        }

        public readonly int maxSpheresToFind;

        public List<SphereCategory> acceptableCategories;

        private static readonly List<SphereCategory> allCategories = new List<SphereCategory>((SphereCategory[])Enum.GetValues(typeof(SphereCategory)));
        private static readonly List<SphereCategory> defaultExcludedCategories = new List<SphereCategory> { SphereCategory.Notes, SphereCategory.Null, SphereCategory.Meta };
        private static readonly List<SphereCategory> defaultAcceptableCategories = allCategories.Except(defaultExcludedCategories).ToList();

        public override string ToString() { return fullPath; }
    }

    public struct FucineNumberGetter
    {
        public readonly ValueArea area;
        public readonly ValueOperation operation;
        public readonly string targetId;

        public delegate float SingleTokenValue(Token token, string target);
        public SingleTokenValue GetValue;

        public delegate float HandleTokenValues(List<Token> tokens, SingleTokenValue getTokenValue, string target);
        HandleTokenValues HandleValues;

        public float GetValueFromTokens(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return 0;

            return HandleValues(tokens, GetValue, targetId);
        }

        public bool Equals(FucineNumberGetter otherValueRef)
        {
            return this.area == otherValueRef.area && this.operation == otherValueRef.operation && this.targetId == otherValueRef.targetId;
        }

        public FucineNumberGetter(string target) : this(target, ValueArea.Aspect, ValueOperation.Sum) { }

        public FucineNumberGetter(string target, ValueArea fromArea, ValueOperation withOperation)
        {
            this.targetId = target;
            this.area = fromArea;
            this.operation = withOperation;

            if (target[target.Length - 1] == '*')
                if (Enum.TryParse(fromArea.ToString() + "Wild", out ValueArea wildArea))
                {
                    fromArea = wildArea;
                    this.targetId = target.Remove(target.Length - 1);
                }

            GetValue = singleValueGetters[fromArea];
            HandleValues = valueHandlers[withOperation];

            if (this.area == ValueArea.Aspect || this.operation == ValueOperation.Root)
                Watchman.Get<Compendium>().SupplyElementIdsForValidation(this.targetId);
        }

        public enum ValueArea
        {
            Aspect, //returns aspect amount from an element token
            Mutation, //return mutation amount from an element token
            SituationContent, //returns aspect amount from a situation token
            AnySourceAspect, //returns aspect amount from any token
            Verb, VerbWild, //retuns a quantity (likely 1) if the token is a verb
            Recipe, RecipeWild, //retuns a quantity (likely 1) if the token is a verb running a recipe
            RecipeAspect, //retuns quantity (likely 1) if the token is a verb running a recipe with the defined aspect
            Token, Payload, Entity, //return token/its payload/payload entity property; incredibly hacky (and probably slow) rn, but work
            NoArea
        };

        private static readonly Dictionary<ValueArea, SingleTokenValue> singleValueGetters = new Dictionary<ValueArea, SingleTokenValue>()
                {
                    { ValueArea.Aspect, AreaOperationsStorage.ElementAspect },
                    { ValueArea.Mutation, AreaOperationsStorage.Mutation },
                    { ValueArea.SituationContent, AreaOperationsStorage.AspectInSituation },
                    { ValueArea.AnySourceAspect, AreaOperationsStorage.AspectOnAnyToken },
                    { ValueArea.Verb, AreaOperationsStorage.VerbId },
                    { ValueArea.VerbWild, AreaOperationsStorage.VerbWild },
                    { ValueArea.Recipe, AreaOperationsStorage.RecipeId },
                    { ValueArea.RecipeWild, AreaOperationsStorage.RecipeWild },
                    { ValueArea.RecipeAspect, AreaOperationsStorage.RecipeAspect },
                    { ValueArea.Token, AreaOperationsStorage.Token },
                    { ValueArea.Payload, AreaOperationsStorage.Payload },
                    { ValueArea.Entity, AreaOperationsStorage.Entity },
                    { ValueArea.NoArea, null },
                };

        private static class AreaOperationsStorage
        {
            public static float ElementAspect(Token token, string target)
            {
                return token.IsValidElementStack() ? token.GetAspects(true).AspectValue(target) : 0;
            }

            public static float Mutation(Token token, string target)
            {
                return token.GetCurrentMutations().TryGetValue(target, out int value) ? value : 0;
            }

            public static float AspectInSituation(Token token, string target)
            {
                return IsSituation(token.Payload) ? token.GetAspects(true).AspectValue(target) : 0;
            }

            public static float AspectOnAnyToken(Token token, string target)
            {
                return token.GetAspects(true).AspectValue(target);
            }

            public static float VerbId(Token token, string target)
            {
                return (IsSituation(token.Payload) && token.PayloadEntityId == target) ? token.Quantity : 0;
            }

            public static float VerbWild(Token token, string target)
            {
                return (IsSituation(token.Payload) && token.PayloadEntityId.Contains(target)) ? token.Quantity : 0;
            }

            public static float RecipeId(Token token, string target)
            {
                return IsSituation(token.Payload) && (token.Payload as Situation).Recipe?.Id == target ? token.Quantity : 0;
            }

            public static float RecipeWild(Token token, string target)
            {
                return IsSituation(token.Payload) && (token.Payload as Situation).Recipe?.Id.Contains(target) == true ? token.Quantity : 0;
            }

            public static float RecipeAspect(Token token, string target)
            {
                return IsSituation(token.Payload) && (token.Payload as Situation).Recipe?.Aspects.ContainsKey(target) == true ? token.Quantity : 0;
            }

            public static float Payload(Token token, string target)
            {
                return (float)token.Payload.GetType().GetProperty(target).GetValue(token.Payload);
            }

            public static float Token(Token token, string target)
            {
                return (float)typeof(Token).GetProperty(target).GetValue(token);
            }

            public static float Entity(Token token, string target)
            {
                if (token.IsValidElementStack())
                {
                    Element element = Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId);
                    return (float)typeof(Element).GetProperty(target).GetValue(element);
                }
                if (IsSituation(token.Payload))
                {
                    Recipe recipe = Watchman.Get<Compendium>().GetEntityById<Recipe>((token.Payload as Situation).RecipeId);
                    return (float)typeof(Recipe).GetProperty(target).GetValue(recipe);
                }

                return 0;
            }
        }

        public enum ValueOperation
        {
            Sum, //sum of values of all tokens
            Num, //value from a token as if it had quantity 1
            Max, Min, //max/min value among all tokens
            Rand, //single value of a random token
            Root, //value from FucineRoot mutations
            Executions, //recipe execution count for the current character - NoArea
            Count, //number of tokens - NoArea and no target
        };

        private static readonly Dictionary<ValueOperation, HandleTokenValues> valueHandlers = new Dictionary<ValueOperation, HandleTokenValues>()
                {
                    { ValueOperation.Sum, ValueOperationsStorage.Sum },
                    { ValueOperation.Num, ValueOperationsStorage.Num },
                    { ValueOperation.Max, ValueOperationsStorage.Max },
                    { ValueOperation.Min, ValueOperationsStorage.Min },
                    { ValueOperation.Rand, ValueOperationsStorage.Rand },
                    { ValueOperation.Root, ValueOperationsStorage.Root },
                    { ValueOperation.Executions, ValueOperationsStorage.Executions },
                    { ValueOperation.Count, ValueOperationsStorage.Count },
                };

        private static class ValueOperationsStorage
        {
            public static float Sum(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                float result = 0;
                foreach (Token token in tokens)
                    result += tokenValue(token, target);
                return result;
            }

            public static float Num(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                float result = 0;
                foreach (Token token in tokens)
                    result += tokenValue(token, target) / token.Quantity;
                return result;
            }

            public static float Max(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                float maxValue = 0; float currentTokenValue;
                foreach (Token token in tokens)
                {
                    currentTokenValue = tokenValue(token, target) / token.Quantity;
                    if (currentTokenValue != 0 && (currentTokenValue > maxValue || (currentTokenValue == maxValue && UnityEngine.Random.Range(0, 99) > 50)))
                        maxValue = currentTokenValue;
                }
                return maxValue;
            }

            public static float Min(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                float minValue = float.MaxValue; float currentTokenValue;
                foreach (Token token in tokens)
                    if (token.IsValidElementStack())
                    {
                        currentTokenValue = tokenValue(token, target) / token.Quantity;
                        if (currentTokenValue != 0 && (currentTokenValue < minValue || (currentTokenValue == minValue && UnityEngine.Random.Range(0, 100) > 50)))
                            minValue = currentTokenValue;
                    }
                return minValue == float.MaxValue ? 0 : minValue;
            }

            public static float Rand(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                int i = UnityEngine.Random.Range(0, tokens.Count - 1);
                return tokenValue(tokens[i], target);
            }

            public static float Count(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                int result = 0;
                foreach (Token token in tokens)
                    result += token.Quantity;
                return result;
            }

            public static float Root(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                return SecretHistories.Assets.Scripts.Application.Entities.NullEntities.FucineRoot.Get().Mutations.TryGetValue(target, out int result) ? result : 0;
            }

            public static float Executions(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                return Watchman.Get<Stable>().Protag().GetExecutionsCount(target);
            }
        }

        private static bool IsSituation(ITokenPayload payload)
        {
            return typeof(Situation).IsAssignableFrom(payload.GetType());
        }
    }
}
