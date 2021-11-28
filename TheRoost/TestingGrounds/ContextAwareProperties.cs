using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using HarmonyLib;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;

using TheRoost;

namespace TheRoost.Nowhere
{
    internal class TheWorld
    {
        static AspectsDictionary currentLocal;

        public static void Test()
        {
            List<ContextAware> list = Watchman.Get<Compendium>().GetEntitiesAsList<ContextAware>();
            Twins.Sing((int)list[0].intValue[0]);
        }

        static void Invoke()
        {
            TheRoostMachine.Patch(
                original: typeof(RecipeCompletionEffectCommand).GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance),
                prefix: typeof(TheWorld).GetMethod("SetLocalScope", BindingFlags.NonPublic | BindingFlags.Static),
                postfix: typeof(TheWorld).GetMethod("ResetLocalScope", BindingFlags.NonPublic | BindingFlags.Static));

            Beachcomber.InfectFucineWith<ContextAware>();
        }

        public static Func<string, AspectsDictionary> GetContextReference(string tag)
        {
            switch (tag)
            {
                case "extant": return GetExtantAspects;
                case "table": return GetTableAspects;
                case "default": return GetLocalAspects;
                default: return GetVerbAspects;
            }
        }

        static void SetLocalScope(Situation situation)
        {
            TheWorld.currentLocal = situation.GetAspects(true);
        }

        static void ResetLocalScope()
        {
            TheWorld.currentLocal = GetTableAspects(string.Empty);
        }

        public static AspectsDictionary GetExtantAspects(string useless)
        {
            return Watchman.Get<HornedAxe>().GetAspectsInContext(null).AspectsExtant;
        }

        public static AspectsDictionary GetTableAspects(string useless)
        {
            return Watchman.Get<HornedAxe>().GetAspectsInContext(null).AspectsOnTable;
        }

        public static AspectsDictionary GetLocalAspects(string id)
        {
            return currentLocal;
        }

        public static AspectsDictionary GetVerbAspects(string id)
        {
            HornedAxe horned = Watchman.Get<HornedAxe>();
            var situations = horned.GetSituationsWithVerbOfActionId(id);
            foreach (Situation situation in situations)
                return situation.GetAspects(true);

            return new AspectsDictionary();
        }

        public static T Evaluate<T>(string expression, FucineReference[] references)
        {
            StringBuilder builder = new StringBuilder(expression, expression.Length + references.Length * 3);
            for (var n = 0; n < references.Length; n++)
                builder.Replace(ToLetter(n), references[n].value.ToString());

            string result = builder.ToString();
            var evaluator = new NCalc.Expression(result, NCalc.EvaluateOptions.None);
            object value = evaluator.Evaluate();

            Twins.Sing("{0} = {1} = {2}", expression, result, value);

            return (T)Convert.ChangeType(value, typeof(T));
        }

        private const char referenceSeparator = '@';
        private const char scopeSeparator = '#';
        public static string ParseReferences(string expression, out FucineReference[] references)
        {
            string[] split = expression.Split(referenceSeparator);
            List<FucineReference> referencesList = new List<FucineReference>();
            int referencesCount = 0;

            for (var n = 0; n < split.Length; n++)
                if (split[n].Length > 0 && Char.IsLetter(split[n][0]))
                {
                    referencesList.Add(new FucineReference(split[n].Split(scopeSeparator)));
                    split[n] = ToLetter(referencesCount);
                    referencesCount++;
                }

            references = referencesList.ToArray();
            return string.Concat(split);
        }

        public static string ToLetter(int number)
        {
            return ((char)(number + 65)).ToString();
        }
    }

    public class FucineInt : AbstractEntity<FucineInt>
    {
        [FucineValue]
        public string expression { get; set; }
        FucineReference[] references;
        public static implicit operator int(FucineInt me) { return TheWorld.Evaluate<int>(me.expression, me.references); }

        public FucineInt(EntityData importDataForEntity, ContentImportLog log)
            : base(importDataForEntity, log)
        {
            expression = TheWorld.ParseReferences(expression, out references);
        }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }
    }

    public struct FucineReference
    {
        readonly Func<string, AspectsDictionary> contextRef;
        readonly string element;

        public FucineReference(string[] reference)
        {
            if (reference.Length == 1) //scope isnt defined
            {
                this.contextRef = TheWorld.GetContextReference("default");
                this.element = reference[0];
            }
            else if (reference.Length == 2)
            {
                this.contextRef = TheWorld.GetContextReference(reference[0]);
                this.element = reference[1];
            }
            else
            {
                Twins.Sing("Malformed reference {0}", String.Concat(reference));
                this.contextRef = null;
                this.element = string.Empty;
            }
        }

        public int value
        {
            get
            {
                AspectsDictionary context = contextRef.Invoke(element);
                return context.ContainsKey(element) ? context[element] : 0;
            }
        }
    }

    [FucineImportable("context")]
    public class ContextAware : AbstractEntity<ContextAware>
    {
        [FucineList]
        public List<FucineInt> intValue { get; set; }

        public ContextAware(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }
    }
}