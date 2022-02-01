﻿using System.Collections.Generic;
using System.Linq;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Fucine;
using SecretHistories.Spheres;

using TheRoost.Twins.Entities;

namespace TheRoost.Twins
{
    public static class TokenContextAccessors
    {
        private static Situation local_situation = SecretHistories.NullObjects.NullSituation.Create();
        private static readonly List<Token> local_token = new List<Token>();
        private static List<Token> local_sphere = new List<Token>();

        public static List<Token> GetTableTokens()
        {
            return Watchman.Get<HornedAxe>().GetDefaultSphere().GetElementTokens();
        }

        public static List<Token> GetExtantTokens()
        {
            List<Token> tokens = GetTableTokens();
            foreach (Situation situation in Watchman.Get<HornedAxe>().GetRegisteredSituations())
                tokens.AddRange(situation.GetElementTokensInSituation());

            return tokens;
        }

        public static Situation GetSituation(string verbId)
        {
            foreach (Situation situation in Watchman.Get<HornedAxe>().GetSituationsWithVerbOfActionId(verbId))
                return situation;

            return SecretHistories.NullObjects.NullSituation.Create();
        }

        public static Sphere GetSituationSlot(this Situation situation, string slotId)
        {
            foreach (Sphere sphere in situation.GetSpheresByCategory(SecretHistories.Enums.SphereCategory.Threshold))
                if (sphere.Id == slotId)
                    return sphere;

            return Assets.Scripts.Application.Entities.NullEntities.NullSphere.Create();
        }

        public static List<Token> GetSpheresTokens(this List<Sphere> spheres)
        {
            List<Token> result = new List<Token>();
            foreach (Sphere sphere in spheres)
                result.AddRange(sphere.GetTokens());

            return result;
        }

        public static List<Token> GetSituationStorageTokens(this Situation situation)
        {
            List<Token> result = new List<Token>();
            Sphere storage = situation.GetSingleSphereByCategory(SecretHistories.Enums.SphereCategory.SituationStorage);
            if (storage != null)
                result.AddRange(storage.GetElementTokens());
            else
            {
                storage = situation.GetSingleSphereByCategory(SecretHistories.Enums.SphereCategory.Output);
                if (storage != null)
                    result.AddRange(storage.GetTokens());
            }

            return result;
        }

        public static List<Token> GetDeckTokens(string deckId)
        {
            return Watchman.Get<SecretHistories.Infrastructure.DealersTable>().GetDrawPile(deckId).GetElementTokens();
        }

        public static List<Token> GetDeckForbiddenTokens(string deckId)
        {
            return Watchman.Get<SecretHistories.Infrastructure.DealersTable>().GetForbiddenPile(deckId).GetElementTokens();
        }

        public static List<Token> GetSphereTokensByPath(FucinePath spherePath)
        {
            return Watchman.Get<HornedAxe>().GetSphereByPath(spherePath).GetTokens().ToList();
        }

        public static Situation GetLocalSituation()
        {
            return local_situation;
        }

        public static List<Token> GetLocalTokenAsTokens()
        {
            return local_token;
        }

        public static List<Token> GetLocalSphereTokens()
        {
            return local_sphere;
        }

        public static void SetLocalSituation(Situation situation)
        {
            local_situation = situation;
            local_sphere = local_situation.GetElementTokensInSituation();
        }

        public static void SetLocalToken(Token token)
        {
            local_token.Clear();
            local_token.Add(token);
            local_sphere = local_token;
        }

        public static void ResetLocalToken()
        {
            local_sphere = GetLocalSituation().GetElementTokensInSituation();
            local_token.Clear();
        }

        public static List<Token> FilterTokens(this IEnumerable<Token> tokens, Funcine<bool> filter)
        {
            List<Token> result = new List<Token>();
            foreach (Token token in tokens)
            {
                SetLocalToken(token);
                if (filter.result == true)
                    result.Add(token);
            }

            local_sphere = GetLocalSituation().GetElementTokensInSituation();
            local_token.Clear();

            return result;
        }

        public static void TestReference(string[] command)
        {
            string formula = string.Concat(command);
            FuncineRef reference = new FuncineRef(formula, "A");
            Birdsong.Sing("Targeting element '{0}' by filter {2}", reference.targetElementId, reference.tokensFilter.formula);
        }

        public static void TestExpression(params string[] command)
        {
            string formula = string.Concat(command);
            Birdsong.Sing(new Funcine<int>(formula));
        }

        public static void LogAllSpheres(params string[] command)
        {
            foreach (Sphere sphere in Watchman.Get<HornedAxe>().GetSpheres())
                Birdsong.Sing(sphere.GetAbsolutePath());
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
    }
}