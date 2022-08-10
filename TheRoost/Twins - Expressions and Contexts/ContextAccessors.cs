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
                original: typeof(StartingState).GetMethodInvariant(nameof(StartingState.Enter)),
                postfix: typeof(Crossroads).GetMethodInvariant(nameof(ClearVerbThresholds)));
            Machine.Patch(
                original: typeof(CompleteState).GetMethodInvariant(nameof(CompleteState.Exit)),
                postfix: typeof(Crossroads).GetMethodInvariant(nameof(ClearOutput)));
        }

        private static readonly string VERB_THRESHOLDS_SPHERE = SituationDominionEnum.VerbThresholds.ToString();
        private static readonly string OUTPUT_SPHERE = SituationDominionEnum.Output.ToString();
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
        public const string currentTokens = "~/tokens";
        public const string currentToken = "~/token";
        public const string currentScope = "~/local";

        private static readonly List<Sphere> localSingleTokenContainer = new List<Sphere> { new TokenFakeSphere() };
        private static readonly List<Sphere> allLocalTokensContainer = new List<Sphere> { new TokenFakeSphere() };
        public static readonly List<Sphere> defaultSphereContainer = new List<Sphere>();

        private static readonly Dictionary<string, List<Sphere>> cachedSpheres = new Dictionary<string, List<Sphere>>()
        {
            { currentSituation, null },
            { currentSphere, defaultSphereContainer },
            { currentTokens, allLocalTokensContainer },
            { currentToken, localSingleTokenContainer },
            { currentScope, defaultSphereContainer }
        };

        public static List<Sphere> GetSpheresByPath(FucinePath fucinePath)
        {
            string fullPath = fucinePath.ToString();
            if (cachedSpheres.ContainsKey(fullPath))
                return cachedSpheres[fullPath];

            List<Sphere> result = new List<Sphere>();
            if (fucinePath is FucinePathPlus)
            {
                FucinePathPlus pathPlus = fucinePath as FucinePathPlus;

                string sphereMask = pathPlus.sphereMask;
                int maxAmount = pathPlus.maxSpheresToFind;

                foreach (Sphere sphere in Watchman.Get<HornedAxe>().GetSpheres())
                    if (pathPlus.AcceptsCategory(sphere.SphereCategory) && !result.Contains(sphere)
                        && sphere.GetAbsolutePath().Path.IndexOf(sphereMask) != -1)
                    {
                        result.Add(sphere);

                        maxAmount--;
                        if (maxAmount == 0)
                            break;
                    }
            }
            else
            {
                Sphere sphere = Watchman.Get<HornedAxe>().GetSphereByAbsolutePath(fucinePath);
                //the game (unhelpfully) returns the default (tabletop) sphere when no sphere is found; gotta recheck that the sphere is correct
                //also I find this ironic that the Twins here require an assistance from the Horned Axe
                if (sphere.GetAbsolutePath() == fucinePath || sphere.GetWildPath() == fucinePath)
                    result.Add(sphere);
            }

            cachedSpheres[fullPath] = result;
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

        public static void MarkAllLocalTokens(List<Token> tokens)
        {
            (allLocalTokensContainer[0] as TokenFakeSphere).Set(tokens);
        }

        public static void MarkLocalToken(Token token)
        {
            (localSingleTokenContainer[0] as TokenFakeSphere).Set(token);
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

        public static void UnmarkAllLocalTokens()
        {
            allLocalTokensContainer[0].RetireAllTokens(); //don't be afraid, the method is overriden to just clear the list
            localSingleTokenContainer[0].RetireAllTokens(); //don't be afraid, the method is overriden to just clear the list
            MarkLocalScope(cachedSpheres[currentSphere]);
        }

        public static void UnmarkLocalToken()
        {
            localSingleTokenContainer[0].RetireAllTokens(); //don't be afraid, the method is overriden to just clear the list
            MarkLocalScope(cachedSpheres[currentSphere]);
        }

        public static void ResetCache()
        {
            cachedSpheres.Clear();
            cachedSpheres[currentTokens] = allLocalTokensContainer;
            cachedSpheres[currentToken] = localSingleTokenContainer;
            cachedSpheres[currentSphere] = defaultSphereContainer;
            cachedSpheres[currentScope] = defaultSphereContainer;
        }
    }

    public class TokenFakeSphere : Sphere
    {
        public override SphereCategory SphereCategory { get { return SphereCategory.Meta; } }
        public void Set(List<Token> token) { _tokens.Clear(); _tokens.AddRange(token); }
        public void Set(Token token) { _tokens.Clear(); _tokens.Add(token); }
        public override void RetireAllTokens() { _tokens.Clear(); }
    }

    internal class TwinsDebug
    {
        public static void TestReference(params string[] command)
        {
            string path = string.Concat(command);
            try
            {
                FucineRef reference = new FucineRef(path);
                Birdsong.Tweet($"Reference value '{reference.value}'");
                Crossroads.ResetCache();
            }
            catch (Exception ex)
            {
                Birdsong.Tweet(ex.FormatException());
            }
        }

        public static void TestExpression(params string[] command)
        {
            try
            {
                string formula = string.Concat(command);
                Birdsong.Tweet(new FucineExp<float>(formula));
                Crossroads.ResetCache();
            }
            catch (Exception ex)
            {
                Birdsong.Tweet(ex.FormatException());
            }
        }

        public static void SphereFind(params string[] command)
        {
            string result = string.Empty;

            List<Sphere> foundSpheres;
            if (command.Length == 0)
                foundSpheres = new List<Sphere>(Watchman.Get<HornedAxe>().GetSpheres());
            else
            {
                FucinePath path = TwinsParser.ParseSpherePath(command[0]);

                DateTime startTime = DateTime.Now;
                foundSpheres = Crossroads.GetSpheresByPath(path);
                TimeSpan searchTime = DateTime.Now - startTime;

                result += $"Search time {searchTime.TotalSeconds} sec\n";
                if (foundSpheres.Count == 0)
                    result = $"No spheres found for path '{path}'\n";
            }

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

            FucinePath path = TwinsParser.ParseSpherePath(command[0]);

            List<Sphere> foundSpheres = Crossroads.GetSpheresByPath(path);

            if (foundSpheres.Count > 0)
                foreach (Sphere sphere in foundSpheres)
                {
                    result += $"{sphere.SphereCategory.ToString().ToUpper()} SPHERE ID '{sphere.Id}:'\n--------------\n";
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