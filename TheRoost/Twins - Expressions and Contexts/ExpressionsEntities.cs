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

    public class FucinePathPlus : FucinePath, IConvertible //dont ask
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

        //dont ask
        public override string ToString() { return fullPath; }
        public string ToString(IFormatProvider provider) { return fullPath; }
        public TypeCode GetTypeCode() { return default(TypeCode); }
        public bool ToBoolean(IFormatProvider provider) { return default(bool); }
        public byte ToByte(IFormatProvider provider) { return default(byte); }
        public char ToChar(IFormatProvider provider) { return default(char); }
        public DateTime ToDateTime(IFormatProvider provider) { return default(DateTime); }
        public decimal ToDecimal(IFormatProvider provider) { return default(decimal); }
        public double ToDouble(IFormatProvider provider) { return default(double); }
        public short ToInt16(IFormatProvider provider) { return default(short); }
        public int ToInt32(IFormatProvider provider) { return default(int); }
        public long ToInt64(IFormatProvider provider) { return default(long); }
        public sbyte ToSByte(IFormatProvider provider) { return default(sbyte); }
        public float ToSingle(IFormatProvider provider) { return default(ulong); }
        public object ToType(Type conversionType, IFormatProvider provider) { return this; }
        public ushort ToUInt16(IFormatProvider provider) { return default(ushort); }
        public uint ToUInt32(IFormatProvider provider) { return default(uint); }
        public ulong ToUInt64(IFormatProvider provider) { return default(ulong); }
    }

    public struct FucineNumberGetter
    {
        public readonly ValueArea area;
        public readonly ValueOperation operation;
        public readonly bool lever;
        public readonly string targetId;
        public string target { get { return lever ? Elegiast.Scribe.GetLeverForCurrentPlaythrough(targetId) : targetId; } }

        public delegate float SingleTokenValue(Token token, string target);
        public SingleTokenValue GetValue;

        public delegate float HandleTokenValues(List<Token> tokens, SingleTokenValue getTokenValue, string target);
        HandleTokenValues HandleValues;

        public float GetValueFromTokens(List<Token> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return 0;

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

            if (target[target.Length - 1] == '*')
            {
                if (Enum.TryParse(fromArea.ToString() + "Wild", out ValueArea wildArea))
                {
                    this.area = wildArea;
                    this.targetId = target.Remove(target.Length - 1);
                }
                else
                    Birdsong.Tweet(VerbosityLevel.Essential, 1, $"{fromArea}{withOperation}/{target} - '*' is used, but {fromArea} doesn't support wildcards");
            }

            const string leverMark = "lever_";
            if (targetId.StartsWith(leverMark))
            {
                lever = true;
                targetId = targetId.Substring(leverMark.Length);
            }
            else
                lever = false;

            GetValue = singleValueGetters[this.area];
            HandleValues = valueHandlers[this.operation];

            if (lever == false &&
                (this.area == ValueArea.Aspect || this.operation == ValueOperation.Root))
                Watchman.Get<Compendium>().SupplyElementIdsForValidation(this.targetId);
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
                return (IsSituation(token.Payload) && token.PayloadEntityId.StartsWith(target)) ? token.Quantity : 0;
            }

            public static float RecipeId(Token token, string target)
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

            public static float RecipeWild(Token token, string target)
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

            public static float RecipeAspect(Token token, string target)
            {
                if (!IsSituation(token.Payload))
                    return 0;

                return (token.Payload as Situation).CurrentRecipe.Aspects.AspectValue(target);
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
                return FucineRoot.Get().Mutations.TryGetValue(target, out int result) ? result : 0;
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
            else
                throw Birdsong.Cack($"Too many parameters in Random({functionArgs.Parameters.UnpackCollection(exp => (exp as Expression).Evaluate(), ",")})");
        }
    }
}
