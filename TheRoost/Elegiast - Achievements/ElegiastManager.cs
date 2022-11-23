using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Steamworks;

using SecretHistories.UI;
using SecretHistories.Services;
using SecretHistories.Constants;

using UnityEngine;

namespace Roost.Elegiast
{
    public static class CustomAchievementsManager
    {
        const string ACHIEVEMENTS_FILE = "customachievements.json";
        static string localFile { get { return Application.persistentDataPath + "\\" + ACHIEVEMENTS_FILE; } }

        const string testAchievementName = "custom_achievement_sample";

        internal static void Enact()
        {
            ConvertLegacyAchievements();
        }

        public static void ConvertLegacyAchievements()
        {
            var chronicler = Watchman.Get<AchievementsChronicler>();
            Dictionary<string, string> unlocks = typeof(AchievementsChronicler).GetFieldInvariant("_unlocks").GetValue(chronicler)
                as Dictionary<string, string>;
            bool needUpdate = false;

            string[] cloudData = GetCloudLegacyData();
            if (cloudData.Length > 0)
            {
                Dictionary<string, string> legacyUnlocks = LoadUnlocks(cloudData);
                foreach (string achievement in legacyUnlocks.Keys)
                    if (achievement != testAchievementName)
                        if (unlocks.ContainsKey(achievement) == false)
                        {
                            unlocks.Add(achievement, legacyUnlocks[achievement]);
                            needUpdate = true;
                        }

                Birdsong.TweetLoud($"Legacy achievements from {ACHIEVEMENTS_FILE} were converted. Removing the old file from the cloud.");
                SteamRemoteStorage.FileDelete(ACHIEVEMENTS_FILE);
            }


            string[] localData = GetLocalLegacyData();
            if (localData.Length > 0)
            {
                Dictionary<string, string> legacyUnlocks = LoadUnlocks(localData);
                foreach (string achievement in legacyUnlocks.Keys)
                    if (achievement != testAchievementName)
                        if (unlocks.ContainsKey(achievement) == false)
                        {
                            unlocks.Add(achievement, legacyUnlocks[achievement]);
                            needUpdate = true;
                        }

                Birdsong.TweetLoud($"Legacy achievements from {ACHIEVEMENTS_FILE} were converted. Removing the old file from the disk.");
                File.Delete(localFile);
            }

            if (needUpdate)
                typeof(AchievementsChronicler).GetMethodInvariant("TryUpdateCloudStorage").Invoke(chronicler, new Type[0]);
        }

        private static string[] GetLocalLegacyData()
        {
            if (File.Exists(localFile) == false)
                return new string[0];

            return File.ReadAllLines(localFile);
        }

        private static string[] GetCloudLegacyData()
        {
            StorefrontServicesProvider storefront = Watchman.Get<StorefrontServicesProvider>();
            if (storefront.IsAvailable(StoreClient.Steam) && SteamRemoteStorage.FileExists(ACHIEVEMENTS_FILE))
            {
                int size = SteamRemoteStorage.GetFileSize(ACHIEVEMENTS_FILE);
                byte[] bytes = new byte[size];
                SteamRemoteStorage.FileRead(ACHIEVEMENTS_FILE, bytes, size);
                return System.Text.Encoding.UTF8.GetString(bytes).Split('\n');
            }

            return new string[0];
        }

        private static Dictionary<string, string> LoadUnlocks(string[] encodedData)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            foreach (string str in encodedData)
                try
                {
                    //to minimize amount of devastation that each individual entry can cause we wrap them into a separate hashtable
                    //especially important since we push stuff on Cloud, where it's held for all God's eternity                    
                    string data = data = String.Concat("{", Decode(str), "}");
                    foreach (DictionaryEntry entry in SimpleJsonImporter.Import(data, true))
                        dictionary.Add(entry.Key.ToString(), entry.Value.ToString());
                }
                catch
                {
                    Birdsong.TweetLoud($"Malformed achievement in {ACHIEVEMENTS_FILE}, deleting.");
                }

            return dictionary;
        }

        private static string Decode(string dataToDecode)
        {
            if (dataToDecode == string.Empty)
                return string.Empty;

            try
            {
                byte[] bytes = Convert.FromBase64String(dataToDecode);
                string result = System.Text.Encoding.UTF8.GetString(bytes);
                return result;
            }
            catch
            {
                Birdsong.TweetLoud($"Malformed entry in {ACHIEVEMENTS_FILE}, deleting");
                return string.Empty;
            }
        }
    }
}