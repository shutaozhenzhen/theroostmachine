using System;
using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.Core;
using SecretHistories.UI;
using SecretHistories.Infrastructure;
using Assets.Logic;
using SecretHistories.Abstract;
using SecretHistories.Spheres;

using TheRoost.Twins;
using TheRoost.Twins.Entities;

namespace TheRoost.LocalApplications
{
    public static class Legerdemain
    {
        const string deckAutoShuffleProperty = "shuffleAfterEachDraw";

        //deck effects order:
        //shuffle, forbid, normal effects, takeout, allow, add, insert
        //takeout isn't affected by forbids
        const string deckShuffleProperty = "deckShuffle";
        const string deckForbidProperty = "deckForbid";
        const string deckTakeOutProperty = "deckTakeOut";
        const string deckAllowProperty = "deckAllow";
        const string deckAddProperty = "deckAdd";
        const string deckInsertProperty = "deckInsert";

        static DealersTable dtable;

        internal static void Enact()
        {
            Machine.ClaimProperty<DeckSpec, bool>(deckAutoShuffleProperty);

            Machine.ClaimProperty<Recipe, List<string>>(deckShuffleProperty);
            Machine.ClaimProperty<Recipe, Dictionary<string, List<string>>>(deckForbidProperty);
            Machine.ClaimProperty<Recipe, Dictionary<string, List<string>>>(deckAllowProperty);
            Machine.ClaimProperty<Recipe, Dictionary<string, List<string>>>(deckAddProperty);
            Machine.ClaimProperty<Recipe, Dictionary<string, List<Funcine<bool>>>>(deckTakeOutProperty);
            Machine.ClaimProperty<Recipe, Dictionary<string, List<Funcine<bool>>>>(deckInsertProperty);

            AtTimeOfPower.NewGameStarted.Schedule(CatchNewGame, PatchType.Postfix);
            AtTimeOfPower.TabletopLoaded.Schedule(ReshuffleDecksOnNewGame, PatchType.Postfix);
            AtTimeOfPower.RecipeDeckEffects.Schedule<RecipeCompletionEffectCommand, Sphere>(DeckEffectsPre, PatchType.Prefix);
            AtTimeOfPower.RecipeDeckEffects.Schedule<RecipeCompletionEffectCommand, Sphere>(DeckEffectsPost, PatchType.Postfix);

            Machine.Patch(
                original: typeof(Dealer).GetMethod("Deal", new Type[] { typeof(DeckSpec) }),
                prefix: typeof(Legerdemain).GetMethodInvariant("Deal"));
        }

        static bool newGameAndIShouldReshuffleAllTheDecks = false;
        static void CatchNewGame()
        {
            newGameAndIShouldReshuffleAllTheDecks = true;
        }
        static void ReshuffleDecksOnNewGame()
        {
            dtable = Watchman.Get<DealersTable>();

            if (newGameAndIShouldReshuffleAllTheDecks)
            {
                Dealer dealer = new Dealer(dtable);
                string currentLegacyFamily = Watchman.Get<Stable>().Protag().ActiveLegacy.Family;

                foreach (DeckSpec deck in Watchman.Get<Compendium>().GetEntitiesAsList<DeckSpec>())
                    if (string.IsNullOrEmpty(deck.ForLegacyFamily) || currentLegacyFamily == deck.ForLegacyFamily)
                    {
                        dealer.Shuffle(deck);
                        IHasElementTokens drawPile = dtable.GetDrawPile(deck.Id);

                        if (drawPile.GetTotalStacksCount() == 0)
                        {
                            if (deck.DefaultCard != "")
                                drawPile.ProvisionElementToken(deck.DefaultCard, 1);
                            else
                                Birdsong.Sing("For whatever reason, deck {0} is completely empty, can't be reshuffled and has no default card", deck.Id);
                        }
                    }

                newGameAndIShouldReshuffleAllTheDecks = false;
            }
        }

        private static bool Deal(Dealer __instance, DeckSpec fromDeckSpec, DealersTable ____dealersTable, ref Token __result)
        {
            IHasElementTokens drawPile = ____dealersTable.GetDrawPile(fromDeckSpec.Id);

            __result = drawPile.GetElementTokens()[drawPile.GetTotalStacksCount() - 1];

            if (fromDeckSpec.RetrieveProperty<bool>(deckAutoShuffleProperty))
                ____dealersTable.RenewDeck(__instance, fromDeckSpec.Id);
            else if (drawPile.GetTotalStacksCount() == 0 && fromDeckSpec.ResetOnExhaustion)
                __instance.Shuffle(fromDeckSpec);

            if (drawPile.GetTotalStacksCount() == 0)
            {
                if (fromDeckSpec.DefaultCard != "")
                    drawPile.ProvisionElementToken(fromDeckSpec.DefaultCard, 1);
                else
                    Birdsong.Sing("For whatever reason, deck {0} is completely empty, can't be reshuffled and has no default card", fromDeckSpec.Id);
            }

            if (fromDeckSpec.DrawMessages.ContainsKey(__result.PayloadEntityId))
                __result.Payload.SetIllumination("mansusjournal", fromDeckSpec.DrawMessages[__result.PayloadEntityId]);

            return false;
        }

