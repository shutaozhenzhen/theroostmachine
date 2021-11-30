using System;
using System.Reflection;
using System.Collections.Generic;

using HarmonyLib;
using NCalc;

using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.UI;

using TheRoost.Entities;

namespace TheRoost
{
    internal class TheWorld
    {
        static AspectsDictionary localAspects;

        static string myTestProperty = "effectsButBetter";
        internal static void Enact()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            TheRoostMachine.Patch(
                original: typeof(RecipeCompletionEffectCommand).GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance),
                prefix: typeof(TheWorld).GetMethod("SetLocalScope", BindingFlags.NonPublic | BindingFlags.Static),
                postfix: typeof(TheWorld).GetMethod("ResetLocalScope", BindingFlags.NonPublic | BindingFlags.Static));

            TheRoostMachine.Patch(
                original: typeof(RecipeCompletionEffectCommand).GetMethod("RunRecipeEffects", BindingFlags.NonPublic | BindingFlags.Instance),
                prefix: typeof(TheWorld).GetMethod("RunBetterEffects", BindingFlags.NonPublic | BindingFlags.Static));

            Vagabond.AddTest("exp", FucineExpression.Test);
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
            TheWorld.localAspects = situation.GetAspects(true);
        }

        static void ResetLocalScope()
        {
            TheWorld.localAspects = Watchman.Get<HornedAxe>().GetAspectsInContext(null).AspectsOnTable;
        }

        public static int ExtantAspects(string unused, string elementId)
        {
            AspectsDictionary extant = Watchman.Get<HornedAxe>().GetAspectsInContext(null).AspectsExtant;
            return extant.ContainsKey(elementId) ? extant[elementId] : 0;
        }

        public static int TableAspects(string unused, string elementId)
        {
            AspectsDictionary table = Watchman.Get<HornedAxe>().GetAspectsInContext(null).AspectsOnTable;
            return table.ContainsKey(elementId) ? table[elementId] : 0;
        }

        public static int CurrentLocalAspects(string unused, string elementId)
        {
            return localAspects.ContainsKey(elementId) ? localAspects[elementId] : 0;
        }

        public static int VerbAspects(string verbId, string elementId)
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

        public static int DeckAspects(string deckId, string elementId)
        {
            AspectsDictionary aspects = new AspectsDictionary();
            var table = Watchman.Get<SecretHistories.Infrastructure.DealersTable>();
            var drawPile = table.GetDrawPile(deckId);
            if (drawPile.GetTotalStacksCount() == 0)
                new Assets.Logic.Dealer(table).Shuffle(deckId);

            foreach (Token token in drawPile.GetElementTokens())
                aspects.CombineAspects(token.GetAspects());

            return aspects.ContainsKey(elementId) ? aspects[elementId] : 0;
        }

        public static Func<string, string, int> GetContextType(string tag)
        {
            switch (tag)
            {
                case "local":
                case "default": return CurrentLocalAspects;
                case "extant": return ExtantAspects;
                case "table": return TableAspects;
                case "verbs":
                case "verb": return VerbAspects;
                case "decks":
                case "deck": return DeckAspects;
                default: throw new Exception("Unknown context tag " + tag);
            }
        }
    }
}