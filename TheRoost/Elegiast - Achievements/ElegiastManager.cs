﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Steamworks;
using Galaxy.Api;

using SecretHistories.UI;
using SecretHistories.Services;
using SecretHistories.Constants;
using SecretHistories.Core;

using UnityEngine;
using TheRoost.Elegiast.Entities;

namespace TheRoost.Elegiast
{
    public static class CustomAchievementsManager
    {
        //Dictionary<achievement id, unlock time>
        private static Dictionary<string, string> unlocks;

        const string propertyThatUnlocks = "elegiastUnlock";

        const string datafile = "customachievements.json";
        static string localFile { get { return Application.persistentDataPath + "\\" + datafile; } }
        const string achievementDataFormat = "\"{0}\": \"{1}\",\n";

        internal static void ClaimProperties()
        {
            Machine.ClaimProperty<SecretHistories.Entities.Recipe, List<string>>(propertyThatUnlocks);
            TheRoost.Vagabond.CommandLine.AddCommand("achievements", AchievementsDebug);
        }

        internal static void Enact()
        {
            AtTimeOfPower.MainMenuLoaded.Schedule(CustomAchievementInterface.CreateInterface, PatchType.Prefix, Enactors.Elegiast.patchId);
            AtTimeOfPower.RecipeExecution.Schedule<RecipeCompletionEffectCommand>(UnlockAchievements, PatchType.Prefix, Enactors.Elegiast.patchId);

            LoadAllUnlocks();

            if (Watchman.Get<SecretHistories.Services.StorefrontServicesProvider>().IsAvailable(StoreClient.Gog))
            {
                cachedGogStats = GalaxyInstance.Stats();
                cachedGogStats.RequestUserStatsAndAchievements();
            }
        }

        private static void UnlockAchievements(RecipeCompletionEffectCommand __instance)
        {
            List<string> ids = __instance.Recipe.RetrieveProperty<List<string>>(propertyThatUnlocks);
            if (ids == null)
                return;

            bool atLeastOneUnlock = false;
            for (var i = ids.Count - 1; i >= 0; i--)
                atLeastOneUnlock = UnlockAchievement(ids[i], i) || atLeastOneUnlock;

            if (atLeastOneUnlock)
                TrySyncAchievementStorages();
        }

        private static bool UnlockAchievement(string id, int messageOrder = -1) //-1 for no message (if not inside the game scene currently, for example)
        {
            CustomAchievement achievement = Watchman.Get<Compendium>().GetEntityById<CustomAchievement>(id);
            if (achievement == null)
            {
                Birdsong.Sing(VerbosityLevel.SystemChatter, 0, "Attempt to unlock achievement '{0}' - no such achievement exists", id);
                return false;
            }
            else if (isUnlocked(achievement))
            {
                Birdsong.Sing(VerbosityLevel.SystemChatter, 0, "Attempt to unlock achievement '{0}' - but it is already unlocked", id);
                return false;
            }

            unlocks.Add(id, DateInvariant(DateTime.Now));

            if (messageOrder >= 0)
            {
                string message = achievement.unlockMessage == string.Empty ? achievement.unlockdesc : achievement.unlockMessage;
                Watchman.Get<Notifier>().ShowNotificationWindow(achievement.label, message, achievement.sprite, (messageOrder + 1) * 2, false);
            }

            return true;
        }

        private static void LoadAllUnlocks()
        {
            string[] cloudData = GetCloudData();
            string[] localData = GetLocalData();
            unlocks = LoadUnlocks(cloudData);
            Dictionary<string, string> localUnlocks = LoadUnlocks(localData);

            foreach (string achievement in localUnlocks.Keys)
                if (unlocks.ContainsKey(achievement) == false)
                    unlocks.Add(achievement, localUnlocks[achievement]);

            if (unlocks.ContainsKey("custom_achievement_sample") == false)
            {
                unlocks.Add("custom_achievement_sample", DateInvariant(DateTime.Now));
                TrySyncAchievementStorages();
            }
            else
                if (string.Concat(cloudData) != string.Concat(localData)) //if out of sync, sync
                    TrySyncAchievementStorages();
        }

        private static string[] GetLocalData()
        {
            if (File.Exists(localFile) == false)
                return new string[0];

            return File.ReadAllLines(localFile);
        }

        private static string[] GetCloudData()
        {
            StorefrontServicesProvider storefront = Watchman.Get<StorefrontServicesProvider>();
            if (storefront.IsAvailable(StoreClient.Steam) && SteamRemoteStorage.FileExists(datafile))
            {
                int size = SteamRemoteStorage.GetFileSize(datafile);
                byte[] bytes = new byte[size];
                SteamRemoteStorage.FileRead(datafile, bytes, size);
                return System.Text.Encoding.UTF8.GetString(bytes).Split('\n');
            }
            else if (storefront.IsAvailable(StoreClient.Gog))
            {
                ///TODO GOG (MAYBEY)
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
                    Birdsong.Sing("Malformed achievement in {0}, deleting", datafile);
                }

            return dictionary;
        }

        private static void SaveCurrentUnlocksLocally()
        {
            File.WriteAllText(localFile, Encode(unlocks));
        }

