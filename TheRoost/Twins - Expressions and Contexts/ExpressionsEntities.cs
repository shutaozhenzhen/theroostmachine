using System;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Core;
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

            this.formula = data.Trim();
            try
            {
                this.references = TwinsParser.LoadReferencesForExpression(ref data).ToArray();
                this.expression = new Expression(data);
                if (formula.Contains("Random("))
                    this.expression.EvaluateFunction += NCalcExtensions.Random;
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

        public string targetElement { get { return references[0].valueGetter.target; } }
        public Sphere targetSphere { get { return references[0].targetSpheres.SingleOrDefault(); } }

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
    }

    public struct FucineNumberGetter
    {
        public readonly ValueArea area;
        public readonly ValueOperation operation;
        public readonly bool lever;
        public readonly string targetId;
        public string target { get { return lever ? Elegiast.Scribe.GetLeverForCurrentPlaythrough(targetId) : targetId; } }

        public delegate int SingleTokenValue(Token token, string target);
        public SingleTokenValue GetValue;

        public delegate int HandleTokenValues(List<Token> tokens, SingleTokenValue getTokenValue, string target);
        HandleTokenValues HandleValues;

        public float GetValueFromTokens(List<Token> tokens)
        {
            //NB - not always tokens! sometimes Root or Char data
            return HandleValues(tokens, GetValue, target);
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

            if (!string.IsNullOrEmpty(targetId))
            {
                if (target[target.Length - 1] == '*')
                {
                    if (Enum.TryParse(fromArea.ToString() + "Wild", out ValueArea wildArea))
                    {
                        this.area = wildArea;
                        this.targetId = target.Remove(target.Length - 1);
                    }
                    else
                        Birdsong.TweetLoud($"{fromArea}{withOperation}/{target} - '*' is used, but {fromArea} doesn't support wildcards");
                }

                const string leverMark = "lever_";
                if (targetId.StartsWith(leverMark))
                {
                    lever = true;
                    targetId = targetId.Substring(leverMark.Length);
                }
                else
                    lever = false;

                if (lever == false &&
                    (this.area == ValueArea.Aspect || this.operation == ValueOperation.Root))
                    Watchman.Get<Compendium>().SupplyIdForValidation(typeof(Element), this.targetId);
            }
            else
                lever = false;

            GetValue = AreaOperationsStorage.GetAreaHandler(area);
            HandleValues = ValueOperationsStorage.GetOperationHandler(operation);
        }

        public enum ValueArea
        {
            Aspect, //returns aspect amount from an element token
            Mutation, //return mutation amount from an element token
            SituationContent, //returns aspect amount from a situation token
            AnySourceAspect, //returns aspect amount from any token
            Verb, VerbWild, //retuns a quantity (likely 1) if the token is a verb
            RecipeAspect, //retuns quantity (likely 1) if the token is a verb running a recipe with the defined aspect
            Recipe, RecipeWild, //retuns a quantity (likely 1) if the token is a verb running a recipe
            Token, Payload, Entity, //return token/its payload/payload entity property; incredibly hacky (and probably slow) rn, but work
            NoArea
        };

        public enum ValueOperation
        {
            Sum, //sum of values of all tokens
            Num, //value from a token as if it had quantity 1
            Max, Min, //max/min value among all tokens
            Rand, //single value of a random token
            Root, //value from FucineRoot mutations
            DeckSpecCount, //value from Deck spec
            Executions, //recipe execution count for the current character - NoArea
            Count, //number of tokens - NoArea and no target
        };

        private static class AreaOperationsStorage
        {
            public static SingleTokenValue GetAreaHandler(ValueArea area)
            {
                switch (area)
                {
                    case ValueArea.Aspect: return ElementAspect;
                    case ValueArea.Mutation: return Mutation;
                    case ValueArea.SituationContent: return AspectInSituation;
                    case ValueArea.AnySourceAspect: return AspectOnAnyToken;
                    case ValueArea.Verb: return VerbId;
                    case ValueArea.VerbWild: return VerbWild;
                    case ValueArea.Recipe: return RecipeId;
                    case ValueArea.RecipeWild: return RecipeWild;
                    case ValueArea.RecipeAspect: return RecipeAspect;
                    case ValueArea.Token: return Token;
                    case ValueArea.Payload: return Payload;
                    case ValueArea.Entity: return Entity;
                    case ValueArea.NoArea: return null;
                    default:
                        Birdsong.TweetLoud($"Value area '{area}' doesn't have a matching method; will always return zero");
                        return Zero;
                }
            }

            private static int ElementAspect(Token token, string target)
            {
                return token.IsValidElementStack() ? token.GetAspects(true).AspectValue(target) : 0;
            }

            private static int Mutation(Token token, string target)
            {
                return token.GetCurrentMutations().TryGetValue(target, out int value) ? value : 0;
            }

            private static int AspectInSituation(Token token, string target)
            {
                return IsSituation(token.Payload) ? token.GetAspects(true).AspectValue(target) : 0;
            }

            private static int AspectOnAnyToken(Token token, string target)
            {
                return token.GetAspects(true).AspectValue(target);
            }

            private static int VerbId(Token token, string target)
            {
                return (IsSituation(token.Payload) && token.PayloadEntityId == target) ? token.Quantity : 0;
            }

            private static int VerbWild(Token token, string target)
            {
                return (IsSituation(token.Payload) && token.PayloadEntityId.StartsWith(target)) ? token.Quantity : 0;
            }

            private static int RecipeId(Token token, string target)
            {
                Situation situation = token.Payload as Situation;

                if (situation == null)
                    return 0;

                if (situation.StateIdentifier == StateEnum.Unstarted)
                    return 0;

                if (situation.CurrentRecipe.Id == target)
                    return token.Quantity;

                return 0;
            }

            private static int RecipeWild(Token token, string target)
            {
                Situation situation = token.Payload as Situation;

                if (situation == null)
                    return 0;

                if (situation.StateIdentifier == StateEnum.Unstarted)
                    return 0;

                if (situation.CurrentRecipe.Id.StartsWith(target))
                    return token.Quantity;

                return 0;
            }

            private static int RecipeAspect(Token token, string target)
            {
                if (!IsSituation(token.Payload))
                    return 0;

                return (token.Payload as Situation).CurrentRecipe.Aspects.AspectValue(target);
            }

            private static int Payload(Token token, string target)
            {

                object value = token.Payload.GetType().GetProperty(target).GetValue(token.Payload);

                try
                {
                    return ConvertToInt(value);
                }
                catch (Exception ex)
                {
                    Birdsong.TweetLoud($"Unable to parse property '{target}' of '{token.Payload}': {ex.FormatException()}");
                    return 0;
                }
            }

            private static int Token(Token token, string target)
            {
                object value = token.Payload.GetType().GetProperty(target).GetValue(token.Payload);

                try
                {
                    return ConvertToInt(value);
                }
                catch (Exception ex)
                {
                    Birdsong.TweetLoud($"Unable to parse property '{target}' of '{token.Payload}': {ex.FormatException()}");
                    return 0;
                }
            }

            private static int Entity(Token token, string target)
            {
                if (token.IsValidElementStack())
                {
                    Element element = Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId);

                    object value = typeof(Element).GetProperty(target).GetValue(element);

                    try
                    {
                        return ConvertToInt(value);
                    }
                    catch (Exception ex)
                    {
                        Birdsong.TweetLoud($"Unable to parse property '{target}' of '{element}': {ex.FormatException()}");
                        return 0;
                    }
                }

                if (IsSituation(token.Payload))
                {
                    Recipe recipe = Watchman.Get<Compendium>().GetEntityById<Recipe>((token.Payload as Situation).RecipeId);
                    object value = typeof(Recipe).GetProperty(target).GetValue(recipe);

                    try
                    {
                        return ConvertToInt(value);
                    }
                    catch (Exception ex)
                    {
                        Birdsong.TweetLoud($"Unable to parse property '{target}' of '{recipe}': {ex.FormatException()}");
                        return 0;
                    }
                }

                return 0;
            }

            private static int Zero(Token token, string target)
            {
                return 0;
            }

            private static int ConvertToInt(object value)
            {
                if (value is int intValue)
                    return intValue;

                if (value is float floatValue)
                    return (int)floatValue * 100;

                try
                {
                    return (int)ImportMethods.ConvertValue(value, typeof(int));
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        private static class ValueOperationsStorage
        {
            public static HandleTokenValues GetOperationHandler(ValueOperation operation)
            {
                switch (operation)
                {
                    case ValueOperation.Sum: return Sum;
                    case ValueOperation.Num: return Num;
                    case ValueOperation.Max: return Max;
                    case ValueOperation.Min: return Min;
                    case ValueOperation.Rand: return Rand;
                    case ValueOperation.Root: return Root;
                    //case ValueOperation.DeckSpec: return DeckSpec;
                    case ValueOperation.DeckSpecCount: return DeckSpecCount;
                    case ValueOperation.Executions: return Executions;
                    case ValueOperation.Count: return Count;

                    default:
                        Birdsong.TweetLoud($"Value operation {operation} doesn't have a matching method; will always return zero");
                        return Zero;
                }
            }

            private static int Sum(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int result = 0;
                foreach (Token token in tokens)
                    result += tokenValue(token, target);
                return result;
            }

            public static int Num(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int result = 0;
                foreach (Token token in tokens)
                    result += tokenValue(token, target) / token.Quantity;
                return result;
            }

            private static int Max(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int maxValue = 0; int currentTokenValue;
                foreach (Token token in tokens)
                {
                    currentTokenValue = tokenValue(token, target) / token.Quantity;
                    if (currentTokenValue != 0 && (currentTokenValue > maxValue || (currentTokenValue == maxValue && UnityEngine.Random.Range(0, 99) > 50)))
                        maxValue = currentTokenValue;
                }
                return maxValue;
            }

            private static int Min(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int minValue = int.MaxValue; int currentTokenValue;
                foreach (Token token in tokens)
                    if (token.IsValidElementStack())
                    {
                        currentTokenValue = tokenValue(token, target) / token.Quantity;
                        if (currentTokenValue != 0 && (currentTokenValue < minValue || (currentTokenValue == minValue && UnityEngine.Random.Range(0, 100) > 50)))
                            minValue = currentTokenValue;
                    }
                return minValue == int.MaxValue ? 0 : minValue;
            }

            private static int Rand(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int i = UnityEngine.Random.Range(0, tokens.Count - 1);
                return tokenValue(tokens[i], target);
            }

            private static int Count(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int result = 0;
                foreach (Token token in tokens)
                    result += token.Quantity;
                return result;
            }

            private static int Root(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                return FucineRoot.Get().Mutations.TryGetValue(target, out int result) ? result : 0;
            }

            private static int DeckSpec(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                var deck = Watchman.Get<Compendium>().GetEntityById<DeckSpec>(target);
                if (deck == null)
                {
                    Birdsong.TweetLoud($"Trying to access non-existent deck spec {target}");
                    return 0;
                }

                AspectsDictionary specAspects = new AspectsDictionary();
                foreach (string elementId in deck.Spec)
                {
                    Element element = Watchman.Get<Compendium>().GetEntityById<Element>(elementId);
                    if (element.IsValid())
                        specAspects.CombineAspects(element.Aspects);
                }

                //how do we pass an element id............
                return specAspects.AspectValue(target);
            }

            private static int DeckSpecCount(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                var deck = Watchman.Get<Compendium>().GetEntityById<DeckSpec>(target);
                if (deck == null)
                {
                    Birdsong.TweetLoud($"Trying to access non-existent deck spec {target}");
                    return 0;
                }

                return deck.Spec.Count;
            }

            private static int Executions(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                return Watchman.Get<Stable>().Protag().GetExecutionsCount(target);
            }

            private static int Zero(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                return 0;
            }
        }

        private static bool IsSituation(ITokenPayload payload)
        {
            return typeof(Situation).IsAssignableFrom(payload.GetType());
        }
    }

    internal static class NCalcExtensions
    {
        internal static void Random(string name, FunctionArgs functionArgs)
        {
            if (name != "Random")
                return;

            //faster without?
            /*if (functionArgs.Parameters.Length == 1)
            {
                functionArgs.Result = UnityEngine.Random.Range(0, functionArgs.Parameters[0].Evaluate().ConvertTo<int>());
                return;
            }*/
            if (functionArgs.Parameters.Length == 2)
            {
                functionArgs.Result = UnityEngine.Random.Range(functionArgs.Parameters[0].Evaluate().ConvertTo<int>(), functionArgs.Parameters[1].Evaluate().ConvertTo<int>());
                return;
            }

            if (functionArgs.Parameters.Length == 1)
                throw Birdsong.Cack($"Not enough parameters in Random({functionArgs.Parameters.UnpackCollection(exp => (exp as Expression).Evaluate(), ",")})");

            throw Birdsong.Cack($"Too many parameters in Random({functionArgs.Parameters.UnpackCollection(exp => (exp as Expression).Evaluate(), ",")})");
        }
    }
}
