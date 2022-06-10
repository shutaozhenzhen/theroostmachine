using Roost;
using SecretHistories.Assets.Scripts.Application.Entities.NullEntities;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Infrastructure;
using SecretHistories.Infrastructure.Persistence;
using SecretHistories.Services;
using SecretHistories.UI;
using System.IO;
using System.Threading.Tasks;

namespace Roost.Vagabond
{ 
    class CustomSavePersistenceProvider : GamePersistenceProvider
    {
        string saveName = null;
        public CustomSavePersistenceProvider(string _saveName)
        {
            saveName = _saveName;
        }

        protected override string GetSaveFileLocation()
        {
            string persistentDataPath = Watchman.Get<MetaInfo>().PersistentDataPath;
            return $"{persistentDataPath}/custom_save_{saveName}.json";
        }

        public void PurgeSaveFileIrrevocably()
        {
            File.Delete(GetSaveFileLocation());
        }
    }


    class CustomSavesMaster
    {
        static string persistentDataPath = null;
        public static void Enact()
        {
            persistentDataPath = Watchman.Get<MetaInfo>().PersistentDataPath;
            Roost.Vagabond.CommandLine.AddCommand("listsaves", ListCustomSaves);
            Roost.Vagabond.CommandLine.AddCommand("save", SaveCustomSave);
            Roost.Vagabond.CommandLine.AddCommand("load", LoadCustomSave);
        }

        public static void ListCustomSaves(string[] args)
        {
            DirectoryInfo d = new DirectoryInfo(persistentDataPath);
            FileInfo[] saveFiles = d.GetFiles("custom_save_*");
            if (saveFiles.Length == 0)
            {
                Birdsong.Sing("Didn't find any custom save file.");
                return;
            }
            foreach (FileInfo fileInfo in saveFiles)
            {
                char[] sep = { '.' };
                string saveWithExtension = fileInfo.Name.Substring(12);
                string saveWithoutExtension = saveWithExtension.Split(sep)[0];
                Birdsong.Sing("→ " + saveWithoutExtension);
            }
        }

        public static void LoadCustomSave(string[] args)
        {
            if (args.Length < 1)
            {
                Birdsong.Sing("This command requires to provide the save name as the first argument");
                return;
            }
            string saveName = args[0];
            Birdsong.Sing("Trying to load custom save", args[0]);

            var persistenceProvider = new CustomSavePersistenceProvider(saveName);
            Watchman.Get<StageHand>().LoadGameOnTabletop(persistenceProvider);
        }

        public static async void SaveCustomSave(string[] args)
        {
            if (args.Length < 1)
            {
                Birdsong.Sing("This command requires to provide the save name as the first argument");
                return;
            }
            string saveName = args[0];
            Birdsong.Sing("Trying to save to custom save", args[0]);

            Watchman.Get<Heart>().Metapause();
            Watchman.Get<LocalNexus>().DisablePlayerInput(0f);

            var saveResult = await WriteStateToDisk(saveName);

            if (saveResult)
            {
                Watchman.Get<Heart>().Unmetapause();
                Watchman.Get<LocalNexus>().EnablePlayerInput();
                Birdsong.Sing("Saved!");
            }
        }

        public static async Task<bool> WriteStateToDisk(string saveName)
        {
            var persistenceProvider = new CustomSavePersistenceProvider(saveName);
            persistenceProvider.Encaust(Watchman.Get<Stable>(), FucineRoot.Get(), Watchman.Get<Xamanek>());
            var saveTask = persistenceProvider.SerialiseAndSaveAsync();
            var result = await saveTask;
            return result;
        }
    }
}