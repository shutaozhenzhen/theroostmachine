using System;
using System.Linq;
using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Fucine;
using SecretHistories.Spheres;
using SecretHistories.Enums;
using SecretHistories.States;
using SecretHistories.Commands.SituationCommands;

using Roost.Twins.Entities;

namespace Roost.Twins
{
    public static class TokenContextAccessors
    {
        internal static void Enact()
        {
            Roost.Vagabond.CommandLine.AddCommand("ref", TwinsDebug.TestReference);
            Roost.Vagabond.CommandLine.AddCommand("exp", TwinsDebug.TestExpression);
            Roost.Vagabond.CommandLine.AddCommand("sphere", TwinsDebug.SphereFind);
            Roost.Vagabond.CommandLine.AddCommand("spheres", TwinsDebug.SphereFind);
            Roost.Vagabond.CommandLine.AddCommand("tokens", TwinsDebug.SphereContent);

            //the game keeps slot and output spheres even when they are unused; to avoid confusion in references, clear them
            Machine.Patch(
                original: typeof(OngoingState).GetMethodInvariant(nameof(OngoingState.Enter)),
                postfix: typeof(TokenContextAccessors).GetMethodInvariant(nameof(ClearVerbThresholds)));
            Machine.Patch(
                original: typeof(CompleteState).GetMethodInvariant(nameof(CompleteState.Exit)),
                postfix: typeof(TokenContextAccessors).GetMethodInvariant(nameof(ClearOutput)));
        }

        static readonly string VERB_THRESHOLDS_SPHERE = SituationDominionEnum.VerbThresholds.ToString();
        static readonly string RECIPE_THRESHOLDS_SPHERE = SituationDominionEnum.RecipeThresholds.ToString();
        static readonly string OUTPUT_SPHERE = SituationDominionEnum.Output.ToString();
        static void ClearVerbThresholds(Situation situation)
        {
            situation.AddCommand(new ClearDominionCommand(VERB_THRESHOLDS_SPHERE, SphereRetirementType.Graceful));
        }
        static void ClearOutput(Situation situation)
        {
            situation.AddCommand(new ClearDominionCommand(OUTPUT_SPHERE, SphereRetirementType.Graceful));
        }

        public static readonly FucinePath currentLocal = new FucinePath("*/local");
        public static readonly FucinePath currentSituation = new FucinePath("*/situation");
        public static readonly FucinePath currentToken = new FucinePath("*/token");

        private static readonly SingleTokenSphere localTokenSphere = new SingleTokenSphere();
        private static Dictionary<FucinePath, HashSet<Sphere>> convenienceSpheres = new Dictionary<FucinePath, HashSet<Sphere>>()
            {
                {  currentLocal, null },
                {  currentSituation, null },
                {  currentToken, new HashSet<Sphere> { localTokenSphere } }
            };
        private static Dictionary<FucinePath, HashSet<Sphere>> cachedSpheres = new Dictionary<FucinePath, HashSet<Sphere>>();

        public static HashSet<Sphere> GetSpheresByPath(FucinePath path)
        {
            if (cachedSpheres.ContainsKey(path))
                return cachedSpheres[path];
            if (convenienceSpheres.ContainsKey(path))
                return convenienceSpheres[path];

            HashSet<Sphere> result;

            if (path is FucineMultiPath)
                result = (path as FucineMultiPath).GetSpheresByPath();
            else
            {
                result = new HashSet<Sphere>();
                Sphere sphere = Watchman.Get<HornedAxe>().GetSphereByAbsolutePath(path);
                //the game (unhelpfully) returns the default (tabletop) sphere when no sphere is found; gotta recheck that the sphere is correct
                if (sphere.GetAbsolutePath() == path || sphere.GetWildPath() == path)
                    result.Add(sphere);
            }

            cachedSpheres.Add(path, result);
            return result;
        }

        public static List<Token> GetTokensByPath(FucinePath path)
        {
            List<Token> tokens = new List<Token>();
            HashSet<Sphere> spheres = GetSpheresByPath(path);
            foreach (Sphere sphere in spheres)
                tokens.AddRange(sphere.Tokens);

            return tokens;
        }

        public static void SetLocalSituation(Situation situation)
        {
            convenienceSpheres[currentSituation] = new HashSet<Sphere>(situation.GetSpheresActiveForCurrentState());
            convenienceSpheres[currentLocal] = convenienceSpheres[currentSituation];
        }

        public static void SetLocalToken(Token token)
        {
            localTokenSphere.Set(token);
            convenienceSpheres[currentLocal] = convenienceSpheres[currentToken];
        }

        public static void ResetLocalToken()
        {
            convenienceSpheres[currentLocal] = convenienceSpheres[currentSituation];
            localTokenSphere.Clear();
        }

        public static void ResetCache()
        {
            cachedSpheres.Clear();
        }

        public static List<Token> FilterTokens(this List<Token> tokens, Funcine<bool> filter)
        {
            if (filter.isUndefined)
                return tokens;

            List<Token> result = new List<Token>();

            foreach (Token token in tokens)
            {
                SetLocalToken(token);
                if (filter.value == true)
                    result.Add(token);
            }

            ResetLocalToken();

            return result;
        }
    }

