using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Fucine;
using SecretHistories.Spheres;
using SecretHistories.Enums;

using TheRoost.Twins.Entities;

namespace TheRoost.Twins
{
    public static class TokenContextManager
    {
        internal static void AddDebugCommads()
        {
            TheRoost.Vagabond.CommandLine.AddCommand("spheres", LogAllSpheres);
            TheRoost.Vagabond.CommandLine.AddCommand("ref", TestReference);
            TheRoost.Vagabond.CommandLine.AddCommand("exp", TestExpression);
            TheRoost.Vagabond.CommandLine.AddCommand("sphere", SphereContent);
            TheRoost.Vagabond.CommandLine.AddCommand("cache", ShowCache);
        }

        static readonly Dictionary<string, List<Token>> cachedSpheres = new Dictionary<string, List<Token>>();
        public const string VERBS_PATH = "~/convenience/verbs";
        public const string VERBS_SLOTS_PATH = "~/convenience/verbSlots";
        public const string VERB_STORAGE_PATH = "~/convenience/verbStorages";
        public const string TABLE_SPHERE_PATH = "~/tabletop";
        public const string EXTANT_PATH = "~/convenience/extant";
        public const string LOCAL_SPHERE_PATH = "~/convenience/local";
        public const string LOCAL_SITUATION_PATH = "~/convenience/localSituation";
        public const string LOCAL_TOKEN_PATH = "~/convenience/localToken";

        public static string PathForVerb(string verbId) { return VERBS_PATH + "/" + verbId; }
        public static string SlotsPathForVerb(string verbId) { return VERBS_SLOTS_PATH + "/" + verbId; }
        public static string StoragePathForVerb(string verbId) { return VERB_STORAGE_PATH + "/" + verbId; }
        public static string SingleSlotPathForVerb(string verbId, string slotId) { return VERBS_SLOTS_PATH + "/" + verbId + "/" + slotId; }

        public static void ResetCache()
        {
            cachedSpheres.Clear();
            HornedAxe hornedaxe = Watchman.Get<HornedAxe>();
            cachedSpheres[TABLE_SPHERE_PATH] = hornedaxe.GetDefaultSphere().GetElementTokens();
            cachedSpheres[LOCAL_SPHERE_PATH] = cachedSpheres[TABLE_SPHERE_PATH];
            cachedSpheres[LOCAL_TOKEN_PATH] = singleTokenList;
            cachedSpheres[EXTANT_PATH] = new List<Token>(cachedSpheres[TABLE_SPHERE_PATH]);

            //situation access convenience caching; they are also cached when all spheres are cached
            foreach (Situation situation in hornedaxe.GetRegisteredSituations())
            {
                string currentVerbPath = PathForVerb(situation.VerbId);
                string currentVerbAllSlotsPath = SlotsPathForVerb(situation.VerbId);
                string currentVerbStoragePath = StoragePathForVerb(situation.VerbId);

                cachedSpheres[currentVerbPath] = situation.GetElementTokensInSituation();
                cachedSpheres[EXTANT_PATH].AddRange(cachedSpheres[currentVerbPath]);

                Sphere storage = situation.GetSingleSphereByCategory(SphereCategory.SituationStorage);
                if (storage == null)
                    storage = situation.GetSingleSphereByCategory(SphereCategory.Output);

                if (storage != null)
                    cachedSpheres[currentVerbStoragePath] = storage.GetElementTokens();

                List<Token> tokensInAllSlots = new List<Token>();
                foreach (Sphere slot in situation.GetSpheresByCategory(SphereCategory.Threshold))
                {
                    List<Token> inSlot = slot.GetElementTokens();
                    string slotPath = SingleSlotPathForVerb(situation.VerbId, slot.Id);
                    cachedSpheres[slotPath] = inSlot;
                    tokensInAllSlots.AddRange(inSlot);
                }
                cachedSpheres[currentVerbAllSlotsPath] = tokensInAllSlots;
            }

            foreach (Sphere sphere in hornedaxe.GetSpheres())
                if (sphere.SphereCategory != SphereCategory.Notes)
                {
                    string spherePath = sphere.GetAbsolutePath().ToString();
                    cachedSpheres[spherePath] = sphere.GetElementTokens();
                }
        }

