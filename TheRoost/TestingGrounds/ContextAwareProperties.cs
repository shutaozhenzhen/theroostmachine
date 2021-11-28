using System;
using System.Collections;
using System.Collections.Generic;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;

using UnityEngine;

using TheRoost;

namespace TheRoost.Nowhere
{
    class TheLeak
    {
        private static void Invoke()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            Beachcomber.InfectFucineWith<ContextAware>();
            Twins.onServicesInitialized += TestContext;
        }

        private static void TestContext()
        {
            List<ContextAware> list = Watchman.Get<Compendium>().GetEntitiesAsList<ContextAware>();
            Twins.Sing(list.Count);
        }
    }

    [FucineImportable("context")]
    public class ContextAware : AbstractEntity<ContextAware>
    {
     //   [FucineValue]
        public FucineInt value { get; set; }
        [FucineValue(DefaultValue = 0)]
        public int normalValue { get; set; }

        public ContextAware(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }
    }

    public struct FucineInt
    {
        string expression;

        public FucineInt(string expression) { this.expression = expression; }
        public static implicit operator FucineInt(string expression) { return new FucineInt(expression); }
        public static implicit operator FucineInt(int expression) { return new FucineInt(expression.ToString()); }
        public static implicit operator int(FucineInt me) { return TheWorld.Evaluate<int>(me.expression); }
        public static implicit operator string(FucineInt me) { return me.expression; }

    }


    public class TheWorld
    {
        /*
        static AspectsDictionary extant;
        static AspectsDictionary table;
        static Dictionary<Situation, AspectsDictionary> verbs;
        static Dictionary<DeckSpec, AspectsDictionary> decks;
        */
        public static T Evaluate<T>(string expression)
        {
            return (T)Convert.ChangeType(int.Parse(expression), typeof(T));
            return default(T);
        }
    }
}