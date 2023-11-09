using SecretHistories.Entities;
using SecretHistories.Services;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;

namespace Roost.Piebald
{
    public static class ILocStringProviderExtensions
    {
        public static Culture GetCurrentCulture(this Config config)
        {
            string cultureId = config.GetConfigValue("culture", "en");
            return Watchman.Get<Compendium>().GetEntityById<Culture>(cultureId);
        }

        public static TMP_FontAsset GetFontForCurrentCulture(this ILocStringProvider provider, LanguageManager.eFontStyle fontstyle = LanguageManager.eFontStyle.BodyText)
        {
            Culture currentCulture = Watchman.Get<Config>().GetCurrentCulture();
            return provider.GetFont(fontstyle, currentCulture.FontScript);
        }
    }
}
