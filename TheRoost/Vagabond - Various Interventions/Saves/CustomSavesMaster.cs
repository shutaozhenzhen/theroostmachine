using Roost;

using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Infrastructure;
using SecretHistories.Infrastructure.Persistence;
using SecretHistories.Services;
using SecretHistories.UI;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Roost.Vagabond.Saves
{ 
    class CustomSavePersistenceProvider : GamePersistenceProvider
    {
        string saveName = null;
        bool asCustom = true;
        public CustomSavePersistenceProvider(string _saveName, bool _asCustom = true)
        {
            saveName = _saveName;
            asCustom = _asCustom;
        }

        protected override string GetSaveFileLocation()
        {
            string persistentDataPath = Watchman.Get<MetaInfo>().PersistentDataPath;
            if (asCustom) return $"{persistentDataPath}/custom_save_{saveName}.json";
            return $"{persistentDataPath}/{saveName}.json";
        }

        protected override string GetSaveFileLocation(string save)
        {
            return GetSaveFileLocation();
        }

        public void PurgeSaveFileIrrevocably()
        {
            File.Delete(GetSaveFileLocation());
        }

        public override async Task<bool> SerialiseAndSaveAsyncWithDefaultSaveName()
        {
            var saveTask = SerialiseAndSaveAsync(saveName);
            var result = await saveTask;

            return result;
        }
    }


    static class CustomSavesMaster
    {
        static string persistentDataPath = null;
        internal static void Enact()
        {
            persistentDataPath = Watchman.Get<MetaInfo>().PersistentDataPath;
            Roost.Vagabond.CommandLine.AddCommand("listsaves", ListCustomSaves);
            Roost.Vagabond.CommandLine.AddCommand("save", SaveCustomSaveFromArgs);
            Roost.Vagabond.CommandLine.AddCommand("load", LoadCustomSaveFromArgs);
        }

        public static void ListCustomSaves(string[] args)
        {
            DirectoryInfo d = new DirectoryInfo(persistentDataPath);
            FileInfo[] saveFiles = d.GetFiles("custom_save_*");
            if (saveFiles.Length == 0)
            {
                Birdsong.TweetLoud("Didn't find any custom save file.");
                return;
            }
            foreach (FileInfo fileInfo in saveFiles)
            {
                char[] sep = { '.' };
                string saveWithExtension = fileInfo.Name.Substring(12);
                string saveWithoutExtension = saveWithExtension.Split(sep)[0];
                Birdsong.TweetLoud("→ " + saveWithoutExtension);
            }
        }

        public static void LoadCustomSaveFromArgs(string[] args)
        {
            if (args.Length < 1)
            {
                Birdsong.TweetLoud("This command requires to provide the save name as the first argument");
                return;
            }
            string saveName = args[0];
            LoadCustomSave(saveName);
        }

        public static void LoadCustomSave(string saveName, bool asCustom = true)
        {
            Birdsong.TweetLoud("Trying to load save", saveName);
            var persistenceProvider = new CustomSavePersistenceProvider(saveName, asCustom);
            Watchman.Get<StageHand>().LoadGameOnTabletop(persistenceProvider);
        }

        public static void SaveCustomSaveFromArgs(string[] args)
        {
            if (args.Length < 1)
            {
                Birdsong.TweetLoud("This command requires to provide the save name as the first argument");
                return;
            }
            string saveName = args[0];
            _ = SaveCustomSave(saveName, true);
        }

        public static async Task<bool> SaveCustomSave(string saveName, bool asCustom)
        {
            Birdsong.TweetLoud("Trying to save to custom save", saveName);
            Watchman.Get<Heart>().Metapause();
            Watchman.Get<LocalNexus>().DisablePlayerInput(0f);

            var saveResult = await WriteStateToDisk(saveName, asCustom);

            if (saveResult)
            {
                Watchman.Get<Heart>().Unmetapause();
                Watchman.Get<LocalNexus>().EnablePlayerInput();
                Birdsong.TweetLoud("Saved!");
                return true;
            }
            return false;
        }

        public static async Task<bool> WriteStateToDisk(string saveName, bool asCustom)
        {
            var persistenceProvider = new CustomSavePersistenceProvider(saveName, asCustom);
            persistenceProvider.Encaust(Watchman.Get<Stable>(), FucineRoot.Get(), Watchman.Get<Xamanek>());
            var saveTask = persistenceProvider.SerialiseAndSaveAsync(saveName);
            var result = await saveTask;
            return result;
        }
    }
}