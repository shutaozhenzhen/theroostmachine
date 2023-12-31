namespace Roost.Piebald
{
    using System.IO;
    using SecretHistories.Infrastructure.Modding;
    using SecretHistories.UI;
    using UnityEngine;

    public static class ResourceResolver
    {
        private const string DefaultCategory = "ui";

        /// <summary>
        /// Resolves resources from various game asset locations.
        /// By default, this will look in the game's UI assets.  However, it can be targeted to other areas by prefixing the asset name.
        /// If an asset is not found, 'null' is returned.  This contrasts with the game's own resource manager class, which returns appropriate fallbacks for their categories.
        /// Available targets are:
        /// "ui:" The game's UI assets
        /// "aspect:" The game's aspect assets
        /// "element:" The game's element assets
        /// "legacy:" The game's legacy assets
        /// "verb:" The game's verb assets
        /// "internal:" Scans all assets loaded into unity.  Note that this may not find the asset if the game has not loaded it in yet.
        /// <summary>
        public static Sprite GetSprite(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return null;
            }

            var parts = resourceName.Split(':');
            if (parts.Length == 1)
            {
                parts = new[] { DefaultCategory, parts[0] };
            }

            switch (parts[0])
            {
                case "ui":
                    return GetSpriteFromFolder("ui", parts[1]);
                case "aspect":
                    return GetSpriteFromFolder("aspects", parts[1]);
                case "element":
                    return GetSpriteFromFolder("elements", parts[1]);
                case "legacy":
                    return GetSpriteFromFolder("legacies", parts[1]);
                case "verb":
                    return GetSpriteFromFolder("verbs", parts[1]);
                case "internal":
                    return ResourceHack.FindSprite(parts[1]);
            }

            NoonUtility.LogWarning($"Unknown sprite category {parts[0]}");
            return null;
        }

        private static Sprite GetSpriteFromFolder(string folder, string resourceName)
        {
            // This is a partial reimplementation of ResourcesManager.GetSpriteForUI
            // We want to return null if the sprite is not found, while GetSpriteForUI returns a fallback.
            string uiPath = Path.Combine(Watchman.Get<SecretHistories.Services.Config>().GetConfigValue("imagesdir"), folder, resourceName);

            var sprite = Watchman.Get<ModManager>().GetSprite(uiPath);
            if (sprite == null)
            {
                sprite = Resources.Load<Sprite>(uiPath);
            }

            if (sprite == null)
            {
                NoonUtility.LogWarning($"Could not find sprite {folder}/{resourceName} at {uiPath}");
            }

            return sprite;
        }
    }
}
