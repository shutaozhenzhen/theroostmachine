using System;
using System.Reflection;
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
    public struct FucineExp<T>
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

                return (T)Roost.Beachcomber.Panimporter.ConvertValue(result, typeof(T));
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
        public readonly FucineValueGetter target;

        public List<Sphere> targetSpheres { get { return Crossroads.GetSpheresByPath(path); } }
        public List<Token> tokens { get { return Crossroads.GetTokensByPath(path).FilterTokens(filter); } }
        public float value { get { return target.GetValueFromTokens(this.tokens); } }

        public FucineRef(string referenceData, string referenceId)
        {
            this.idInExpression = referenceId;
            TwinsParser.ParseFucineRef(referenceData, out path, out filter, out target);
        }

        public FucineRef(string referenceData)
        {
            idInExpression = null;
            TwinsParser.ParseFucineRef(referenceData, out path, out filter, out target);
        }

        public bool Equals(FucineRef otherReference)
        {
            return otherReference.path == this.path && otherReference.filter.formula == this.filter.formula && otherReference.target.Equals(this.target);
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

    public struct FucineValueGetter
    {
        public readonly ValueArea area;
        public readonly ValueOperation operation;
        public readonly string target;

        public delegate float GetTokenValue(Token token, string target);
        public GetTokenValue getTokenValue;

        public delegate float GetResultValue(List<Token> tokens, GetTokenValue getTokenValue, string target);
        GetResultValue getResultValue;

        public float GetValueFromTokens(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return 0;

            return getResultValue(tokens, getTokenValue, target);
        }

        public bool Equals(FucineValueGetter otherValueRef)
        {
            return this.area == otherValueRef.area && this.operation == otherValueRef.operation && this.target == otherValueRef.target;
        }

        public FucineValueGetter(string target) : this(target, ValueArea.Aspect, ValueOperation.Sum) { }

        public FucineValueGetter(string target, ValueArea area, ValueOperation operation)
        {
            this.target = target;
            this.area = area;
            this.operation = operation;

            getTokenValue = allAreaGetters[area];
            getResultValue = allResultGetters[operation];

            if (this.area == ValueArea.Aspect || this.operation == ValueOperation.Root)
                Watchman.Get<Compendium>().SupplyElementIdsForValidation(this.target);
        }

        public enum ValueArea
        {
            Aspect, //returns aspect amount from an element token
            Mutation, //return mutation amount from an element token
            SituationContent, //returns aspect amount from a situation token
            AnySource, //returns aspect amount from any token
            Verb, Recipe, //retuns verb/recipe amount among the tokens
            Token, //returns token property
            Payload, //return token payload property
            //Entity, //should return token payload entity property, but it's a hassle so not implemented
            NoArea
        };

        private static readonly Dictionary<ValueArea, GetTokenValue> allAreaGetters = new Dictionary<ValueArea, GetTokenValue>()
                {
                    { ValueArea.Aspect, AreaOperationsStorage.Aspect },
                    { ValueArea.Mutation, AreaOperationsStorage.Mutation },
                    { ValueArea.Verb, AreaOperationsStorage.Verb },
                    { ValueArea.Recipe, AreaOperationsStorage.Recipe },
                    { ValueArea.SituationContent, AreaOperationsStorage.SituationAspects },
                    { ValueArea.AnySource, AreaOperationsStorage.AspectsFromAnySource },
                    { ValueArea.Payload, AreaOperationsStorage.Payload },
                    { ValueArea.NoArea, null },
                };

        private static class AreaOperationsStorage
        {
            public static float Aspect(Token token, string target)
            {
                return token.IsValidElementStack() ? token.GetAspects().AspectValue(target) : 0;
            }

            public static float SituationAspects(Token token, string target)
            {
                return IsSituation(token.Payload) ? token.GetAspects().AspectValue(target) : 0;
            }

            public static float AspectsFromAnySource(Token token, string target)
            {
                return token.GetAspects().AspectValue(target);
            }

            public static float Mutation(Token token, string target)
            {
                return token.IsValidElementStack() ? token.GetCurrentMutations().TryGetValue(target, out int value) ? value : 0 : 0;
            }

            public static float Verb(Token token, string target)
            {
                return (IsSituation(token.Payload) && token.PayloadEntityId.Contains(target)) ? token.Quantity : 0;
            }

            public static float Recipe(Token token, string target)
            {
                if (IsSituation(token.Payload))
                {
                    Situation situation = token.Payload as Situation;
                    //if recipe id matches, or any of its aspects match
                    if (situation.RecipeId == target || situation.Recipe?.Aspects.ContainsKey(target) == true)
                        return token.Quantity;
                }

                return 0;
            }

            public static float Payload(Token token, string target)
            {
                return (float)token.Payload.GetType().GetProperty(target).GetValue(token.Payload);
            }

            /*     case ValueArea.Token:
                      PropertyInfo targetPropertyInfo = typeof(Token).GetProperty(target, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                      MethodInfo propertyReturnCreator = typeof(FucineValueGetter).GetMethod(nameof(CreatePropertyReturner), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(Token), targetPropertyInfo.PropertyType);

                      return propertyReturnCreator.Invoke(null, new object[] { targetPropertyInfo.GetGetMethod() }) as Func<Token, float>;
            static Func<TClass, float> CreatePropertyReturner<TClass, TProperty>(MethodInfo getMethod)
            {
                try
                {
                    Func<TClass, TProperty> func = getMethod.CreateDelegate(typeof(Func<TClass, TProperty>)) as Func<TClass, TProperty>;
                    return token => (float)Beachcomber.Panimporter.ConvertValue(func(token), typeof(float));
                }
                catch (Exception ex)
                {
                    Birdsong.Tweet(ex.FormatException());
                }

                return null;
            }*/
        }

        public enum ValueOperation
        {
            Sum, //sum of values of all tokens
            Num, //value from a token as if it had quantity 1
            Max, //max value among all tokens
            Min, //min value among all tokens
            Rand, //value of a random token
            Count, //number of tokens
            Root, //value from FucineRoot mutations
            Executions, //recipe execution count for the current character
        };

        private static readonly Dictionary<ValueOperation, GetResultValue> allResultGetters = new Dictionary<ValueOperation, GetResultValue>()
                {
                    { ValueOperation.Sum, ValueOperationsStorage.Sum }, //sum of values of all tokens
                    { ValueOperation.Num, ValueOperationsStorage.Num }, //value from a token as if it had quantity 1
                    { ValueOperation.Max, ValueOperationsStorage.Max }, //max value among all tokens
                    { ValueOperation.Min, ValueOperationsStorage.Min }, //min value among all tokens
                    { ValueOperation.Rand, ValueOperationsStorage.Rand }, //value of a random token
                    { ValueOperation.Count, ValueOperationsStorage.Count }, //number of tokens
                    { ValueOperation.Root, ValueOperationsStorage.Root }, //value from FucineRoot mutations
                    { ValueOperation.Executions, ValueOperationsStorage.Executions }, //recipe execution count for the current character
                };

        private static class ValueOperationsStorage
        {
            public static float Sum(List<Token> tokens, GetTokenValue tokenValue, string target)
            {
                float result = 0;
                foreach (Token token in tokens)
                    result += tokenValue(token, target);
                return result;
            }

            public static float Num(List<Token> tokens, GetTokenValue tokenValue, string target)
            {
                float result = 0;
                foreach (Token token in tokens)
                    result += tokenValue(token, target) / token.Quantity;
                return result;
            }

            public static float Max(List<Token> tokens, GetTokenValue tokenValue, string target)
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

            public static float Min(List<Token> tokens, GetTokenValue tokenValue, string target)
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

            public static float Rand(List<Token> tokens, GetTokenValue tokenValue, string target)
            {
                int i = UnityEngine.Random.Range(0, tokens.Count - 1);
                return tokenValue(tokens[i], target);
            }

            public static float Count(List<Token> tokens, GetTokenValue tokenValue, string target)
            {
                int result = 0;
                foreach (Token token in tokens)
                    result += token.Quantity;
                return result;
            }

            public static float Root(List<Token> tokens, GetTokenValue tokenValue, string target)
            {
                return SecretHistories.Assets.Scripts.Application.Entities.NullEntities.FucineRoot.Get().Mutations.TryGetValue(target, out int result) ? result : 0;
            }

            public static float Executions(List<Token> tokens, GetTokenValue tokenValue, string target)
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