        private static void DeckEffectsPre(RecipeCompletionEffectCommand __instance, Sphere sphere)
        {
            DeckShuffles(__instance.Recipe);
            DeckForbids(__instance.Recipe);
        }

        private static void DeckEffectsPost(RecipeCompletionEffectCommand __instance, Sphere sphere)
        {
            DeckAllows(__instance.Recipe);
            DeckTakeOuts(__instance.Recipe, sphere);
            DeckAdds(__instance.Recipe);
            DeckInserts(__instance.Recipe, sphere);
        }

        private static void DeckShuffles(Recipe recipe)
        {
            var deckShuffles = recipe.RetrieveProperty<List<string>>(deckShuffleProperty);
            if (deckShuffles == null)
                return;

            Dealer dealer = new Dealer(dtable);
            foreach (string deckId in deckShuffles)
                dtable.RenewDeck(dealer, deckId);
        }

        private static void DeckForbids(Recipe recipe)
        {
            var deckForbids = recipe.RetrieveProperty<Dictionary<string, List<string>>>(deckForbidProperty);
            if (deckForbids == null)
                return;

            foreach (string deckId in deckForbids.Keys)
            {
                IHasElementTokens forbiddenPile = dtable.GetForbiddenPile(deckId);
                List<string> forbids = deckForbids[deckId];

                foreach (string elementId in forbids)
                    if (forbiddenPile.GetElementToken(elementId) == null)
                        forbiddenPile.ProvisionElementToken(elementId, 1);
            }
        }

        private static void DeckAllows(Recipe recipe)
        {
            var deckAllows = recipe.RetrieveProperty<Dictionary<string, List<string>>>(deckAllowProperty);
            if (deckAllows == null)
                return;

            foreach (string deckId in deckAllows.Keys)
            {
                IHasElementTokens forbiddenPile = dtable.GetForbiddenPile(deckId);
                List<string> allows = deckAllows[deckId];

                foreach (string elementId in allows)
                {
                    Token token = forbiddenPile.GetElementToken(elementId);
                    if (token != null)
                        token.Retire(SecretHistories.Enums.RetirementVFX.None);
                }
            }
        }

        private static void DeckTakeOuts(Recipe recipe, Sphere situationStorage)
        {
            var deckTakeOuts = recipe.RetrieveProperty<Dictionary<string, List<Funcine<bool>>>>(deckTakeOutProperty);
            if (deckTakeOuts == null)
                return;

            Context context = new Context(Context.ActionSource.SituationCreated);

            foreach (string deckId in deckTakeOuts.Keys)
            {
                List<Token> tokens = dtable.GetDrawPile(deckId).GetElementTokens();

                foreach (Funcine<bool> filter in deckTakeOuts[deckId])
                    foreach (Token token in tokens.FilterTokens(filter))
                        situationStorage.AcceptToken(token, context);
            }
        }

        private static void DeckInserts(Recipe recipe, Sphere sphere)
        {
            var deckInserts = recipe.RetrieveProperty<Dictionary<string, List<Funcine<bool>>>>(deckInsertProperty);
            if (deckInserts == null)
                return;

            Context context = new Context(Context.ActionSource.SituationCreated);
            List<Token> tokens = sphere.GetElementTokens();

            foreach (string deckId in deckInserts.Keys)
            {
                IHasElementTokens drawPile = dtable.GetDrawPile(deckId);

                foreach (Funcine<bool> filter in deckInserts[deckId])
                    foreach (Token token in tokens.FilterTokens(filter))
                        drawPile.AcceptToken(token, context);
            }
        }

        private static void DeckAdds(Recipe recipe)
        {
            var deckAdds = recipe.RetrieveProperty<Dictionary<string, List<string>>>(deckAddProperty);
            if (deckAdds == null)
                return;

            foreach (string deckId in deckAdds.Keys)
                foreach (string elementId in deckAdds[deckId])
                    dtable.GetDrawPile(deckId).ProvisionElementToken(elementId, 1);
        }

        private static void RenewDeck(this DealersTable dtable, Dealer dealer, string deckId)
        {
            IHasElementTokens drawPile = dtable.GetDrawPile(deckId);
            int tokenCount = drawPile.GetTotalStacksCount();
            List<Token> tokens = drawPile.GetElementTokens();
            for (int n = 0; n < tokenCount; n++)
                tokens[n].Retire(SecretHistories.Enums.RetirementVFX.None);

            dealer.Shuffle(deckId);
        }

        public static Token GetElementToken(this IHasElementTokens pile, string elementId)
        {
            return pile.GetElementTokens().Find(token => token.PayloadEntityId == elementId);
        }
    }
}
