using System;
using System.Collections.Generic;
using TheRoost;
using NCalc;

namespace TheRoost.Entities
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class FucineExpression : Attribute
    {
        static readonly string[] operationSeparators = new string[] { "(", ")", "+", "-", "*", "\\", "/", "%", "&", "||", "|", "!=", ">=", "<=", ">", "<", "=", };
        const char scopeSeparator = '@';
        public static Expression ParseAndCompile(string expression)
        {
            string[] expressionParts = expression.Split(operationSeparators, StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            int referenceLetter = 0;
            for (var n = 0; n < expressionParts.Length; n++)
                if (expressionParts[n].Length > 0 && Char.IsLetter(expressionParts[n][0]))
                {
                    parameters[ToLetter(referenceLetter)] = CreateReference(expressionParts[n].Split(scopeSeparator));
                    expression = expression.Replace(expressionParts[n], ToLetter(referenceLetter));
                    referenceLetter++;
                }

            Expression result = new Expression(Expression.Compile(expression, false));
            result.Parameters = parameters;
            return result;
        }

        ///this one assumes more strict reference syntax, all enclosed in @@: @variable@, @scope#variable@, @scope#scopeId#varialble
        ///while the new one doesn't need enclosing and only needs separator to define scopes: variable, variable@scope, variable@scopeId@scope
        ///but in case it'll new bites me in the ass later I'll leave this one be
        /*
        const char referenceSeparator = '@';
        const char scopeSeparator = '#';
        public static Expression ParseAndCompileOld(string expression)
        {         
            string[] expressionParts = expression.Split(referenceSeparator);
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            int referencesCount = 0;
            for (var n = 0; n < expressionParts.Length; n++)
                if (expressionParts[n].Length > 0 && Char.IsLetter(expressionParts[n][0]))
                {
                    parameters[ToLetter(referencesCount)] = CreateReference(expressionParts[n].Split(scopeSeparator));
                    expressionParts[n] = ToLetter(referencesCount);
                    referencesCount++;
                }

            Expression result = new Expression(Expression.Compile(String.Concat(expressionParts), false));
            result.Parameters = parameters;
            return result;            
        }
         */

        public static T Evaluate<T>(Expression expression, Func<object, T> castToTargetType)
        {
            for (int n = 0; n < expression.Parameters.Count; n++)
                expression.Parameters[ToLetter(n)] = ((FucineReference)expression.Parameters[ToLetter(n)]).value;
            object result = expression.Evaluate();

            return (T)castToTargetType.Invoke(result);
        }

        static FucineReference CreateReference(string[] reference)
        {
            if (reference.Length == 1)
                return new FucineReference(TheWorld.GetContextType("default"), string.Empty, reference[0]);
            else if (reference.Length == 2)
                return new FucineReference(TheWorld.GetContextType(reference[0]), string.Empty, reference[1]);
            else if (reference.Length == 3)
                return new FucineReference(TheWorld.GetContextType(reference[0]), reference[1], reference[2]);
            else
            {
                Birdsong.Sing("Malformed reference {0}", String.Concat(reference));
                return FucineReference.unknown;
            }
        }

        static string ToLetter(int number)
        {
            return ((char)(number + 65)).ToString();
        }

        public static void Test(string[] command)
        {
            FucineInt value = command[0];
            Birdsong.Sing(value);
        }
    }

    public struct FucineReference
    {
        readonly Func<string, string, int> contextType;
        readonly string contextId;
        readonly string elementId;
        public static readonly FucineReference unknown = new FucineReference(null, string.Empty, string.Empty);

        public int value { get { return contextType.Invoke(contextId, elementId); } }

        public static implicit operator int(FucineReference me) { return me.contextType.Invoke(me.contextId, me.elementId); }
        public FucineReference(Func<string, string, int> contextType, string contextId, string elementId)
        {
            this.contextType = contextType;
            this.contextId = contextId;
            this.elementId = elementId;
        }
    }

    [FucineExpression]
    public struct FucineInt
    {
        Expression expression;
        public FucineInt(string expression) { this.expression = FucineExpression.ParseAndCompile(expression); }
        public static implicit operator int(FucineInt me) { return FucineExpression.Evaluate<int>(me.expression, Convert.ToInt32); }

        public static implicit operator string(FucineInt me) { return ((int)me).ToString(); } //this one is purely for debug logs
        public static implicit operator FucineInt(string expression) { return new FucineInt(expression); } //for debug constructing
    }

    [FucineExpression]
    public struct FucineFloat
    {
        Expression expression;
        public FucineFloat(string expression) { this.expression = FucineExpression.ParseAndCompile(expression); }
        public static implicit operator float(FucineFloat me) { return FucineExpression.Evaluate<float>(me.expression, Convert.ToSingle); }

        public static implicit operator FucineFloat(string expression) { return new FucineFloat(expression); } //this one is purely for debug logs
        public static implicit operator string(FucineFloat me) { return ((float)me).ToString(); } //for debug constructing
    }

    [FucineExpression]
    public struct FucineBool
    {
        Expression expression;
        public static implicit operator bool(FucineBool me) { return FucineExpression.Evaluate<bool>(me.expression, Convert.ToBoolean); }

        public FucineBool(string expression) { this.expression = FucineExpression.ParseAndCompile(expression); }
        public static implicit operator FucineBool(string expression) { return new FucineBool(expression); }
        public static implicit operator string(FucineBool me) { return ((bool)me).ToString(); }
    }
}
