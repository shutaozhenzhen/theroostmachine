using System;
using System.Linq;

using SecretHistories.Spheres;
using SecretHistories.UI;

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
                this.expression = new Expression(data, EvaluateOptions.BooleanCalculation | EvaluateOptions.IgnoreCase);

                if (NCalcExtensions.ExpressionUsesExtensions(this.formula))
                    this.expression.EvaluateFunction += NCalcExtensions.HandleNCalcExtensions;
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

        public string targetElement => references[0].valueGetter.targetId;
        public Sphere targetSphere => references[0].targetSpheres.SingleOrDefault();

        public static implicit operator FucineExp<T>(string formula) { return new FucineExp<T>(formula); }

        public bool isUndefined => expression == null;

        public override string ToString()
        {
            if (isUndefined)
                return UNDEFINED;
            return "'" + this.formula + "' = " + this.value;
        }

        public bool isSimpleNumber()
        {
            return references.Length == 0;
        }


    }
}

namespace Roost.Twins
{
    using Roost.Twins.Entities;

    public static class ExpressionExtensions
    {
        public static bool Matches(this FucineExp<bool> expression, Token token)
        {
            if (expression.isUndefined)
                return true;

            //filtering happens only when we already reset the crossroads for the current context
            Crossroads.MarkAllLocalTokens(token);
            Crossroads.MarkLocalToken(token);
            bool result = expression.value;
            Crossroads.UnmarkAllLocalTokens();

            return result;
        }
    }
}
