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
        public const string DECK_AUTO_SHUFFLE = "shuffleAfterDraw";
        public const string DECK_IS_HIDDEN = "isHidden";
        private static DealersTable dealerstable;
        private static Dealer dealer;

        internal static void Enact()
        {
            Machine.ClaimProperty<DeckSpec, bool>(DECK_AUTO_SHUFFLE);
            Machine.ClaimProperty<DeckSpec, bool>(DECK_IS_HIDDEN);

            AtTimeOfPower.NewGameStarted.Schedule(CatchNewGame, PatchType.Prefix);
            AtTimeOfPower.TabletopLoaded.Schedule(OnGameStarted, PatchType.Postfix);
        }

        private static bool itsANewGameAndWeShouldReshuffleAllTheDecks = false;
        private static void CatchNewGame()
        {
            itsANewGameAndWeShouldReshuffleAllTheDecks = true;
        }
        private static void OnGameStarted()
        {
            dealerstable = Watchman.Get<DealersTable>();
            dealer = new Dealer(dealerstable);

            if (itsANewGameAndWeShouldReshuffleAllTheDecks)
            {
                string currentLegacyFamily = Watchman.Get<Stable>().Protag().ActiveLegacy.Family;

                foreach (DeckSpec deck in Watchman.Get<Compendium>().GetEntitiesAsList<DeckSpec>())
                    if (String.IsNullOrEmpty(deck.ForLegacyFamily) || currentLegacyFamily == deck.ForLegacyFamily)
                    {
                        dealer.Shuffle(deck);
                        IHasElementTokens drawPile = dealerstable.GetDrawPile(deck.Id);

                        if (drawPile.GetTotalStacksCount() == 0)
                        {
                            if (deck.DefaultCard != "")
                                drawPile.ProvisionElementToken(deck.DefaultCard, 1);
                            else
                                Birdsong.Sing($"For whatever reason, deck {deck.Id} is completely empty, can't be reshuffled and has no default card");
                        }
                    }

                itsANewGameAndWeShouldReshuffleAllTheDecks = false;
            }
        }

        public static void RunExtendedDeckEffects(GrandEffects effectsGroup, Sphere onSphere)
        {
            DeckShuffles(effectsGroup.DeckShuffles);
            DeckForbids(effectsGroup.DeckForbids);
            DeckEffects(effectsGroup.DeckEffects, onSphere);
            DeckTakeOuts(effectsGroup.DeckTakeOuts, onSphere);
            DeckAllows(effectsGroup.DeckAllows);
            DeckAdds(effectsGroup.DeckAdds);
            DeckInserts(effectsGroup.DeckInserts, onSphere);
        }

        private static void DeckShuffles(List<string> deckShuffles)
        {
            if (deckShuffles == null)
                return;

            foreach (string deckId in deckShuffles)
                RenewDeck(deckId);
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

        private static void DeckEffects(Dictionary<string, Funcine<int>> deckEffects, Sphere toSphere)
        {
            if (deckEffects == null)
                return;

            foreach (string deckId in deckEffects.Keys)
                Deal(deckId, toSphere, deckEffects[deckId].value);
        }

        private static void DeckTakeOuts(Dictionary<string, List<Funcine<bool>>> deckTakeOuts, Sphere toSphere)
        {
            if (deckTakeOuts == null)
                return;

            foreach (string deckId in deckTakeOuts.Keys)
            {
                List<Token> tokens = dealerstable.GetDrawPile(deckId).GetElementTokens();

                foreach (Funcine<bool> filter in deckTakeOuts[deckId])
                    foreach (Token token in tokens.FilterTokens(filter))
                        RecipeExecutionBuffer.ScheduleMovement(token, toSphere);
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

        private static void DeckInserts(Dictionary<string, List<Funcine<bool>>> deckInserts, Sphere fromSphere)
        {
            if (deckInserts == null)
                return;
            List<Token> tokens = fromSphere.GetElementTokens();

            foreach (string deckId in deckInserts.Keys)
            {
                Sphere drawPile = dealerstable.GetDrawPile(deckId) as Sphere;

                foreach (Funcine<bool> filter in deckInserts[deckId])
                    foreach (Token token in tokens.FilterTokens(filter))
                        RecipeExecutionBuffer.ScheduleMovement(token, drawPile);
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

        public static void Deal(string deckId, Sphere toSphere, int draws = 1)
        {
            DeckSpec deckSpec = Machine.GetEntity<DeckSpec>(deckId);
            if (deckSpec == null)
                throw Birdsong.Cack($"TRYING TO DRAW FROM NON-EXISTENT DECK '{deckId}'");

            IHasElementTokens drawPile = dealerstable.GetDrawPile(deckId);
            Limbo limbo = Watchman.Get<Limbo>();
            for (int i = 0; i < draws; i++)
            {
                Token token = drawPile.GetElementTokens()[drawPile.GetTotalStacksCount() - 1];
                RecipeExecutionBuffer.ScheduleMovement(token, toSphere);
                token.SetSphere(limbo, RecipeExecutionBuffer.situationEffectContext);
                //need to exclude the token from the deck sphere right now so the next calculations and operations are correct

                if (drawPile.GetTotalStacksCount() == 0 && deckSpec.ResetOnExhaustion)
                    dealer.Shuffle(deckSpec);
                //if we've shuffled the deck, but it's still empty, add default card; (if it's not defined it'll be a blank card, so better don't draw it!)
                if (drawPile.GetTotalStacksCount() == 0)
                {
                    if (String.IsNullOrEmpty(deckSpec.DefaultCard))
                        throw Birdsong.Cack($"DECK '{deckId}' IS EMPTY, WON'T SHUFFLE AND HAS NO DEFAULT CARD");

                    drawPile.ProvisionElementToken(deckSpec.DefaultCard, 1);
                }

                if (deckSpec.DrawMessages.ContainsKey(token.PayloadEntityId))
                    token.Payload.SetIllumination("mansusjournal", deckSpec.DrawMessages[token.PayloadEntityId]);
            }

            if (deckSpec.RetrieveProperty<bool>(DECK_AUTO_SHUFFLE) == true)
                RecipeExecutionBuffer.ScheduleDeckRenew(deckSpec.Id);
        }

        public static void RenewDeck(string deckId)
        {
            IHasElementTokens drawPile = dealerstable.GetDrawPile(deckId);
            int tokenCount = drawPile.GetTotalStacksCount();
            List<Token> tokens = drawPile.GetElementTokens();
            for (int n = 0; n < tokenCount; n++)
                tokens[n].Retire();

            dealer.Shuffle(deckId);
        }

        public static Token GetElementToken(this IHasElementTokens pile, string elementId)
        {
            return pile.GetElementTokens().Find(token => token.PayloadEntityId == elementId);
        }
    }
}
