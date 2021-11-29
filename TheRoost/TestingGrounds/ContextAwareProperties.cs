using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

using HarmonyLib;
using NCalc;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;

using TheRoostManchine;

namespace TheRoostManchine
{
    internal class TheWorld
    {
        static AspectsDictionary currentLocal;
        static string myTestProperty = "effectsButBetter";
        static void Invoke()
        {
            TheRoostMachine.Patch(
                original: typeof(RecipeCompletionEffectCommand).GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance),
                prefix: typeof(TheWorld).GetMethod("SetLocalScope", BindingFlags.NonPublic | BindingFlags.Static),
                postfix: typeof(TheWorld).GetMethod("ResetLocalScope", BindingFlags.NonPublic | BindingFlags.Static));

            TheRoostMachine.Patch(
                original: typeof(RecipeCompletionEffectCommand).GetMethod("RunRecipeEffects", BindingFlags.NonPublic | BindingFlags.Instance),
                prefix: typeof(TheWorld).GetMethod("RunBetterEffects", BindingFlags.NonPublic | BindingFlags.Static));

            Vagabond.AddTest("exp", Test);
            Beachcomber.ClaimProperty<Recipe>(myTestProperty, typeof(Dictionary<string, FucineInt>));
        }

        static void RunBetterEffects(SecretHistories.Core.RecipeCompletionEffectCommand __instance, SecretHistories.Spheres.Sphere sphere)
        {
            Dictionary<string, FucineInt> effects = Beachcomber.RetrieveProperty<Dictionary<string, FucineInt>>(__instance.Recipe, myTestProperty);
            if (effects != null)
                foreach (KeyValuePair<string, FucineInt> effect in effects)
                    sphere.ModifyElementQuantity(effect.Key, effect.Value, new Context(Context.ActionSource.SituationEffect));
        }

        static void SetLocalScope(Situation situation)
        {
            TheWorld.currentLocal = situation.GetAspects(true);
        }

        static void ResetLocalScope()
        {
            TheWorld.currentLocal = Watchman.Get<HornedAxe>().GetAspectsInContext(null).AspectsOnTable;
        }

        static void Test(string[] command)
        {
            FucineInt value = command[0];
            Twins.Sing(value);
        }

        public static int GetExtantAspects(string unused, string elementId)
        {
            AspectsDictionary extant = Watchman.Get<HornedAxe>().GetAspectsInContext(null).AspectsExtant;
            return extant.ContainsKey(elementId) ? extant[elementId] : 0;
        }

        public static int GetTableAspects(string unused, string elementId)
        {
            AspectsDictionary table = Watchman.Get<HornedAxe>().GetAspectsInContext(null).AspectsOnTable;
            return table.ContainsKey(elementId) ? table[elementId] : 0;
        }

        public static int GetLocalAspects(string unused, string elementId)
        {
            return currentLocal.ContainsKey(elementId) ? currentLocal[elementId] : 0;
        }

        public static int GetVerbAspects(string verbId, string elementId)
        {
            HornedAxe horned = Watchman.Get<HornedAxe>();
            var situations = horned.GetSituationsWithVerbOfActionId(verbId);

            foreach (Situation situation in situations)
            {
                AspectsDictionary aspectsInVerb = situation.GetAspects(true);
                return aspectsInVerb.ContainsKey(elementId) ? aspectsInVerb[elementId] : 0;
            }

            return 0;
        }
    }

    [AttributeUsage(AttributeTargets.Struct)]
    public class FucineExpression : Attribute
    {
        const char referenceSeparator = '@';
        const char scopeSeparator = '#';

        public static Expression ParseAndCompile(string expression)
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

        public static T Evaluate<T>(Expression expression)
        {
            for (int n = 0; n < expression.Parameters.Count; n++)
                expression.Parameters[ToLetter(n)] = ((FucineReference)expression.Parameters[ToLetter(n)]).value;

            return (T)Convert.ChangeType(expression.Evaluate(),typeof(T));
        }

        static FucineReference CreateReference(string[] reference)
        {
            if (reference.Length == 1)
                return new FucineReference(ContextGetterByTag("default"), string.Empty, reference[0]);
            else if (reference.Length == 2)
                return new FucineReference(ContextGetterByTag(reference[0]), string.Empty, reference[1]);
            else if (reference.Length == 3)
                return new FucineReference(ContextGetterByTag(reference[0]), reference[1], reference[2]);

            Twins.Sing("Malformed reference {0}", String.Concat(reference));
            return new FucineReference(null, string.Empty, string.Empty);
        }

        static string ToLetter(int number)
        {
            return ((char)(number + 65)).ToString();
        }

        public static Func<string, string, int> ContextGetterByTag(string tag)
        {
            switch (tag)
            {
                case "extant": return TheWorld.GetExtantAspects;
                case "table": return TheWorld.GetTableAspects;
                case "default": return TheWorld.GetLocalAspects;
                case "verb": return TheWorld.GetVerbAspects;
                default: throw new Exception("Unknown context tag " + tag);
            }
        }
    }

    [FucineExpression]
    public struct FucineInt
    {
        Expression expression;
        public readonly string originalExpression;

        public FucineInt(string expression)
        {
            this.expression = FucineExpression.ParseAndCompile(expression);
            this.originalExpression = expression;
        }
        public static implicit operator int(FucineInt me)
        {
            //Twins.Sing(me.originalExpression);

            return FucineExpression.Evaluate<int>(me.expression);
        }

        public static implicit operator string(FucineInt me) { return ((int)me).ToString(); }
        public override string ToString() { return (string)this; }
        public static implicit operator FucineInt(string expression) { return new FucineInt(expression); }
    }

    public struct FucineReference
    {
        readonly Func<string, string, int> getcontext;
        readonly string contextId;
        readonly string elementId;
        public int value { get { return getcontext.Invoke(contextId, elementId); } }

        public static implicit operator int(FucineReference me) { return me.getcontext.Invoke(me.contextId, me.elementId); }
        public FucineReference(Func<string, string, int> getcontext, string contextId, string elementId)
        {
            this.getcontext = getcontext;
            this.contextId = contextId;
            this.elementId = elementId;
        }
    }
}