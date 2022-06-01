using System;
using System.Reflection;
using System.Collections.Generic;

using SecretHistories.Abstract;
using SecretHistories.Spheres;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Enums;
using SecretHistories.Fucine;

using NCalc;

using Roost.World.Recipes;
using Roost.World.Recipes.Entities;

namespace Roost.Twins.Entities
{
    public struct Funcine<T>
    {
        readonly Expression expression;
        readonly FuncineRef[] references;
        public readonly string formula;

        public Funcine(string stringExpression)
        {
            this.formula = stringExpression;
            try
            {
                this.references = FuncineParser.LoadReferences(ref stringExpression).ToArray();
                this.expression = new Expression(Expression.Compile(stringExpression, false));
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
                foreach (FuncineRef reference in references)
                    expression.Parameters[reference.idInExpression] = reference.value;
                object result = expression.Evaluate();

                return (T)Roost.Beachcomber.Panimporter.ConvertValue(result, typeof(T));
            }
        }

        public static implicit operator Funcine<T>(string formula) { return new Funcine<T>(formula); }

        public bool isUndefined { get { return this.expression == null; } }

        public override string ToString()
        {
            if (isUndefined)
                return "undefined expression";
            return "'" + this.formula + "' = " + this.value;
        }
    }

    public struct FuncineRef
    {
        public delegate List<Token> SphereTokensRef();

        public readonly string idInExpression;

        public readonly FucinePath path;
        public readonly Funcine<bool> filter;
        public readonly TokenValueRef target;

        public float value
        {
            get
            {
                List<Token> tokens = Crossroads.GetTokensByPath(path).FilterTokens(filter);

                return target.GetValueFromTokens(tokens);
            }
        }

        public FuncineRef(string referenceId, FucinePath path, Funcine<bool> filter, TokenValueRef target)
        {
            this.idInExpression = referenceId;
            this.path = path;
            this.filter = filter;
            this.target = target;
        }

        public bool Equals(FuncineRef otherReference)
        {
            return otherReference.path == this.path && otherReference.filter.formula == this.filter.formula && otherReference.target.Equals(this.target);
        }
    }

    public class FucinePathPlus : FucinePath
    {
        public FucinePathPlus(string path, int maxSpheresToFind, List<SphereCategory> acceptable = null, List<SphereCategory> excluded = null) : base(path)
        {
            this.maxSpheresToFind = maxSpheresToFind;

            acceptableCategories = acceptable ?? defaultAcceptableCategories;
            excludedSphereCategories = excluded ?? defaultExcludedCategories;

            if (this.IsAbsolute())
                GetRelevantSpherePath = getAbsolutePath;
            else
                GetRelevantSpherePath = getWildPath;
        }

        public readonly int maxSpheresToFind;

        public List<SphereCategory> acceptableCategories;
        public List<SphereCategory> excludedSphereCategories;

        private static readonly List<SphereCategory> defaultAcceptableCategories = new List<SphereCategory>((SphereCategory[])Enum.GetValues(typeof(SphereCategory)));
        private static readonly List<SphereCategory> defaultExcludedCategories = new List<SphereCategory> { SphereCategory.Notes };

        private static readonly Func<Sphere, string> getAbsolutePath = sphere => sphere.GetAbsolutePath().ToString();
        private static readonly Func<Sphere, string> getWildPath = sphere => sphere.GetWildPath().ToString();
        private Func<Sphere, string> GetRelevantSpherePath;

        public List<Sphere> GetSpheresSpecial()
        {
            List<Sphere> result = new List<Sphere>();
            string pathMask = this.ToString().ToLower();
            int maxAmount = maxSpheresToFind;

            foreach (Sphere sphere in Watchman.Get<HornedAxe>().GetSpheres())
                if (!excludedSphereCategories.Contains(sphere.SphereCategory) && acceptableCategories.Contains(sphere.SphereCategory) && !result.Contains(sphere)
                    && GetRelevantSpherePath(sphere).ToLower().Contains(pathMask))
                {
                    result.Add(sphere);

                    maxAmount--;
                    if (maxAmount == 0)
                        break;
                }

            return result;
        }
    }

    public struct TokenValueRef
    {
        public readonly ValueArea area;
        public readonly ValueOperation operation;
        public readonly string target;
        public enum ValueArea
        {
            Aspect, Aspects, //returns aspect amount from the tokens
            Verb, Recipe, //retuns verb/recipe amount among the tokens
            Token, //returns token property
            Payload, //return token payload property
            Entity, //should return token payload entity property, but it's a hassle so not implemented
            Special //currently only $count
        };
        public enum ValueOperation
        {
            Sum,
            Max, Min,
            Rand, //value from random token
            Count, //returns amount of tokens
            Root //returns FucineRoot mutation amount of the specified target (on the first glance, it may look like a value area, but it isn't!
        };

        Func<List<Token>, float> GetResult;

        public float GetValueFromTokens(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return 0;

            return GetResult(tokens);
        }

        public bool Equals(TokenValueRef otherValueRef)
        {
            return this.area == otherValueRef.area && this.operation == otherValueRef.operation && this.target == otherValueRef.target;
        }

        public TokenValueRef(string target) : this(target, ValueArea.Aspect, ValueOperation.Sum) { }

        public TokenValueRef(string target, ValueArea area, ValueOperation operation)
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
                                return 1;
                        }

                        return 0;
                    };
                case ValueArea.Token:
                    PropertyInfo targetPropertyInfo = typeof(Token).GetProperty(target, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    MethodInfo propertyReturnCreator = typeof(TokenValueRef).GetMethod(nameof(CreatePropertyReturner), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(Token), targetPropertyInfo.PropertyType);

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
            }
        }

        private static bool IsSituation(ITokenPayload payload)
        {
            return typeof(Situation).IsAssignableFrom(payload.GetType());
        }

        static Func<TClass, float> CreatePropertyReturner<TClass, TProperty>(MethodInfo getMethod)
        {
            try
            {
                Func<TClass, TProperty> func = getMethod.CreateDelegate(typeof(Func<TClass, TProperty>)) as Func<TClass, TProperty>;
                return token => (float)Beachcomber.Panimporter.ConvertValue(func(token), typeof(float));
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex.FormatException());
            }

            return null;
        }
    }
}
