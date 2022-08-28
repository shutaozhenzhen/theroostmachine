using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.UI;
using SecretHistories.Constants.Modding;

using UnityEngine;
using UnityEngine.Networking;

using Roost;

namespace Roost.World.Audio
{
    internal static class Nightingale
    {
        internal const string AUDIO = "audio";

        internal const string MUSIC = "music";
        internal const string OVERRIDE_TRACKS = "overrideMusic";

        internal static void Enact()
        {
            Machine.Patch(typeof(ModManager).GetMethodInvariant(nameof(ModManager.TryLoadImagesForEnabledMods)),
                postfix: typeof(Nightingale).GetMethodInvariant(nameof(TryLoadAudioForEnabledMods)));

            Machine.ClaimProperty<Legacy, List<string>>(MUSIC);
            Machine.ClaimProperty<Legacy, bool>(OVERRIDE_TRACKS);
            AtTimeOfPower.TabletopSceneInit.Schedule(HandleTabletopBGMusic, PatchType.Postfix);

            Machine.ClaimProperty<Ending, string>(MUSIC);
            Machine.Patch(typeof(GameOverScreenController).GetMethodInvariant(nameof(PlayEndingMusic)),
                postfix: typeof(Nightingale).GetMethodInvariant(nameof(PlayEndingMusic)));

            Vagabond.CommandLine.AddCommand("music", MusicPlay);
            Vagabond.CommandLine.AddCommand("sfx", SFXPlay);
        }

        static List<AudioClip> tabletopBGMusic;
        private static void HandleTabletopBGMusic()
        {
            var bgMusField = typeof(BackgroundMusic).GetFieldInvariant("backgroundMusic");
            tabletopBGMusic = (bgMusField.GetValue(Watchman.Get<BackgroundMusic>()) as IEnumerable<AudioClip>).ToList();
            bgMusField.SetValue(Watchman.Get<BackgroundMusic>(), tabletopBGMusic);

            List<string> trackList;
            if (Watchman.Get<Stable>().Protag().ActiveLegacy.RetrieveProperty<bool>(OVERRIDE_TRACKS))
                tabletopBGMusic.Clear();

            if (Watchman.Get<Stable>().Protag().ActiveLegacy.TryRetrieveProperty(MUSIC, out trackList))
                foreach (string trackName in trackList)
                {
                    AudioClip clip = GetCustomClip(trackName);

                    if (clip == null)
                        continue;

                    tabletopBGMusic.Add(clip);
                }
        }

        private static void PlayEndingMusic(Ending ending, AudioSource ___audioSource)
        {
            if (ending.TryRetrieveProperty(MUSIC, out string trackName))
            {
                AudioClip clip = GetCustomClip(trackName);

                if (clip == null)
                    return;

                ___audioSource.Stop();
                ___audioSource.clip = clip;
                ___audioSource.Play();
            }
        }

        static Dictionary<string, AudioClip> audioClips;
        public static AudioClip GetCustomClip(string name)
        {
            if (!audioClips.ContainsKey(name))
            {
                Birdsong.Sing($"Trying to get audio clip '{name}', but it's not loaded");
                return null;
            }

            return audioClips[name];
        }

        private static void TryLoadAudioForEnabledMods(ContentImportLog log)
        {
            audioClips = AudioLoader.TryLoadAudioForEnabledMods(log);
        }

        public static void MusicPlay(params string[] command)
        {
            string trackName = String.Join(" ", command);

            if (trackName == "next")
            {
                Watchman.Get<BackgroundMusic>().PlayNextClip();
                return;
            }

            try
            {
                int index = tabletopBGMusic.FindIndex(track => track.name == trackName);
                Watchman.Get<BackgroundMusic>().PlayClip(index, tabletopBGMusic);
            }
            catch (Exception ex)
            {
                Birdsong.Sing($"Unable to play track '{trackName}': {ex.FormatException()}");
            }
        }

        public static void SFXPlay(params string[] command)
        {
            string sfxName = String.Join(" ", command);

            if (SoundManager.PlaySfx(sfxName) == -1)
                Birdsong.Sing($"Unable to play sfx '{sfxName}'.");
        }
    }



    internal static class AudioLoader
    {
        private static Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>();
        internal static Dictionary<string, AudioClip> TryLoadAudioForEnabledMods(ContentImportLog log)
        {
            foreach (AudioClip clip in audioClips.Values)
                GameObject.DestroyImmediate(clip, true);

            audioClips.Clear();

            foreach (Mod mod in Watchman.Get<ModManager>().GetEnabledModsInLoadOrder())
            {
                string musicFolder = Path.Combine(mod.ModRootFolder, Nightingale.AUDIO).SlashInvariant();

                foreach (string audioFilePath in GetFilesRecursive(musicFolder, ""))
                    LoadClip(audioFilePath, log);
            }

            return audioClips;
        }

        private static string SlashInvariant(this string str)
        {
            return str.Replace('/', '\\');
        }

        private static async void LoadClip(string path, ContentImportLog log)
        {
            try
            {
                AudioType audioType = GetTypeByExtension(Path.GetExtension(path));
                AudioClip clip = await GetAudioClip(path, audioType);

                if (clip.length == 0)
                    throw new ApplicationException("Unable to load.");

                clip.name = Path.GetFileName(path);
                audioClips[clip.name] = clip;

                if (audioClips.ContainsKey(clip.name))
                    log.LogWarning($"Duplicate audio clip name '{clip.name}' at '{path}'. Overriding the present one.");
            }
            catch (Exception ex)
            {
                log.LogWarning($"Unable to load audio clip '{path}': {ex.FormatException()}");
            }
        }

        private async static Task<AudioClip> GetAudioClip(string filePath, AudioType fileType)
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(filePath, fileType))
            {
                var result = www.SendWebRequest();

                while (!result.isDone) { await Task.Delay(100); }

                if (www.result == UnityWebRequest.Result.ConnectionError)
                {
                    throw new ApplicationException(www.error);
                }
                else
                {
                    return DownloadHandlerAudioClip.GetContent(www);
                }
            }
        }

        private static AudioType GetTypeByExtension(string extension)
        {
            switch (extension)
            {
                case ".ogg": return AudioType.OGGVORBIS;
                case ".wav": return AudioType.WAV;
                case ".mp2": case ".mp3": return AudioType.MPEG;
                case ".aiff": case ".aif": case ".aifc": return AudioType.AIFF;
                case ".it": return AudioType.IT;
                case ".mod": return AudioType.MOD;
                case ".vag": return AudioType.VAG;
                default:
                    throw new ApplicationException("Unknown audio type");
            }
        }

        private static List<string> GetFilesRecursive(string path, string extension)
        {
            List<string> result = new List<string>();

            return GetFilesRecursive(result, path, extension);
        }

        private static List<string> GetFilesRecursive(List<string> result, string path, string extension)
        {
            if (Directory.Exists(path))
            {
                result.AddRange(Directory.GetFiles(path).ToList().FindAll((string f) => f.EndsWith(extension)));

                foreach (string path2 in Directory.GetDirectories(path))
                    result.AddRange(GetFilesRecursive(path2, extension));
            }

            return result;
        }
    }
}
