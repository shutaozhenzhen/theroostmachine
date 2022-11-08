using Roost.Vagabond;
using SecretHistories.Entities;
using SecretHistories.UI;
using System;
using System.Linq;
using System.IO;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Roost.World
{
    class CheckpointMaster
    {
        const string USE_CHECKPOINTS = "useCheckpoints";
        const string SAVE_CHECKPOINT = "saveCheckpoint";
        public delegate void EventHandler(PointerEventData eventData);
        public static void Enact()
        {
            //1. New property on legacies, + new setting (in setting files)
            Machine.ClaimProperty<Legacy, Boolean>(USE_CHECKPOINTS, false, false);

            //2. New property on recipes to save a checkpoint as a "checkpoint.json" save file
            Machine.ClaimProperty<Recipe, Boolean>(SAVE_CHECKPOINT, false, false);
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(SaveCheckpointEffect, PatchType.Postfix);

            //3. Hook into the ending screen to maybe display the checkpoint button
            AtTimeOfPower.GameOverSceneInit.Schedule(InjectCheckpointButton, PatchType.Postfix);

            // On starting a new game, clear the checkpoint file
            AtTimeOfPower.NewGame.Schedule(ClearCheckpointFile, PatchType.Postfix);
            AtTimeOfPower.NewGameSceneInit.Schedule(ClearCheckpointFile, PatchType.Postfix);
        }

        private static bool CheckpointsAllowedInSettings()
        {
            return Watchman.Get<Compendium>().GetEntityById<Setting>("checkpoints").CurrentValue.Equals(1);
        }

        private static bool LegacyAllowsCheckpoints()
        {
            Legacy legacy = Watchman.Get<Stable>().GetAllCharacters().First().ActiveLegacy;
            return Machine.RetrieveProperty<Boolean>(legacy, USE_CHECKPOINTS);
        }

        private static async void SaveCheckpointEffect(Situation situation)
        {
            var createCheckpoint = situation.CurrentRecipe.RetrieveProperty<Boolean>(SAVE_CHECKPOINT);
            if (!createCheckpoint) return;

            if (!CheckpointsAllowedInSettings()) return; 
            if (!LegacyAllowsCheckpoints())
            {
                Birdsong.Sing(Birdsong.Incr(), "WARNING: A recipe just tried to save a checkpoint, but the current legacy DOES NOT ALLOW IT. Add useCheckpoints: true to the legacy to enable this feature.");
                return;
            }

            await CustomSavesMaster.SaveCustomSave("checkpoint", false);
            Watchman.Get<Notifier>().ShowNotificationWindow(
                Watchman.Get<ILocStringProvider>().Get("UI_CHECKPOINT_SAVED_LABEL"),
                Watchman.Get<ILocStringProvider>().Get("UI_CHECKPOINT_SAVED"), 
                null, 5f
            );
        }

        private static void InjectCheckpointButton()
        {
            if (!CheckpointsAllowedInSettings()) return;
            if (!LegacyAllowsCheckpoints()) return;

            DirectoryInfo d = new DirectoryInfo(Watchman.Get<MetaInfo>().PersistentDataPath);
            FileInfo[] saveFile = d.GetFiles("checkpoint.json");
            if (saveFile.Length == 0) return;

            GameObject parent = GameObject.Find("Buttons");
            GameObject buttonToDuplicate = GameObject.Find("Button_NewGame");
            
            GameObject buttonGO = GameObject.Instantiate(buttonToDuplicate, parent.transform);
            ColorBlock cb = buttonGO.GetComponent<Button>().colors;

            GameObject.DestroyImmediate(buttonGO.GetComponent<ButtonSoundTrigger>());
            GameObject.DestroyImmediate(buttonGO.GetComponent<Button>());
           
            Button btn = buttonGO.AddComponent<Button>();
            btn.colors = cb;

            buttonGO.AddComponent<ButtonSoundTrigger>();

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(ReloadCheckpoint);

            buttonGO.GetComponentInChildren<TextMeshProUGUI>().text = Watchman.Get<ILocStringProvider>().Get("UI_LOAD_CHECKPOINT");
        }

        private static void ReloadCheckpoint()
        {
            Birdsong.Sing("Reloading Checkpoint!");
            CustomSavesMaster.LoadCustomSave("checkpoint", false);
        }

        private static void ClearCheckpointFile()
        {
            DirectoryInfo d = new DirectoryInfo(Watchman.Get<MetaInfo>().PersistentDataPath);
            FileInfo[] saveFile = d.GetFiles("checkpoint.json");
            if (saveFile.Length == 0) return;
            saveFile[0].Delete();
        }
    }
}