        private static void TrySyncAchievementStorages()
        {
            //player can write swear words and other nonsense to Gabe in the file by hand
            //to avoid pushing it on the server, we should ALWAYS do the local save (to rewrite the file) before that
            SaveCurrentUnlocksLocally();

            StorefrontServicesProvider storefront = Watchman.Get<StorefrontServicesProvider>();
            if (storefront.IsAvailable(StoreClient.Steam))
            {
                byte[] bytes = File.ReadAllBytes(localFile);
                if (bytes.Length == 0)
                    SteamRemoteStorage.FileDelete(datafile);
                else
                    SteamRemoteStorage.FileWrite(datafile, bytes, bytes.Length);
                Birdsong.Sing(VerbosityLevel.SystemChatter, 1, "Succesfully pushed achievement info on the cloud storage");

            }
            else if (storefront.IsAvailable(StoreClient.Gog))
            {
                ///TODO GOG (MAYBEY)
            }
        }

        private static string Encode(Dictionary<string, string> unlockDictionary)
        {
            string result = string.Empty;
            foreach (KeyValuePair<string, string> entry in unlockDictionary)
                result += Encode(entry.Key, entry.Value);

            return result;
        }

        private static string Encode(string id, string date)
        {
            string unlockData = String.Format(achievementDataFormat, id, date);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(unlockData);
            return System.Convert.ToBase64String(bytes) + "\n";
        }

        private static string Decode(string[] dataToDecode)
        {
            string data = string.Empty;
            foreach (string encoded in dataToDecode)
                data += Decode(encoded);

            return data;
        }

        private static string Decode(string dataToDecode)
        {
            if (dataToDecode == string.Empty)
                return string.Empty;

            try
            {
                byte[] bytes = System.Convert.FromBase64String(dataToDecode);
                string result = System.Text.Encoding.UTF8.GetString(bytes);
                return result;
            }
            catch
            {
                Birdsong.Sing("Malformed entry in {0}, deleting", datafile);
                return string.Empty;
            }
        }

        private static IStats cachedGogStats;
        public static void CheckVanillaAchievement(VanillaAchievement achievement, out bool unlocked, out bool legit, out bool hidden, out uint unlockTime)
        {
            unlocked = false;
            hidden = false;
            legit = false;
            unlockTime = 0;

            if (Watchman.Get<StorefrontServicesProvider>().IsAvailable(StoreClient.Steam))
            {
                legit = (SteamUserStats.GetAchievementAndUnlockTime(achievement.Id, out unlocked, out unlockTime) == true);
                hidden = (SteamUserStats.GetAchievementDisplayAttribute(achievement.Id, "hidden") == "1");
            }
            else if (cachedGogStats != null)
            {
                cachedGogStats.GetAchievement(achievement.Id, ref unlocked, ref unlockTime);
                hidden = cachedGogStats.IsAchievementVisibleWhileLocked(achievement.Id);
                legit = cachedGogStats.IsAchievementVisible(achievement.Id);
            }
        }

        private static string DateInvariant(DateTime date)
        {
            return DateTime.Now.ToString(new System.Globalization.CultureInfo("en-GB"));
        }

        public static DateTime GetUnlockTime(string id)
        {
            if (unlocks.ContainsKey(id))
                return DateTime.Parse(unlocks[id], new System.Globalization.CultureInfo("en-GB"));
            else
                return DateTime.MinValue;
        }

        public static bool isUnlocked(CustomAchievement achievement)
        {
            return unlocks.ContainsKey(achievement.Id);
        }

        public static void AchievementsDebug(string[] command)
        {
            try
            {
                string data = string.Empty;
                switch (command[0])
                {
                    case "reset": CustomAchievementsManager.ClearAchievement(command[1]); return;
                    case "cloud": data = CustomAchievementsManager.ReadableCloudData(); break;
                    case "local": data = CustomAchievementsManager.ReadableLocalData(); break;
                    case "all": data = CustomAchievementsManager.ReadableAll(); break;
                }

                if (command.Length == 1)
                    Birdsong.Sing(data);
                else
                    Birdsong.Sing("Checking achievement '{0}' presence: {1}", command[1], data.Contains("\"" + command[1] + "\"") ? "unlocked" : "locked");
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex);
            }
        }

        public static void ClearAchievement(string achievement)
        {
            if (achievement == "all")
            {
                unlocks.Clear();
                Birdsong.Sing("a l l  c u s t o m  a c h i e v e m e n t s  w e r e  r e s e t", achievement);
                TrySyncAchievementStorages();
                return;
            }

            if (unlocks.ContainsKey(achievement) == false)
            {
                Birdsong.Sing("Trying to reset achievement '{0}', but it's not unlocked, try checking 'achievements.cloud' and 'achievements.local' commands", achievement);
                return;
            }

            unlocks.Remove(achievement);
            Birdsong.Sing("Deleted achievement '{0}' from the local storage", achievement);
            TrySyncAchievementStorages();
        }

        public static string ReadableCloudData()
        {
            return Decode(GetCloudData());
        }

        public static string ReadableLocalData()
        {
            return Decode(GetLocalData());
        }

        public static string ReadableAll()
        {
            string result = string.Empty;
            foreach (KeyValuePair<string, string> entry in unlocks)
                result += String.Format(achievementDataFormat, entry.Key, entry.Value);

            return result;
        }
    }
}