        public static void SetLocalSituation(Situation situation)
        {
            cachedSpheres[LOCAL_SITUATION_PATH] = situation.GetElementTokensInSituation();
            cachedSpheres[LOCAL_SPHERE_PATH] = cachedSpheres[LOCAL_SITUATION_PATH];
        }

        static readonly List<Token> singleTokenList = new List<Token>();
        public static void SetLocalToken(Token token)
        {
            cachedSpheres[LOCAL_SPHERE_PATH] = singleTokenList;
            singleTokenList.Clear();
            singleTokenList.Add(token);
        }

        public static List<Token> GetReferencedTokens(string path)
        {
            return cachedSpheres[path];
        }

        public static List<Token> FilterTokens(this IEnumerable<Token> tokens, Funcine<bool> filter)
        {
            List<Token> result = new List<Token>();
            foreach (Token token in tokens)
            {
                TokenContextManager.SetLocalToken(token);
                if (filter.result == true)
                    result.Add(token);
            }

            cachedSpheres[LOCAL_SPHERE_PATH] = cachedSpheres[LOCAL_SITUATION_PATH];
            return result;
        }

        public static void LogAllSpheres(params string[] command)
        {
            foreach (Sphere sphere in Watchman.Get<HornedAxe>().GetSpheres())
                Birdsong.Sing(sphere.GetAbsolutePath());
        }

        public static void TestReference(string[] command)
        {
            FucineRef reference = FuncineParser.LoadReference(command[0], "A");
            Birdsong.Sing("Targeting element '{0}' at {1} by filter {2}", reference.targetElementId, reference.targetPath, reference.filter.formula);
        }

        public static void TestExpression(params string[] command)
        {
            TokenContextManager.ResetCache();
            TokenContextManager.SetLocalSituation(SecretHistories.NullObjects.NullSituation.Create());
            Funcine<int> test = new Funcine<int>(command[0]);
            Birdsong.Sing("{0} = {1}", test.formula, test.result);
        }

        public static void TestExpressionForCurrentCache(params string[] command)
        {
            Funcine<int> test = new Funcine<int>(command[0]);
            Birdsong.Sing("{0} = {1}", test.formula, test.result);
        }

        public static void SphereContent(params string[] command)
        {
            string path = command[0];
            HornedAxe hornedaxe = Watchman.Get<HornedAxe>();
            Sphere sphere = hornedaxe.GetSphereByPath(new FucinePath(path));
            if (sphere == hornedaxe.GetDefaultSphere() && path != hornedaxe.GetDefaultSpherePath().ToString())
            {
                Birdsong.Sing("Unknown sphere {0}", path);
                return;
            }

            foreach (Token token in sphere.GetTokens())
                Birdsong.Sing(token.PayloadEntityId);
        }

        public static void ShowCache(params string[] command)
        {
            TokenContextManager.ResetCache();
            if (command == null || command.Length == 0)
            {
                foreach (string cachedSphere in cachedSpheres.Keys)
                {
                    Birdsong.Sing(cachedSphere);
                    foreach (Token token in cachedSpheres[cachedSphere])
                        Birdsong.Sing("     ", token.PayloadEntityId);
                }
                return;
            }

            string path = FuncineParser.InterpretReferencePath(ref command[0]);
            if (cachedSpheres.ContainsKey(path))
            {
                Birdsong.Sing(command[0]);
                foreach (Token token in cachedSpheres[path])
                    Birdsong.Sing("     ", token.PayloadEntityId);
            }
            else
                Birdsong.Sing("Path '{0}' - insterpreted as '{1}' - isn't present in the cache", command[0], path);
        }
    }
}