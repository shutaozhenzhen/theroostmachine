using System;
using System.Collections;
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

namespace Roost.World.Audio
{
    internal class TrumpetLily
    {
        internal const string AUDIO = "audio";

        internal const string MUSIC = "BGmusic";
        internal const string OVERRIDE_MUSIC = "overrideBGMusic";

        const string START_SFX = "startSFX";
        const string END_SFX = "endSFX";

        const string PLAY_MUSIC = "playAudioTrack";
        const string SET_BG_MUSIC = "setBGMusic";

        internal static void Enact()
        {
            Machine.Patch(typeof(ModManager).GetMethodInvariant(nameof(ModManager.TryLoadImagesForEnabledMods)),
                postfix: typeof(TrumpetLily).GetMethodInvariant(nameof(TryLoadAudioForEnabledMods)));

            Machine.ClaimProperty<Legacy, List<string>>(MUSIC);
            Machine.ClaimProperty<Legacy, bool>(OVERRIDE_MUSIC);
            AtTimeOfPower.TabletopSceneInit.Schedule(HandleTabletopBGMusic, PatchType.Postfix);

            Machine.ClaimProperty<Recipe, List<string>>(START_SFX);
            Machine.ClaimProperty<Recipe, List<string>>(END_SFX);
            Machine.ClaimProperty<Recipe, List<string>>(SET_BG_MUSIC);
            Machine.ClaimProperty<Recipe, string>(PLAY_MUSIC);
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(SingRecipeEffects, PatchType.Prefix);

            Machine.ClaimProperty<Ending, string>(MUSIC);
            Machine.Patch(typeof(GameOverScreenController).GetMethodInvariant(nameof(PlayEndingMusic)),
                postfix: typeof(TrumpetLily).GetMethodInvariant(nameof(PlayEndingMusic)));

            Vagabond.CommandLine.AddCommand("music", MusicPlay);
            Vagabond.CommandLine.AddCommand("sfx", SFXPlay);
        }

        static List<AudioClip> tabletopBGMusic;
        static List<AudioClip> legacyDefaultBGMusic;
        static AudioSource tabletopMusicAudioSource;
        static AudioClip emptyClip = AudioClip.Create("", 1, 1, 1, false);
        private static void HandleTabletopBGMusic()
        {
            var bgMusField = typeof(BackgroundMusic).GetFieldInvariant("backgroundMusic");
            tabletopBGMusic = (bgMusField.GetValue(Watchman.Get<BackgroundMusic>()) as IEnumerable<AudioClip>).ToList();
            bgMusField.SetValue(Watchman.Get<BackgroundMusic>(), tabletopBGMusic);

            tabletopMusicAudioSource = typeof(BackgroundMusic).GetFieldInvariant("audioSource").GetValue(Watchman.Get<BackgroundMusic>()) as AudioSource;

            List<string> trackList;
            if (Watchman.Get<Stable>().Protag().ActiveLegacy.RetrieveProperty<bool>(OVERRIDE_MUSIC))
                tabletopBGMusic.Clear();

            if (Watchman.Get<Stable>().Protag().ActiveLegacy.TryRetrieveProperty(MUSIC, out trackList))
                foreach (string trackName in trackList)
                {
                    if (TryGetCustomClip(trackName, out AudioClip clip))
                        continue;

                    tabletopBGMusic.Add(clip);
                }

            legacyDefaultBGMusic = tabletopBGMusic;
        }

        private static void SingRecipeEffects(Situation situation)
        {
            Recipe recipe = situation.CurrentRecipe;

            if (recipe.TryRetrieveProperty(SET_BG_MUSIC, out List<string> trackNames))
            {
                List<AudioClip> trackList;

                if (trackNames == null || trackNames.Count == 0)
                    trackList = legacyDefaultBGMusic;
                else
                {
                    trackList = new List<AudioClip>();

                    foreach (string trackName in trackNames)
                        if (TryGetCustomClip(trackName, out AudioClip track))
                            trackList.Add(track);
                }


                tabletopBGMusic.Clear();
                tabletopBGMusic.Add(emptyClip);
                tabletopBGMusic.AddRange(trackList);

                if (trackList.Count > 0)
                {
                    AudioClip track = trackList[UnityEngine.Random.Range(0, trackList.Count)];
                    FadeToTrack(track, 3);
                }
            }

            if (recipe.TryRetrieveProperty(PLAY_MUSIC, out string playTrack))
            {
                if (TryGetCustomClip(playTrack, out AudioClip track))
                    FadeToTrack(track, 3);
            }
        }

        public static void FadeToTrack(AudioClip track, float duration)
        {
            Watchman.Get<BackgroundMusic>().StartCoroutine(Transit());

            IEnumerator Transit()
            {
                float startingVolume = tabletopMusicAudioSource.volume;

                Watchman.Get<BackgroundMusic>().FadeToSilence(duration-0.1f);
                yield return new WaitForSeconds(duration);

                tabletopMusicAudioSource.volume = startingVolume;
                tabletopMusicAudioSource.Stop();
                tabletopMusicAudioSource.PlayOneShot(track);
            }
        }


        private static void PlayEndingMusic(Ending ending, AudioSource ___audioSource)
        {
            if (ending.TryRetrieveProperty(MUSIC, out string trackName))
            {
                if (TryGetCustomClip(trackName, out AudioClip clip))
                    return;

                ___audioSource.Stop();
                ___audioSource.clip = clip;
                ___audioSource.Play();
            }
        }

        private static void TryLoadAudioForEnabledMods(ContentImportLog log)
        {
            audioClips = AudioLoader.TryLoadAudioForEnabledMods(log);
        }

        static Dictionary<string, AudioClip> audioClips;
        public static bool TryGetCustomClip(string name, out AudioClip clip)
        {
            if (!audioClips.ContainsKey(name))
            {
                Birdsong.Tweet(VerbosityLevel.Essential, 1, $"Trying to get audio clip '{name}', but it's not loaded");
                clip = null;
                return false;
            }

            clip = audioClips[name];
            return true;
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
                Birdsong.Tweet(VerbosityLevel.Essential, 1, $"Unable to play track '{trackName}': {ex.FormatException()}");
            }
        }

        public static void SFXPlay(params string[] command)
        {
            string sfxName = String.Join(" ", command);

            if (SoundManager.PlaySfx(sfxName) == -1)
                Birdsong.Tweet(VerbosityLevel.Essential, 1, $"Unable to play sfx '{sfxName}'.");
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
                string musicFolder = Path.Combine(mod.ModRootFolder, TrumpetLily.AUDIO).SlashInvariant();

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
