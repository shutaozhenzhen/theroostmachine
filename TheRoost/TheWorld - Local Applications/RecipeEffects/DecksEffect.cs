using System;
using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Infrastructure;
using Assets.Logic;
using SecretHistories.Abstract;
using SecretHistories.Spheres;

using Roost.Twins;
using Roost.Twins.Entities;
using Roost.World.Recipes.Entities;

namespace Roost.World.Recipes
{
    //order: shuffle, forbid, normal draws, normal effects with references, takeout, allow, add, insert
    public static class Legerdemain
    {
        const string DECK_AUTO_SHUFFLE = "shuffleAfterDraw";

        private static DealersTable dealerstable;
        private static Dealer dealer;
        
        internal static void Enact()
        {
            Machine.ClaimProperty<DeckSpec, bool>(DECK_AUTO_SHUFFLE);

            AtTimeOfPower.NewGameStarted.Schedule(CatchNewGame, PatchType.Postfix);
            AtTimeOfPower.TabletopLoaded.Schedule(ReshuffleDecksOnNewGame, PatchType.Postfix);

            Machine.Patch(
                original: typeof(Dealer).GetMethod("Deal", new Type[] { typeof(DeckSpec) }),
                prefix: typeof(Legerdemain).GetMethodInvariant("Deal"));
        }

        private static bool newGameAndIShouldReshuffleAllTheDecks = false;
        private static void CatchNewGame()
        {
            newGameAndIShouldReshuffleAllTheDecks = true;
        }
        private static void ReshuffleDecksOnNewGame()
        {
            dealerstable = Watchman.Get<DealersTable>();
            dealer = new Dealer(dealerstable);

            if (newGameAndIShouldReshuffleAllTheDecks)
            {
                string currentLegacyFamily = Watchman.Get<Stable>().Protag().ActiveLegacy.Family;

                foreach (DeckSpec deck in Watchman.Get<Compendium>().GetEntitiesAsList<DeckSpec>())
                    if (string.IsNullOrEmpty(deck.ForLegacyFamily) || currentLegacyFamily == deck.ForLegacyFamily)
                    {
                        dealer.Shuffle(deck);
                        IHasElementTokens drawPile = dealerstable.GetDrawPile(deck.Id);

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

            if (fromDeckSpec.RetrieveProperty<bool>(DECK_AUTO_SHUFFLE))
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

        public static void RunExtendedDeckEffects(GrandEffects effectsGroup, Sphere onSphere)
        {
            DeckShuffles(effectsGroup.deckShuffles);
            DeckForbids(effectsGroup.deckForbids);
            DeckDraws(effectsGroup.deckDraws, onSphere);
            DeckTakeOuts(effectsGroup.deckTakeOuts, onSphere);
            DeckAllows(effectsGroup.deckAllows);
            DeckAdds(effectsGroup.deckAdds);
            DeckInserts(effectsGroup.deckInserts, onSphere);
        }

        private static void DeckShuffles(List<string> deckShuffles)
        {
            if (deckShuffles == null)
                return;

            Dealer dealer = new Dealer(dealerstable);
            foreach (string deckId in deckShuffles)
                dealerstable.RenewDeck(dealer, deckId);
        }

        private static void DeckForbids(Dictionary<string, List<string>> deckForbids)
        {
            if (deckForbids == null)
                return;

            foreach (string deckId in deckForbids.Keys)
            {
                IHasElementTokens forbiddenPile = dealerstable.GetForbiddenPile(deckId);
                List<string> forbids = deckForbids[deckId];

                foreach (string elementId in forbids)
                    if (forbiddenPile.GetElementToken(elementId) == null)
                        forbiddenPile.ProvisionElementToken(elementId, 1);
            }
        }

        private static void DeckDraws(Dictionary<string, Funcine<int>> deckDraws, Sphere situationStorage)
        {
            if (deckDraws == null)
                return;

            Dealer dealer = new Dealer(dealerstable);
            foreach (string deckId in deckDraws.Keys)
            {
                int draws = deckDraws[deckId].value;
                for (int i = 0; i++ < draws; )
                    dealer.Deal(deckId);
            }
        }

        private static void DeckTakeOuts(Dictionary<string, List<Funcine<bool>>> deckTakeOuts, Sphere situationStorage)
        {
            if (deckTakeOuts == null)
                return;

            Context context = new Context(Context.ActionSource.SituationCreated);

            foreach (string deckId in deckTakeOuts.Keys)
            {
                List<Token> tokens = dealerstable.GetDrawPile(deckId).GetElementTokens();

                foreach (Funcine<bool> filter in deckTakeOuts[deckId])
                    foreach (Token token in tokens.FilterTokens(filter))
                        situationStorage.AcceptToken(token, context);
            }
        }

        private static void DeckAllows(Dictionary<string, List<string>> deckAllows)
        {
            if (deckAllows == null)
                return;

            foreach (string deckId in deckAllows.Keys)
            {
                IHasElementTokens forbiddenPile = dealerstable.GetForbiddenPile(deckId);
                List<string> allows = deckAllows[deckId];

                foreach (string elementId in allows)
                {
                    Token token = forbiddenPile.GetElementToken(elementId);
                    if (token != null)
                        token.Retire(SecretHistories.Enums.RetirementVFX.None);
                }
            }
        }

        private static void DeckInserts(Dictionary<string, List<Funcine<bool>>> deckInserts, Sphere sphere)
        {
            if (deckInserts == null)
                return;

            Context context = new Context(Context.ActionSource.SituationCreated);
            List<Token> tokens = sphere.GetElementTokens();

            foreach (string deckId in deckInserts.Keys)
            {
                IHasElementTokens drawPile = dealerstable.GetDrawPile(deckId);

                foreach (Funcine<bool> filter in deckInserts[deckId])
                    foreach (Token token in tokens.FilterTokens(filter))
                        drawPile.AcceptToken(token, context);
            }
        }

        private static void DeckAdds(Dictionary<string, List<string>> deckAdds)
        {
            if (deckAdds == null)
                return;

            foreach (string deckId in deckAdds.Keys)
                foreach (string elementId in deckAdds[deckId])
                    dealerstable.GetDrawPile(deckId).ProvisionElementToken(elementId, 1);
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
