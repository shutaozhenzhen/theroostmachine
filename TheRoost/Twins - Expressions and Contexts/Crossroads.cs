using System;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Fucine;
using SecretHistories.Spheres;
using SecretHistories.Enums;
using SecretHistories.Entities.NullEntities;
using SecretHistories.States;
using SecretHistories.Commands.SituationCommands;

using UnityEngine;

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

            AtTimeOfPower.TabletopSceneInit.Schedule(ResetCache, PatchType.Prefix);
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

        public static List<Sphere> GetSpheresByPath(this FucinePath fucinePath)
        {
            string fullPath = fucinePath.ToString();
            if (cachedSpheres.ContainsKey(fullPath))
                return new List<Sphere>(cachedSpheres[fullPath]);

            List <Sphere> result;
            if (specialSpheres.ContainsKey(fullPath))
            {
                result = specialSpheres[fullPath](); //special spheres are already wrapped in a new List
            }
            else if (fucinePath is FucinePathPlus)
            {
               
                result = new List<Sphere>();
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
                result = new List<Sphere>();
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
            allLocalTokens.Set(tokens);
        }

        public static void MarkLocalToken(Token token)
        {
            singleLocalToken.Set(token);
            MarkLocalScope(singleLocalToken);
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
            singleLocalToken.Reset();
            allLocalTokens.Reset();
            MarkLocalScope(cachedSpheres[currentSphere]);
        }

        public static void UnmarkLocalToken()
        {
            singleLocalToken.Reset();
            MarkLocalScope(cachedSpheres[currentSphere]);
        }

        public const string currentSituation = "~/situation";
        public const string currentSphere = "~/sphere";
        public const string currentTokens = "~/tokens";
        public const string currentToken = "~/token";
        public const string currentScope = "~/local";

        private static readonly List<Sphere> nullContainer = new List<Sphere>();
        private static readonly FakeSphereList singleLocalToken = new FakeSphereList();
        private static readonly FakeSphereList allLocalTokens = new FakeSphereList();

        private static readonly Dictionary<string, List<Sphere>> cachedSpheres = new Dictionary<string, List<Sphere>>();
        public static void ResetCache()
        {
            cachedSpheres.Clear();
            nullContainer.Clear();
            nullContainer.Add(NullSphere.Create());

            cachedSpheres[currentSituation] = nullContainer;
            cachedSpheres[currentTokens] = allLocalTokens;
            cachedSpheres[currentToken] = singleLocalToken;
            cachedSpheres[currentSphere] = specialSpheres["~/default"]();
            cachedSpheres[currentScope] = cachedSpheres[currentSphere];
        }

        private static readonly Dictionary<string, Func<List<Sphere>>> specialSpheres = new Dictionary<string, Func<List<Sphere>>>
        {
            { "~/extant",
                () => new List<Sphere>(Watchman.Get<HornedAxe>().GetSpheres().Where(sphere => !sphere.IsCategory(SphereCategory.Dormant | SphereCategory.Notes))) },

            { "~/exterior",
                () => new List<Sphere>(Watchman.Get<HornedAxe>().GetExteriorSpheres()) },

            { "~/default",
                () => new List<Sphere> { Watchman.Get<HornedAxe>().GetDefaultSphereForUnknownToken() } },
        };
    }

    public class FakeSphereList : List<Sphere>
    {
        FakeSphere sphere;

        public FakeSphereList()
        {
            //holy jesus freaking christ
            //dear diary, I am writing this with a trembling hand and a sinking heart 
            //a classic mistake is to treat Spheres as an independent objects that can be constructed directly - var s = new Sphere()
            //Gen made it, I made it too, here, long ago
            //Spheres, however, are MonoBehaviors, a component on a GameObject
            //therefore, they possibly can't be instantiated without a GameObject to accompany them, as it's done here below
            //constructing a component without a GO just makes it vanish, they turn into a null at the same instant of their creation
            //or do they? do they?
            //in truth, they never cease to exist; but, oh, how terrifying their existence is!
            //everything will tell they are null; "obj == null" will return true
            //but everything, nevertheless, will still operate with it as with a valid class -  you can store variables; you can execute methods
            //in its own method, you can ask: 'this == null'? and the answer will be 'true'
            //there are no words in this world to describe how wrong this is

            var fakeSphere = new GameObject();
            fakeSphere.name = "Fake Token Sphere";
            sphere = fakeSphere.AddComponent<FakeSphere>();

            UnityEngine.Object.DontDestroyOnLoad(fakeSphere);

            this.Add(sphere);
        }

        public void Set(List<Token> tokens)
        {
            sphere.Set(tokens);
        }

        public void Set(Token token)
        {
            sphere.Set(token);
        }

        public void Reset()
        {
            sphere.Reset();
        }

        class FakeSphere : Sphere
        {
            public override SphereCategory SphereCategory { get { return SphereCategory.Meta; } }
            public override bool IsValid => false;
            public void Set(List<Token> tokens)
            {
                _tokens.Clear();
                _tokens.AddRange(tokens);
            }

            public void Set(Token token)
            {
                _tokens.Clear();
                _tokens.Add(token);
            }

            public void Reset()
            {
                _tokens.Clear();
            }

            public override void AcceptToken(Token token, Context context)
            {
                //in some occassions, FakeSphere is a local sphere and is targeted by the effects
                //we don't want it to actually accept any tokens
                if (_tokens.Count > 0)
                {
                    if (!_tokens[0].Sphere.TryAcceptToken(token, context))
                        _tokens[0].Sphere.ProcessEvictedToken(token, context);
                }
                else
                    Watchman.Get<HornedAxe>().GetDefaultSphereForUnknownToken().ProcessEvictedToken(token, context);
            }

            public override string ToString()
            {
                return "FakeSphere";
            }
        }
    }



    internal class TwinsDebug
    {
        public static void TestReference(params string[] command)
        {
            string path = string.Concat(command);
            try
            {
                FucineRef reference = new FucineRef(path);
                Birdsong.TweetLoud($"Reference value '{reference.value}'");
                Crossroads.ResetCache();
            }
            catch (Exception ex)
            {
                Birdsong.TweetLoud(ex.FormatException());
            }
        }

        public static void TestExpression(params string[] command)
        {
            try
            {
                string formula = string.Concat(command);
                Birdsong.TweetLoud(new FucineExp<int>(formula));
                Crossroads.ResetCache();
            }
            catch (Exception ex)
            {
                Birdsong.TweetLoud(ex.FormatException());
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

            Birdsong.TweetLoud(result);
        }

        public static void SphereContent(params string[] command)
        {
            string result = string.Empty;

            if (command.Length == 0)
            {
                Birdsong.TweetLoud("Empty sphere reference");
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

            Birdsong.TweetLoud(result);
        }
    }
}