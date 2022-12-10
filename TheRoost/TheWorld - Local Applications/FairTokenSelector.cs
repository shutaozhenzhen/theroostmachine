using System.Collections.Generic;
using System.Linq;
using SecretHistories.UI;
using UnityEngine;

using Roost.Twins.Entities;

namespace Roost.World
{
    public static class FairTokenSelector
    {
        static Context contextCalve = new Context(Context.ActionSource.CalvedStack);
        public static List<Token> FilterTokens(this List<Token> tokens, FucineExp<bool> filter)
        {
            if (filter.isUndefined || tokens.Count == 0)
                return tokens;

            Twins.Crossroads.MarkAllLocalTokens(tokens);

            List<Token> result = new List<Token>();
            foreach (Token token in tokens)
            {
                Twins.Crossroads.MarkLocalToken(token);

                if (filter.value == true)
                {
                    //Birdsong.Tweet($"{token.PayloadId} satisfied filter {filter.formula}");
                    result.Add(token);
                }
            }

            Twins.Crossroads.UnmarkAllLocalTokens();
            return result;
        }

        public static Token SelectSingleToken(this IEnumerable<Token> fromTokens)
        {
            if (fromTokens.Count() < 2)
                return fromTokens.FirstOrDefault();               

            Dictionary<Token, int> tokenThresholds = new Dictionary<Token, int>();
            int totalQuantity = 0;

            foreach (Token token in fromTokens)
            {
                totalQuantity = totalQuantity + token.Quantity;
                tokenThresholds[token] = totalQuantity;
            }

            int selectedNumber = Random.Range(0, totalQuantity);
            foreach (KeyValuePair<Token, int> tokenThreshold in tokenThresholds)
                if (selectedNumber < tokenThreshold.Value)
                    return tokenThreshold.Key;

            return null;
        }

        public static List<Token> SelectRandom(this List<Token> fromTokens, int Limit)
        {
            if (Limit <= 0)
                return new List<Token>();

            Dictionary<Token, int> tokenThresholds = new Dictionary<Token, int>();
            int totalQuantity = 0;

            foreach (Token token in fromTokens)
            {
                totalQuantity = totalQuantity + token.Quantity;
                tokenThresholds[token] = totalQuantity;
            }

            if (totalQuantity <= Limit)
                return new List<Token>(fromTokens);

            HashSet<int> selectedNumbers = new HashSet<int>();
            while (selectedNumbers.Count < Limit)
                selectedNumbers.Add(Random.Range(0, totalQuantity));

            List<Token> result = new List<Token>();
            List<int> numbersLeft = new List<int>(selectedNumbers);
            foreach (Token token in tokenThresholds.Keys)
            {
                int selectedAmount = 0;

                for (int n = 0; n < numbersLeft.Count; n++)
                    if (numbersLeft[n] < tokenThresholds[token])
                    {
                        selectedAmount++;
                        numbersLeft.RemoveAt(n);
                        n--;
                    }

                if (selectedAmount == 0)
                    continue;

                if (selectedAmount < token.Quantity)
                {
                    Token newToken = token.CalveToken(selectedAmount, contextCalve);
                    result.Add(newToken);

                    if (newToken.Sphere.SphereCategory == SecretHistories.Enums.SphereCategory.World)
                        new TokenTravelItinerary(token.Location.Anchored3DPosition, token.Location.Anchored3DPosition + Vector3.right * 40).WithDuration(0.1f).Depart(newToken, contextCalve);
                }
                else
                    result.Add(token);
            }

            return result;
        }
    }
}
