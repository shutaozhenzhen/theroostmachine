using System;
using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Infrastructure;
using Assets.Logic;
using SecretHistories.Abstract;
using SecretHistories.Spheres;

using Roost.Twins.Entities;

namespace Roost.World.Recipes
{
    public static class Legerdemain
    {
        public const string DECK_AUTO_SHUFFLE = "shuffleAfterDraw";

        internal static void Enact()
        {
            Machine.ClaimProperty<DeckSpec, bool>(DECK_AUTO_SHUFFLE, defaultValue: false);
            //DeckSpec.Draws is only used for recipe internal decks; to allow them to use expressions, this
            Machine.ClaimProperty<DeckSpec, FucineExp<int>>("draws", false, "1");
        }

        public static void Deal(string deckId, Sphere toSphere, int draws, SecretHistories.Enums.RetirementVFX vfx)
        {
            Dealer dealer = new Dealer(Watchman.Get<DealersTable>());
            DeckSpec deckSpec = Machine.GetEntity<DeckSpec>(deckId);
            if (deckSpec == null)
                throw Birdsong.Cack($"TRYING TO DRAW FROM NON-EXISTENT DECK '{deckId}'");

            Limbo limbo = Watchman.Get<Limbo>();
            for (int i = 0; i < draws; i++)
            {
                Token token  = dealer.Deal(deckSpec);

                //need to exclude the token from the deck sphere right now so the next calculations and operations are correct
                token.SetSphere(limbo, new Context(Context.ActionSource.SituationEffect));
                RecipeExecutionBuffer.ScheduleMovement(token, toSphere, vfx);
            }

            if (deckSpec.RetrieveProperty<bool>(DECK_AUTO_SHUFFLE) == true)
                RecipeExecutionBuffer.ScheduleDeckRenew(deckSpec.Id);
        }

        public static Sphere RenewDeck(string deckId)
        {
            DealersTable dtable = Watchman.Get<DealersTable>();
            Dealer dealer = new Dealer(dtable);
            IHasElementTokens drawPile = dtable.GetDrawPile(deckId);
            int tokenCount = drawPile.GetTotalStacksCount();
            List<Token> tokens = drawPile.GetElementTokens();
            for (int n = 0; n < tokenCount; n++)
                tokens[n].Retire();

            dealer.Shuffle(deckId);

            return (Sphere)drawPile;
        }

        public static Token GetElementToken(this IHasElementTokens pile, string elementId)
        {
            return pile.GetElementTokens().Find(token => token.PayloadEntityId == elementId);
        }
    }
}
