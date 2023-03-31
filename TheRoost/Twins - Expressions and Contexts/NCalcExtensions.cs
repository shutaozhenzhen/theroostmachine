using System;
using NCalc;
using UnityEngine;

namespace Roost.Twins
{

    internal static class NCalcExtensions
    {
        internal static void Extensions(string name, FunctionArgs functionArgs)
        {
            if (name.Equals(nameof(Round), StringComparison.InvariantCultureIgnoreCase))
            {
                Round(functionArgs);
                return;
            }

            if (name.Equals(nameof(Random), StringComparison.InvariantCultureIgnoreCase))
            {
                Random(functionArgs);
                return;
            }
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
            return 
                expression.IndexOf(nameof(Random), StringComparison.InvariantCultureIgnoreCase) > -1
             || expression.IndexOf(nameof(Round), StringComparison.InvariantCultureIgnoreCase) > -1;
        }
    }
}
