using System;
using System.Linq;
using System.Collections.Generic;
using NCalc;
using UnityEngine;

namespace Roost.Twins
{

    public static class NCalcExtensions
    {
        static Dictionary<string, Action<FunctionArgs>> extensions = new Dictionary<string, Action<FunctionArgs>>(StringComparer.InvariantCultureIgnoreCase);
        public static void AddFunction(Action<FunctionArgs> func)
        {
            extensions.Add(func.Method.Name, func);
        }

        public static void HandleNCalcExtensions(string name, FunctionArgs functionArgs)
        {
            if (extensions.ContainsKey(name))
                extensions[name](functionArgs);
        }

        internal static void Round(FunctionArgs functionArgs)
        {
            if (functionArgs.Parameters.Length == 1)
            {
                functionArgs.Result = Mathf.Round(functionArgs.Parameters[0].Evaluate().ConvertTo<float>());
                return;
            }

            if (functionArgs.Parameters.Length == 2)
            {
                float pow = Mathf.Pow(2, functionArgs.Parameters[1].Evaluate().ConvertTo<float>());
                functionArgs.Result = Mathf.Round(functionArgs.Parameters[0].Evaluate().ConvertTo<float>() * pow) / pow;
                return;
            }

            throw Birdsong.Cack($"Too many parameters in Round({functionArgs.Parameters.UnpackCollection(arg => arg.ParsedExpression.ToString(), ",")})");
        }

        internal static void Random(FunctionArgs functionArgs)
        {

            if (functionArgs.Parameters.Length == 2)
            {
                functionArgs.Result = UnityEngine.Random.Range(functionArgs.Parameters[0].Evaluate().ConvertTo<float>(), functionArgs.Parameters[1].Evaluate().ConvertTo<float>());
                return;
            }

            if (functionArgs.Parameters.Length == 1)
            {
                functionArgs.Result = UnityEngine.Random.Range(0, functionArgs.Parameters[0].Evaluate().ConvertTo<float>());
                return;
            }

            //if more than 2 arguments, pick a random number from them
            int ind = UnityEngine.Random.Range(0, functionArgs.Parameters.Length);
            functionArgs.Result = functionArgs.Parameters[ind].Evaluate().ConvertTo<float>();
        }

        public static bool ExpressionUsesExtensions(string expression)
        {
            return extensions.Keys.Any(expression.Contains);
        }
    }
}
