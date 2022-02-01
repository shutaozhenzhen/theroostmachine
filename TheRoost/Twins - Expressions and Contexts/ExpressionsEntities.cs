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
        public delegate List<Token> SphereTokenGet();

        public readonly string idInExpression;
        public readonly string targetElementId;
        public readonly SphereTokenGet getTargetTokens;
        public readonly Funcine<bool> tokensFilter;
        
        public enum SpecialOperation { SingleMin, SingleMax, CountOfAny };

        public int value
        {
            get
            {
                List<Token> tokens = getTargetTokens.Invoke();

                if (this.tokensFilter.isUndefined == false)
                    tokens = tokens.FilterTokens(tokensFilter);

                AspectsDictionary aspects = new AspectsDictionary();
                foreach (Token token in tokens)
                    aspects.CombineAspects(token.GetAspects());

                return aspects.AspectValue(targetElementId);
            }
        }

        public FuncineRef(string referenceData, string referenceId)
        {
            this.idInExpression = referenceId;
            FuncineParser.PopulateFucineReference(referenceData, out targetElementId, out tokensFilter, out getTargetTokens);
        }

        public bool Equals(FuncineRef otherReference)
        {
            return otherReference.getTargetTokens == this.getTargetTokens && otherReference.targetElementId == this.targetElementId && otherReference.tokensFilter.isUndefined && this.tokensFilter.isUndefined;
        }
    }
}
