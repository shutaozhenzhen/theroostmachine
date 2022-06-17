using Roost;
using SecretHistories.Entities;
using SecretHistories.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Roost.World.Beauty
{
    class StartupQuoteMaster
    {
        public static void Enact()
        {
            Machine.ClaimProperty<Legacy, List<string>>("quotes", true);
            AtTimeOfPower.QuoteSceneInit.Schedule(setRandomQuote, PatchType.Postfix);
        }

        public static void setRandomQuote()
        {
            Legacy legacy = Watchman.Get<Stable>().Protag().ActiveLegacy;
            if (legacy == null) return;

            List<string> quotes = legacy.RetrieveProperty<List<string>>("quotes");
            if (quotes == null) return;

            int randomIndex = UnityEngine.Random.Range(0, quotes.Count - 1);
            GameObject.Find("Quote").GetComponent<TextMeshProUGUI>().text = quotes[randomIndex];
        }
    }

}