    public class FucineMultiPath : FucinePath
    {
        public FucineMultiPath(string path, int maxSpheresToFind, List<SphereCategory> acceptable = null, List<SphereCategory> excluded = null) : base(path)
        {
            this.maxSpheresToFind = maxSpheresToFind;

            Birdsong.Sing(acceptable?.Count, excluded?.Count);

            acceptableCategories = acceptable ?? defaultAcceptableCategories;
            excludedSphereCategories = excluded ?? defaultExcludedCategories;

            if (this.IsAbsolute())
                GetRelevantSpherePath = getAbsolutePath;
            else
                GetRelevantSpherePath = getWildPath;
        }
        public readonly int maxSpheresToFind;

        public List<SphereCategory> acceptableCategories;
        public List<SphereCategory> excludedSphereCategories;

        static readonly SphereCategory[] allSphereCategories = (SphereCategory[])Enum.GetValues(typeof(SphereCategory));
        private static readonly List<SphereCategory> defaultAcceptableCategories = allSphereCategories.ToList();
        private static readonly List<SphereCategory> defaultExcludedCategories = new List<SphereCategory> { SphereCategory.Notes };

        private static readonly Func<Sphere, string> getAbsolutePath = sphere => sphere.GetAbsolutePath().ToString();
        private static readonly Func<Sphere, string> getWildPath = sphere => sphere.GetWildPath().ToString();
        private Func<Sphere, string> GetRelevantSpherePath;

        public HashSet<Sphere> GetSpheresByPath()
        {
            HashSet<Sphere> result = new HashSet<Sphere>();
            string pathMask = this.ToString();
            int maxAmount = maxSpheresToFind;

            foreach (Sphere sphere in Watchman.Get<HornedAxe>().GetSpheres())
                if (!excludedSphereCategories.Contains(sphere.SphereCategory) && acceptableCategories.Contains(sphere.SphereCategory) && !result.Contains(sphere)
                    && GetRelevantSpherePath(sphere).Contains(pathMask))
                {
                    result.Add(sphere);

                    maxAmount--;
                    if (maxAmount == 0)
                        break;
                }

            return result;
        }


        //it'd be handy to implement the ability to include nested spheres for wild paths
        private HashSet<Sphere> FindSubSpheres(Sphere sphere)
        {
            HashSet<Sphere> spheres = new HashSet<Sphere>();
            foreach (Token token in sphere.GetTokens())
                foreach (SphereCategory category in allSphereCategories)
                    if (category != SphereCategory.Notes)
                    {
                        List<Sphere> subSpheres = token.Payload.GetSpheresByCategory(category);
                        spheres.UnionWith(subSpheres);
                        foreach (Sphere subSphere in subSpheres)
                            spheres.UnionWith(FindSubSpheres(subSphere));
                    }

            return spheres;
        }
    }

    internal class TwinsDebug
    {
        public static void TestReference(string[] command)
        {
            string path = string.Concat(command);
            try
            {
                FuncineRef reference = FuncineParser.ParseFuncineRef(path, "A");
                Birdsong.Sing($"Reference value '{reference.value}'");
            }
            catch (Exception ex)
            {
                Birdsong.Sing(ex.FormatException());
            }
        }

        public static void TestExpression(params string[] command)
        {
            string formula = string.Concat(command);
            Birdsong.Sing(new Funcine<float>(formula));
        }

        public static void SphereFind(params string[] command)
        {
            string result = string.Empty;

            HashSet<Sphere> foundSpheres;
            if (command.Length == 0)
                foundSpheres = Watchman.Get<HornedAxe>().GetSpheres();
            else
            {
                FucinePath targetPath = FuncineParser.ParseFuncineSpherePath(command[0]);

                DateTime startTime = DateTime.Now;
                foundSpheres = TokenContextAccessors.GetSpheresByPath(targetPath);
                TimeSpan searchTime = DateTime.Now - startTime;

                result += $"Search time {searchTime.TotalSeconds} sec\n";
            }

            if (foundSpheres.Count > 0)
                foreach (Sphere sphere in foundSpheres)
                    result += $"{sphere.SphereCategory.ToString().ToUpper()} SPHERE ID '{sphere.Id}'\nPath: '{sphere.GetAbsolutePath()}'\nWild: '{sphere.GetWildPath()}'\n";

            Birdsong.Sing(result);
        }

        public static void SphereContent(params string[] command)
        {
            string result = string.Empty;

            if (command.Length == 0)
            {
                Birdsong.Sing("Empty sphere reference");
                return;
            }

            FucinePath path = FuncineParser.ParseFuncineSpherePath(command[0]);

            HashSet<Sphere> foundSpheres = TokenContextAccessors.GetSpheresByPath(path);

            if (foundSpheres.Count > 0)
                foreach (Sphere sphere in foundSpheres)
                {
                    result += $"{sphere.Id}:'\n--------------";
                    foreach (Token token in sphere.GetTokens())
                        result += $"{token.PayloadTypeName} {token.PayloadEntityId}\n";
                    result += "--------------\n";
                }
            else
                result = $"No spheres found for path '{path}'\n";

            Birdsong.Sing(result);
        }
    }

    public class SingleTokenSphere : Sphere
    {
        public override SphereCategory SphereCategory { get { return SphereCategory.Meta; } }

        public void Set(Token token) { _tokens.Clear(); _tokens.Add(token); }
        public void Clear() { _tokens.Clear(); }
    }
}