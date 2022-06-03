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
            //DeckSpec.Draws is only used for recipe internal decks; to allow them to use expressions, this
            Machine.ClaimProperty<DeckSpec, Funcine<int>>("draws", false, "1");

            AtTimeOfPower.NewGameStarted.Schedule(CatchNewGame, PatchType.Prefix);
            AtTimeOfPower.TabletopLoaded.Schedule(TabletopEnter, PatchType.Postfix);
        }

        private static bool itsANewGameAndWeShouldReshuffleAllTheDecks = false;
        private static void CatchNewGame()
        {
            itsANewGameAndWeShouldReshuffleAllTheDecks = true;
        }
        private static void TabletopEnter()
        {
            Crossroads.defaultSphereContainer.Add(Watchman.Get<HornedAxe>().GetDefaultSphere(SecretHistories.Enums.OccupiesSpaceAs.Intangible));
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
                                Birdsong.Tweet($"For whatever reason, deck {deck.Id} is completely empty, can't be reshuffled and has no default card");
                        }
                    }

                itsANewGameAndWeShouldReshuffleAllTheDecks = false;
            }
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
                if (drawPile.GetTotalStacksCount() - 1 < 0)//catching a mysterious bug
                    throw Birdsong.Cack($"DECK '{deckId}' IS EMPTY, WON'T SHUFFLE AND HAS NO DEFAULT CARD (AND SOMEHOW PASSED THE PREVIOUS CHECK)");

                Token token = drawPile.GetElementTokens()[drawPile.GetTotalStacksCount() - 1];

                RecipeExecutionBuffer.ScheduleMovement(token, toSphere, SecretHistories.Enums.RetirementVFX.None);
                token.SetSphere(limbo, RecipeExecutionBuffer.situationEffectContext);
                //need to exclude the token from the deck sphere right now so the next calculations and operations are correct

                if (drawPile.GetTotalStacksCount() == 0 && deckSpec.ResetOnExhaustion)
                    dealer.Shuffle(deckSpec);
                //we've shuffled the deck, but it's still empty, add default card; (if it's not defined it'll be a blank card, so better don't draw it!)
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
