using System;
using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Fucine;
using SecretHistories.Spheres;
using SecretHistories.Enums;
using Roost.Twins.Entities;

namespace Roost.Twins
{
    public static class TokenContextAccessors
    {
        private static bool initialized = false;
        internal static void Enact()
        {
            if (!initialized)
            {
                Roost.Vagabond.CommandLine.AddCommand("ref", TwinsDebug.TestReference);
                Roost.Vagabond.CommandLine.AddCommand("exp", TwinsDebug.TestExpression);
                Roost.Vagabond.CommandLine.AddCommand("sphere", TwinsDebug.SphereFind);
                initialized = true;
            }
        }

        public static readonly FucinePath localSphere = new FucinePath("~/localSphere");
        public static readonly FucinePath localSituation = new FucinePath("~/localSituation");
        public static readonly FucinePath localToken = new FucinePath("~/localToken");

        private static readonly SingleTokenSphere localTokenSphere = new SingleTokenSphere();
        private static readonly List<Sphere> defaultScope = new List<Sphere> { Assets.Scripts.Application.Entities.NullEntities.NullSphere.Create() };

        private static Dictionary<FucinePath, List<Sphere>> convenienceSpheres = new Dictionary<FucinePath, List<Sphere>>()
            {
                {  localSphere, null },
                {  localSituation, null },
                {  localToken, new List<Sphere> { localTokenSphere } }
            };
        private static Dictionary<FucinePath, List<Sphere>> cachedSpheres = new Dictionary<FucinePath, List<Sphere>>();

        public static List<Sphere> GetSpheresByPath(FucinePath path)
        {
            if (cachedSpheres.ContainsKey(path))
                return cachedSpheres[path];
            if (convenienceSpheres.ContainsKey(path))
                return convenienceSpheres[path];

            List<Sphere> result = new List<Sphere>();

            if (path is FucineMultiPath)
            {
                Func<Sphere, FucinePath> comparisonPath;
                if (path.IsWild())
                    comparisonPath = sphere => sphere.GetWildPath();
                else
                    comparisonPath = sphere => sphere.GetAbsolutePath();

                int maxAmount = ((FucineMultiPath)path).maxSpheresToFind;

                string desiredPath = path.ToString();

                foreach (Sphere sphere in Watchman.Get<HornedAxe>().GetSpheres())
                    if (comparisonPath(sphere).ToString().Contains(desiredPath))
                    {
                        result.Add(sphere);

                        maxAmount--;
                        if (maxAmount == 0)
                            break;
                    }
            }
            else
            {
                FucinePath defaultSpherePath = Watchman.Get<HornedAxe>().GetDefaultSpherePath();
                Sphere sphere = Watchman.Get<HornedAxe>().GetSphereByAbsolutePath(path);

                if (sphere.GetAbsolutePath() != defaultSpherePath || path == defaultSpherePath)
                    result.Add(sphere);
            }

            cachedSpheres.Add(path, result);
            return result;
        }

        public static List<Token> GetTokensByPath(FucinePath path)
        {
            List<Token> tokens = new List<Token>();
            List<Sphere> spheres = GetSpheresByPath(path);
            foreach (Sphere sphere in spheres)
                tokens.AddRange(sphere.Tokens);

            return tokens;
        }

        public static void SetLocalSituation(Situation situation)
        {
            convenienceSpheres[localSituation] = situation.GetSpheresActiveForCurrentState();
            convenienceSpheres[localSphere] = convenienceSpheres[localSituation];
        }

        public static void ResetLocalSituation()
        {
            convenienceSpheres[localSphere] = defaultScope;
            convenienceSpheres[localSituation] = defaultScope;
        }

        public static void SetLocalToken(Token token)
        {
            localTokenSphere.Set(token);
            convenienceSpheres[localSphere] = convenienceSpheres[localToken];
        }

        public static void ResetLocalToken()
        {
            convenienceSpheres[localSphere] = convenienceSpheres[localSituation];
            localTokenSphere.Clear();
        }

        public static void ResetCache()
        {
            cachedSpheres.Clear();
        }

        public static List<Token> FilterTokens(this IEnumerable<Token> tokens, Funcine<bool> filter)
        {
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
        public FucineMultiPath(string path, int maxSpheresToFind) : base(path) { this.maxSpheresToFind = maxSpheresToFind; }
        public readonly int maxSpheresToFind;
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

            if (command.Length == 0)
            {
                foreach (Sphere sphere in Watchman.Get<HornedAxe>().GetSpheres())
                    result += $"Sphere '{sphere.GetAbsolutePath()}', wild path '{sphere.GetWildPath()}'\n";

                Birdsong.Sing(result);

                return;
            }

            FucinePath path = FuncineParser.ParseFuncineSpherePath(command[0]);

            List<Sphere> foundSpheres = TokenContextAccessors.GetSpheresByPath(path);
            if (foundSpheres.Count == 0)
            {
                Birdsong.Sing($"No spheres found for path '{path}'");
                return;
            }

            foreach (Sphere sphere in foundSpheres)
            {
                result += $"Found sphere {sphere.GetAbsolutePath()}, wild path '{sphere.GetWildPath()}', content:\n";
                foreach (Token token in sphere.GetTokens())
                    result += $"{token.PayloadTypeName} {token.PayloadEntityId}\n";
            }
            Birdsong.Sing(result);
        }
    }

    public class SingleTokenSphere : Sphere
    {

        public override SphereCategory SphereCategory { get { return SphereCategory.Meta; } }

        public void Set(Token token)
        {
            _tokens.Clear();
            _tokens.Add(token);
        }

        public void Clear()
        {
            _tokens.Clear();
        }
    }
}