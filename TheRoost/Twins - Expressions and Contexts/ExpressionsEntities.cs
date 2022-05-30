using System;
using System.Reflection;
using System.Collections.Generic;

using SecretHistories.Core;
using SecretHistories.UI;
using NCalc;
using SecretHistories.Fucine;

namespace Roost.Twins.Entities
{
    public struct Funcine<T>
    {
        readonly Expression expression;
        readonly FuncineRef[] references;
        public readonly string formula;

        public Funcine(string stringExpression)
        {
            try
            {
                this.formula = stringExpression;
                this.references = FuncineParser.LoadReferences(ref stringExpression).ToArray();
                this.expression = new Expression(Expression.Compile(stringExpression, false));
            }
            catch (Exception ex)
            {
                throw ex;
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
        public static Funcine<T> one { get { return new Funcine<T>("1"); } }

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
                List<Token> tokens = TokenContextAccessors.GetTokensByPath(path).FilterTokens(filter);

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

    public struct TokenValueRef
    {
        public readonly ValueArea area;
        public readonly ValueOperation operation;
        public readonly string target;
        public enum ValueArea { Aspect, Aspects, Token, Payload, Entity, Special };
        public enum ValueOperation { Count, Sum, Max, Min, Rand };

        Func<List<Token>, float> resultGet;

        public float GetValueFromTokens(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return 0;

            return resultGet(tokens);
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

            Func<Token, float> tokenValue;
            switch (area)
            {
                default:
                case ValueArea.Aspect:
                case ValueArea.Aspects:
                    tokenValue = token => token.GetAspects().AspectValue(target) / token.Quantity;
                    break;
                case ValueArea.Token:
                    PropertyInfo targetPropertyInfo = typeof(Token).GetProperty(target, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    MethodInfo propertyReturnCreator = typeof(TokenValueRef).GetMethod(nameof(CreatePropertyReturner), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(Token), targetPropertyInfo.PropertyType);

                    tokenValue = propertyReturnCreator.Invoke(null, new object[] { targetPropertyInfo.GetGetMethod() }) as Func<Token, float>;
                    break;
                case ValueArea.Payload:
                    tokenValue = token => (float)token.Payload.GetType().GetProperty(target).GetValue(token.Payload);
                    break;
            }

            switch (operation)
            {
                default:
                case ValueOperation.Sum:
                    //sum of values of all tokens
                    resultGet = tokens =>
                    {
                        float result = 0;
                        foreach (Token token in tokens)
                            result += tokenValue(token);
                        return result;
                    };
                    break;
                case ValueOperation.Max:
                    //max value among all tokens
                    resultGet = tokens =>
                    {
                        float maxValue = 0; float currentTokenValue;
                        foreach (Token token in tokens)
                        {
                            currentTokenValue = tokenValue(token);
                            if (currentTokenValue != 0 && (currentTokenValue > maxValue || (currentTokenValue == maxValue && UnityEngine.Random.Range(0, 99) > 50)))
                                maxValue = currentTokenValue;
                        }
                        return maxValue;
                    };
                    break;
                case ValueOperation.Min:
                    //min value among all tokens
                    resultGet = tokens =>
                    {
                        float minValue = float.MaxValue; float currentTokenValue;
                        foreach (Token token in tokens)
                        {
                            currentTokenValue = tokenValue(token);
                            if (currentTokenValue != 0 && (currentTokenValue < minValue || (currentTokenValue == minValue && UnityEngine.Random.Range(0, 100) > 50)))
                                minValue = currentTokenValue;
                        }
                        return minValue == float.MaxValue ? 0 : minValue;
                    };
                    break;
                case ValueOperation.Rand:
                    //value of a random token
                    resultGet = tokens =>
                    {
                        int i = UnityEngine.Random.Range(0, tokens.Count - 1);
                        return tokenValue(tokens[i]);
                    };
                    break;
                case ValueOperation.Count:
                    //number of tokens
                    resultGet = tokens =>
                    {
                        int result = 0;
                        foreach (Token token in tokens)
                            result += token.Quantity;
                        return result;
                    };
                    break;
            }

            if (this.area == ValueArea.Aspect || this.area == ValueArea.Aspects)
                Watchman.Get<Compendium>().SupplyElementIdsForValidation(this.target);
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
