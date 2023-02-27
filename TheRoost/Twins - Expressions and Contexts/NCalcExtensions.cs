using System.Collections.Generic;
using System.Linq;

using Roost.World;

using NCalc;
using UnityEngine;

namespace Roost.Twins
{

    internal static class NCalcExtensions
    {
        internal static void Round(string name, FunctionArgs functionArgs)
        {
            if (name != "Round")
                return;

            if (functionArgs.Parameters.Length == 1)
            {
                functionArgs.Result = UnityEngine.Mathf.Round(functionArgs.Parameters[0].Evaluate().ConvertTo<float>());
                return;
            }

            if (functionArgs.Parameters.Length == 2)
            {
                float pow = Mathf.Pow(2, functionArgs.Parameters[1].Evaluate().ConvertTo<float>());
                functionArgs.Result = UnityEngine.Mathf.Round(functionArgs.Parameters[0].Evaluate().ConvertTo<float>() * pow) / pow;
                return;
            }

            throw Birdsong.Cack($"Too many parameters in Round({functionArgs.Parameters.UnpackCollection(exp => exp.ToString(), ",")})");
        }

        internal static void Random(string name, FunctionArgs functionArgs)
        {
            if (name != "Random")
                return;

            //faster without?
            if (functionArgs.Parameters.Length == 1)
            {
                functionArgs.Result = UnityEngine.Random.Range(0, functionArgs.Parameters[0].Evaluate().ConvertTo<int>());
                return;
            }
            if (functionArgs.Parameters.Length == 2)
            {
                functionArgs.Result = UnityEngine.Random.Range(functionArgs.Parameters[0].Evaluate().ConvertTo<float>(), functionArgs.Parameters[1].Evaluate().ConvertTo<float>());
                return;
            }

            if (functionArgs.Parameters.Length == 1)
                throw Birdsong.Cack($"Not enough parameters in Random({functionArgs.Parameters.UnpackCollection(exp => (exp as Expression).Evaluate(), ",")})");

            throw Birdsong.Cack($"Too many parameters in Random({functionArgs.Parameters.UnpackCollection(exp => exp.ToString(), ",")})");
        }
    }
}
