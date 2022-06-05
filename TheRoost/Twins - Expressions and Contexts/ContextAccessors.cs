using System;
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
    public static class Crossroads
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
                postfix: typeof(Crossroads).GetMethodInvariant(nameof(ClearVerbThresholds)));
            Machine.Patch(
                original: typeof(CompleteState).GetMethodInvariant(nameof(CompleteState.Exit)),
                postfix: typeof(Crossroads).GetMethodInvariant(nameof(ClearOutput)));
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

        public const string currentSituation = "~/situation";
        public const string currentSphere = "~/sphere";
        public const string currentToken = "~/token";
        public const string currentScope = "~/local";

        private static readonly List<Sphere> localTokenContainer = new List<Sphere> { new TokenFakeSphere() };
        public static readonly List<Sphere> defaultSphereContainer = new List<Sphere>();

        private static readonly Dictionary<string, List<Sphere>> cachedSpheres = new Dictionary<string, List<Sphere>>()
        {
            { currentSituation, null },
            { currentSphere, defaultSphereContainer },
            { currentToken, localTokenContainer },
            { currentScope, defaultSphereContainer }
        };

        public static List<Sphere> GetSpheresByPath(FucinePath path)
        {
            string pathString = path.ToString();
            if (cachedSpheres.ContainsKey(pathString))
                return cachedSpheres[pathString];

            List<Sphere> result;
            if (path is FucinePathPlus)
                result = (path as FucinePathPlus).GetSpheresSpecial();
            else
            {
                result = new List<Sphere>();
                Sphere sphere = Watchman.Get<HornedAxe>().GetSphereByAbsolutePath(path);
                //the game (unhelpfully) returns the default (tabletop) sphere when no sphere is found; gotta recheck that the sphere is correct
                //also I find this ironic that the Twins here require an assistance from the Horned Axe
                if (sphere.GetAbsolutePath() == path || sphere.GetWildPath() == path)
                    result.Add(sphere);
            }

            cachedSpheres.Add(pathString, result);
            return result;
        }

        public static List<Token> GetTokensByPath(FucinePath path)
        {
            List<Token> tokens = new List<Token>();
            List<Sphere> spheres = GetSpheresByPath(path);
            foreach (Sphere sphere in spheres)
                tokens.AddRange(sphere.GetTokens());

            return tokens;
        }

        public static void MarkLocalSituation(Situation situation)
        {
            cachedSpheres[currentSituation] = situation.GetSpheresActiveForCurrentState();
            MarkLocalScope(cachedSpheres[currentSituation]);
        }

        public static void MarkLocalSphere(Sphere sphere)
        {
            cachedSpheres[currentSphere][0] = sphere;
            MarkLocalScope(cachedSpheres[currentSphere]);
        }

        public static void MarkLocalToken(Token token)
        {
            (localTokenContainer[0] as TokenFakeSphere).Set(token);
            MarkLocalScope(cachedSpheres[currentToken]);
        }

        public static void MarkLocalScope(List<Sphere> sphere)
        {
            cachedSpheres[currentScope] = sphere;
        }

        public static void UnmarkLocalSphere()
        {
            cachedSpheres[currentSphere].Clear();
            MarkLocalScope(cachedSpheres[currentSituation]);
        }

        public static void UnmarkLocalToken()
        {
            (localTokenContainer[0] as TokenFakeSphere).Clear();
            MarkLocalScope(cachedSpheres[currentSphere]);
        }

        public static void ResetCache()
        {
            cachedSpheres.Clear();
            cachedSpheres[currentToken] = localTokenContainer;
            cachedSpheres[currentSphere] = defaultSphereContainer;
            cachedSpheres[currentScope] = defaultSphereContainer;
        }

        public static List<Token> FilterTokens(this List<Token> tokens, FucineExp<bool> filter)
        {
            if (filter.isUndefined)
                return tokens;

            List<Token> result = new List<Token>();
            foreach (Token token in tokens)
            {
                MarkLocalToken(token);
                if (filter.value == true)
                    result.Add(token);
            }

            UnmarkLocalToken();
            return result;
        }
    }

    public class TokenFakeSphere : Sphere
    {
        public override SphereCategory SphereCategory { get { return SphereCategory.Meta; } }

        public void Set(Token token) { _tokens.Clear(); _tokens.Add(token); }
        public void Clear() { _tokens.Clear(); }
    }

    internal class TwinsDebug
    {
        public static void TestReference(string[] command)
        {
            string path = string.Concat(command);
            try
            {
                FucineRef reference = ExpressionsParser.ParseFucineRef(path, "A");
                Birdsong.Tweet($"Reference value '{reference.value}'");
            }
            catch (Exception ex)
            {
                Birdsong.Tweet(ex.FormatException());
            }
        }

        public static void TestExpression(params string[] command)
        {
            string formula = string.Concat(command);
            Birdsong.Tweet(new FucineExp<float>(formula));
        }

        public static void SphereFind(params string[] command)
        {
            string result = string.Empty;

            List<Sphere> foundSpheres;
            if (command.Length == 0)
                foundSpheres = new List<Sphere>(Watchman.Get<HornedAxe>().GetSpheres());
            else
            {
                FucinePath targetPath = ExpressionsParser.ParseSpherePath(command[0]);

                DateTime startTime = DateTime.Now;
                foundSpheres = Crossroads.GetSpheresByPath(targetPath);
                TimeSpan searchTime = DateTime.Now - startTime;

                result += $"Search time {searchTime.TotalSeconds} sec\n";
            }

            if (foundSpheres.Count > 0)
                foreach (Sphere sphere in foundSpheres)
                    result += $"{sphere.SphereCategory.ToString().ToUpper()} SPHERE ID '{sphere.Id}'\nPath: '{sphere.GetAbsolutePath()}'\nWild: '{sphere.GetWildPath()}'\n";

            Birdsong.Tweet(result);
        }

        public static void SphereContent(params string[] command)
        {
            string result = string.Empty;

            if (command.Length == 0)
            {
                Birdsong.Tweet("Empty sphere reference");
                return;
            }

            FucinePath path = ExpressionsParser.ParseSpherePath(command[0]);

            List<Sphere> foundSpheres = Crossroads.GetSpheresByPath(path);

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

            Birdsong.Tweet(result);
        }
    }
}