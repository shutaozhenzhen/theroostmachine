using System.Collections.Generic;
using SecretHistories.Core;
using SecretHistories.UI;
using NCalc;

namespace TheRoost.Twins.Entities
{
    public struct Funcine<T>
    {
        readonly Expression expression;
        readonly FuncineRef[] references;
        public readonly string formula;

        public Funcine(string expression)
        {
            this.formula = expression;
            this.references = FuncineParser.LoadReferences(ref expression).ToArray();
            this.expression = new Expression(Expression.Compile(expression, false));
        }

        public T result
        {
            get
            {
                foreach (FuncineRef reference in references)
                    expression.Parameters[reference.idInExpression] = reference.value;
                object result = expression.Evaluate();

                return (T)TheRoost.Beachcomber.Panimporter.ConvertValue(result, typeof(T));
            }
        }

        public bool isUndefined { get { return this.expression == null; } }

        public override string ToString()
        {
            if (isUndefined)
                return "undefined expression";
            return "'" + this.formula + "' = " + this.result;
        }
    }

    public struct FuncineRef
    {
        public delegate List<Token> SphereTokensRef();

        public readonly string idInExpression;
        public readonly string targetElementId;
        public readonly SphereTokensRef GetTargetTokens;
        public readonly Funcine<bool> tokensFilter;
        public readonly SpecialOperation special;

        public enum SpecialOperation { None, SingleLowest, SingleHighest, CountCards, PayloadProperty, ElementProperty };
        public const string opCountKeyword = "$any";
        public const string opMaxKeyword = "$max";
        public const string opMinKeyword = "$min";

        public int value
        {
            get
            {
                List<Token> tokens = GetTargetTokens();

                //!~!!!!!!!!!!!!!!!!!!!!!NB temp and dirty solution
                foreach (Token token in tokens.ToArray())
                    if (token.PayloadEntityId == "tlg.note")
                        tokens.Remove(token);

                if (this.tokensFilter.isUndefined == false)
                    tokens = tokens.FilterTokens(tokensFilter);

                switch (special)
                {
                    case SpecialOperation.None:
                        AspectsDictionary aspects = new AspectsDictionary();
                        foreach (Token token in tokens)
                            aspects.CombineAspects(token.GetAspects());
                        return aspects.AspectValue(targetElementId);

                    case SpecialOperation.SingleHighest:
                        int maxValue = 0; int currentTokenValue;
                        foreach (Token token in tokens)
                        {
                            currentTokenValue = token.GetAspects().AspectValue(targetElementId);
                            if (currentTokenValue > maxValue)
                                maxValue = currentTokenValue;
                        }
                        return maxValue;
                    case SpecialOperation.SingleLowest:
                        int minValue = int.MaxValue;
                        foreach (Token token in tokens)
                        {
                            currentTokenValue = token.GetAspects().AspectValue(targetElementId);
                            if (currentTokenValue != 0 && currentTokenValue < minValue)
                                minValue = currentTokenValue;
                        }
                        
                        return minValue == int.MaxValue ? 0 : minValue;
                    case SpecialOperation.CountCards:
                        int amount = 0;
                        foreach (Token token in tokens)
                            amount += (token.Payload as ElementStack).Quantity;
                            return amount;
                    default: throw Birdsong.Cack("Something strange happened. Unknown reference special operation '{0}'", special);
                }
            }
        }

        public FuncineRef(string referenceData, string referenceId)
        {
            this.idInExpression = referenceId;
            FuncineParser.PopulateFucineReference(referenceData, out targetElementId, out tokensFilter, out GetTargetTokens, out special);
        }

        public bool Equals(FuncineRef otherReference)
        {
            return otherReference.GetTargetTokens == this.GetTargetTokens && otherReference.targetElementId == this.targetElementId && otherReference.tokensFilter.isUndefined && this.tokensFilter.isUndefined;
        }
    }

    public struct SphereRef
    {
        public SphereRef(string reference) { GetSphere = FuncineParser.GetSphereRef(reference); }

        private System.Func<SecretHistories.Spheres.Sphere> GetSphere;
        public SecretHistories.Spheres.Sphere target { get { return GetSphere(); } }
    }
}
