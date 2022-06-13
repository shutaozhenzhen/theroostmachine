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
        public enum ValueArea
        {
            Aspect, Aspects, //returns aspect amount from the tokens
            Mutation, Mutations, //return mutation amount of the target aspect
            Verb, Recipe, //retuns verb/recipe amount among the tokens
            Token, //returns token property
            Payload, //return token payload property
            Entity, //should return token payload entity property, but it's a hassle so not implemented
            Targetless //currently only $count
        };
        public enum ValueOperation
        {
            Sum, //sum of all returned values
            Num, //sum of all returned values, each divided by token quantity
            Max, Min,
            Rand, //value from random token
            Root, //returns FucineRoot mutation amount of the specified target (on the first glance, it may look like a value area, but it isn't!
            Executions, //
            Count, //targetless, returns amount of tokens
        };

        Func<List<Token>, float> GetResult;

        public float GetValueFromTokens(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return 0;

            return GetResult(tokens);
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

            Func<Token, float> GetTokenValue = GetTokenValueGetter(area, target);
            GetResult = GetResultGetter(operation, GetTokenValue, target);

            if (this.area == ValueArea.Aspect || this.area == ValueArea.Aspects || this.operation == ValueOperation.Root)
                Watchman.Get<Compendium>().SupplyElementIdsForValidation(this.target);
        }

        private static Func<Token, float> GetTokenValueGetter(ValueArea area, string target)
        {
            switch (area)
            {
                default:
                case ValueArea.Aspect:
                case ValueArea.Aspects:
                    return token => token.IsValidElementStack() ? token.GetAspects().AspectValue(target) : 0;
                case ValueArea.Mutation:
                case ValueArea.Mutations:
                    return token => token.IsValidElementStack() ? token.GetCurrentMutations().TryGetValue(target, out int value) ? value : 0 : 0;
                case ValueArea.Verb:
                    return token => IsSituation(token.Payload) && token.PayloadEntityId == target ? token.Quantity : 0;
                case ValueArea.Recipe:
                    return token =>
                    {
                        if (IsSituation(token.Payload))
                        {
                            Situation situation = token.Payload as Situation;
                            //if recipe id matches, or any of its aspects match
                            if (situation.RecipeId == target || situation.Recipe.Aspects.ContainsKey(target) == true)
                                return token.Quantity;
                        }

                        return 0;
                    };
                case ValueArea.Token:
                    PropertyInfo targetPropertyInfo = typeof(Token).GetProperty(target, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    MethodInfo propertyReturnCreator = typeof(FucineValueGetter).GetMethod(nameof(CreatePropertyReturner), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(Token), targetPropertyInfo.PropertyType);

                    return propertyReturnCreator.Invoke(null, new object[] { targetPropertyInfo.GetGetMethod() }) as Func<Token, float>;
                case ValueArea.Payload:
                    return token => (float)token.Payload.GetType().GetProperty(target).GetValue(token.Payload);
                case ValueArea.Entity:
                    throw new NotImplementedException();
            }
        }

        private static Func<List<Token>, float> GetResultGetter(ValueOperation operation, Func<Token, float> GetTokenValue, string target)
        {
            switch (operation)
            {
                default:
                case ValueOperation.Num:
                    //sum of values of all tokens
                    return tokens =>
                    {
                        float result = 0;
                        foreach (Token token in tokens)
                            result += GetTokenValue(token) / token.Quantity;
                        return result;
                    };
                case ValueOperation.Sum:
                    //sum of values of all tokens
                    return tokens =>
                    {
                        float result = 0;
                        foreach (Token token in tokens)
                            result += GetTokenValue(token);
                        return result;
                    };
                case ValueOperation.Max:
                    //max value among all tokens
                    return tokens =>
                    {
                        float maxValue = 0; float currentTokenValue;
                        foreach (Token token in tokens)
                        {
                            currentTokenValue = GetTokenValue(token) / token.Quantity;
                            if (currentTokenValue != 0 && (currentTokenValue > maxValue || (currentTokenValue == maxValue && UnityEngine.Random.Range(0, 99) > 50)))
                                maxValue = currentTokenValue;
                        }
                        return maxValue;
                    };
                case ValueOperation.Min:
                    //min value among all tokens
                    return tokens =>
                    {
                        float minValue = float.MaxValue; float currentTokenValue;
                        foreach (Token token in tokens)
                            if (token.IsValidElementStack())
                            {
                                currentTokenValue = GetTokenValue(token) / token.Quantity;
                                if (currentTokenValue != 0 && (currentTokenValue < minValue || (currentTokenValue == minValue && UnityEngine.Random.Range(0, 100) > 50)))
                                    minValue = currentTokenValue;
                            }
                        return minValue == float.MaxValue ? 0 : minValue;
                    };
                case ValueOperation.Rand:
                    //value of a random token
                    return tokens =>
                    {
                        int i = UnityEngine.Random.Range(0, tokens.Count - 1);
                        return GetTokenValue(tokens[i]);
                    };
                case ValueOperation.Count:
                    //number of tokens
                    return tokens =>
                    {
                        int result = 0;
                        foreach (Token token in tokens)
                            result += token.Quantity;
                        return result;
                    };
                case ValueOperation.Root:
                    //number of tokens
                    return tokens =>
                    {
                        return SecretHistories.Assets.Scripts.Application.Entities.NullEntities.FucineRoot.Get().Mutations.TryGetValue(target, out int result) ? result : 0;
                    };
                case ValueOperation.Executions:
                    return tokens =>
                    {
                        return Watchman.Get<Stable>().Protag().GetExecutionsCount(target);
                    };
            }
        }

        private static bool IsSituation(ITokenPayload payload)
        {
            return typeof(Situation).IsAssignableFrom(payload.GetType());
        }

        //WIP
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
        }
    }
}
