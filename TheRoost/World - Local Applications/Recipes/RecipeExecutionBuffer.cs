﻿using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Enums;
using SecretHistories.Abstract;
using SecretHistories.Commands;

namespace Roost.World.Recipes
{
    public static class RecipeExecutionBuffer
    {
        private static readonly HashSet<Token> retirements = new HashSet<Token>();
        private static readonly Dictionary<MutationEffect, HashSet<IHasAspects>> mutations = new Dictionary<MutationEffect, HashSet<IHasAspects>>();
        private static readonly Dictionary<ElementStack, string> transformations = new Dictionary<ElementStack, string>();
        private static readonly Dictionary<Sphere, List<SpawnEffect>> spawns = new Dictionary<Sphere, List<SpawnEffect>>();
        private static readonly Dictionary<Token, int> quantityChanges = new Dictionary<Token, int>();
        private static readonly Dictionary<Token, Sphere> movements = new Dictionary<Token, Sphere>();
        private static readonly Dictionary<Situation, List<LinkedRecipeDetails>> inductions = new Dictionary<Situation, List<LinkedRecipeDetails>>();
        private static readonly HashSet<string> deckRenews = new HashSet<string>();

        private static readonly Dictionary<Token, RetirementVFX> vfxs = new Dictionary<Token, RetirementVFX>();

        public static void ApplyAllEffects()
        {
            ApplyRetirements();
            ApplyMutations();
            ApplyQuantityChanges();
            ApplyTransformations();
            ApplyCreations();
            ApplyMovements();
            ApplyDeckRenews();
        }

        public static void ApplyRetirements()
        {
            foreach (Token token in retirements)
            {
                token.Retire(vfxs.ContainsKey(token) ? vfxs[token] : RetirementVFX.None);
                vfxs.Remove(token);
            }
            retirements.Clear();
        }

        public static void ApplyDeckRenews()
        {
            foreach (string deckId in deckRenews)
                Legerdemain.RenewDeck(deckId);

            deckRenews.Clear();
        }

        public static void ApplyMutations()
        {
            foreach (KeyValuePair<MutationEffect, HashSet<IHasAspects>> mutation in mutations)
                foreach (IHasAspects payload in mutation.Value)
                    mutation.Key.Apply(payload);

            mutations.Clear();
        }

        public static void ApplyQuantityChanges()
        {
            foreach (KeyValuePair<Token, int> change in quantityChanges)
            {
                if (change.Key.Defunct)
                    continue;

                change.Key.Payload.ModifyQuantity(change.Value);
            }

            quantityChanges.Clear();
        }

        public static void ApplyTransformations()
        {
            foreach (ElementStack stack in transformations.Keys)
            {
                var toElement = transformations[stack];
                if (string.IsNullOrWhiteSpace(toElement))
                {
                    Token token = stack.GetToken();
                    token.Retire(vfxs.ContainsKey(token) ? vfxs[token] : RetirementVFX.None);
                }
                else
                    stack.ChangeTo(toElement);

                stack.Token.Payload.Unshroud();
            }

            transformations.Clear();
        }

        public static void ApplyCreations()
        {
            foreach (Sphere sphere in spawns.Keys)
            {
                if (sphere.SupportsVFX())
                    foreach (SpawnEffect creation in spawns[sphere])
                        creation.ApplyWithVFX(sphere);
                else
                    foreach (SpawnEffect creation in spawns[sphere])
                        creation.ApplyWithoutVFX(sphere);
            }

            spawns.Clear();
        }

        public static void ApplyMovements()
        {
            foreach (Token token in movements.Keys)
            {
                Sphere sphere = movements[token];

                if (sphere.SupportsVFX())
                    sphere.ProcessEvictedToken(token, new Context(Context.ActionSource.SituationEffect));
                else
                    sphere.AcceptToken(token, new Context(Context.ActionSource.SituationEffect));
            }

            movements.Clear();
        }

        //applied separately
        public static void ApplyRecipeInductions()
        {
            Character protag = Watchman.Get<Stable>().Protag();

            foreach (Situation situation in inductions.Keys)
            {
                AspectsInContext aspectsInContext = Watchman.Get<HornedAxe>().GetAspectsInContext(situation);

                foreach (LinkedRecipeDetails link in inductions[situation])
                {
                    Recipe recipeWhichCanExecuteInContext = link.GetRecipeWhichCanExecuteInContext(aspectsInContext, protag);
                    if (recipeWhichCanExecuteInContext.IsValid())
                        situation.AdditionalRecipeSpawnToken(recipeWhichCanExecuteInContext, link.Expulsion, link.ToPath, link.OutputPath);
                }
            }

            inductions.Clear();
        }

        public static void ApplyVFX()
        {
            foreach (Token token in vfxs.Keys)
                if (token.Sphere.SupportsVFX())
                    token.Remanifest(vfxs[token]);

            vfxs.Clear();
        }

        public static void ScheduleRetirement(Token token, RetirementVFX vfx)
        {
            ScheduleVFX(token, vfx);
            retirements.Add(token);
        }

        public static void ScheduleDecay(Token token, RetirementVFX vfx)
        {
            Element element = Machine.GetEntity<Element>(token.PayloadEntityId);
            ScheduleTransformation(token, element.DecayTo, vfx);
        }

        public static void ScheduleQuantityChange(Token token, int amount, RetirementVFX vfx)
        {
            if (quantityChanges.ContainsKey(token))
                quantityChanges[token] += amount;
            else
                quantityChanges[token] = amount;

            ScheduleVFX(token, vfx);
        }

        public static void ScheduleDeckRenew(string deckId)
        {
            deckRenews.Add(deckId);
        }

        public static void ScheduleUniqueMutation(Token token, string mutate, int level, bool additive, RetirementVFX vfx, string groupId)
        {
            if (!string.IsNullOrWhiteSpace(groupId))
                foreach (MutationEffect mutation in mutations.Keys)
                    if (mutation.uniqueGroupId == groupId)
                        return;

            ScheduleMutation(token.Payload, mutate, level, additive, groupId);
            ScheduleVFX(token, vfx);
        }

        public static void ScheduleMutation(Token token, string mutate, int level, bool additive, RetirementVFX vfx)
        {
            ScheduleMutation(token.Payload, mutate, level, additive);
            ScheduleVFX(token, vfx);
        }

        public static void ScheduleMutation(IHasAspects payload, string mutate, int level, bool additive, string groupId = "")
        {
            TryReplaceWithLever(ref mutate);

            MutationEffect futureMutation = new MutationEffect(mutate, level, additive, groupId);
            if (!mutations.ContainsKey(futureMutation))
                mutations[futureMutation] = new HashSet<IHasAspects>();

            mutations[futureMutation].Add(payload);
        }

        public static void ScheduleMutation(List<Token> tokens, string mutate, int level, bool additive, RetirementVFX vfx)
        {
            TryReplaceWithLever(ref mutate);

            MutationEffect futureMutation = new MutationEffect(mutate, level, additive, "");
            if (!mutations.ContainsKey(futureMutation))
                mutations[futureMutation] = new HashSet<IHasAspects>();

            foreach (Token token in tokens)
            {
                mutations[futureMutation].Add(token.Payload);
                ScheduleVFX(token, vfx);
            }
        }

        public static void ScheduleTransformation(Token token, string transformTo, RetirementVFX vfx)
        {
            TryReplaceWithLever(ref transformTo);

            transformations[token.Payload as ElementStack] = transformTo;
            ScheduleVFX(token, vfx);
        }

        public static void ScheduleSpawn(Sphere sphere, string elementId, int amount, RetirementVFX vfx)
        {
            TryReplaceWithLever(ref elementId);

            if (spawns.ContainsKey(sphere) == false)
                spawns[sphere] = new List<SpawnEffect>();

            for (int i = 0; i < spawns[sphere].Count; i++)
                if (spawns[sphere][i].IsSameEffect(elementId, vfx))
                {
                    spawns[sphere][i] = spawns[sphere][i].IncreaseAmount(amount);
                    return;
                }

            spawns[sphere].Add(new SpawnEffect(elementId, amount, vfx));
        }

        public static void ScheduleMovement(Token token, Sphere toSphere, RetirementVFX vfx)
        {
            movements[token] = toSphere;
            ScheduleVFX(token, vfx);
        }

        public static void ScheduleRecipeInduction(Situation situation, LinkedRecipeDetails link)
        {
            if (inductions.ContainsKey(situation) == false)
                inductions[situation] = new List<LinkedRecipeDetails>();
            inductions[situation].Add(link);
        }

        public static void ScheduleVFX(Token token, RetirementVFX vfx)
        {
            if (vfx == RetirementVFX.None) //default means "no vfx"
                return;

            if (vfx == RetirementVFX.Default) //none means "remove existing vfx"
            {
                vfxs.Remove(token);
                return;
            }

            if (!retirements.Contains(token))
                vfxs[token] = vfx;

        }

        private struct MutationEffect
        {
            string mutate; int level; bool additive; public string uniqueGroupId;
            public MutationEffect(string mutate, int level, bool additive, string uniqueGroupId)
            { this.mutate = mutate; this.level = level; this.additive = additive; this.uniqueGroupId = uniqueGroupId; }

            public void Apply(IHasAspects payload)
            {
                payload.SetMutation(mutate, level, additive);
            }
        }

        private struct SpawnEffect
        {
            string elementId; int quantity; RetirementVFX vfx;
            public SpawnEffect(string element, int quantity, RetirementVFX vfx)
            { this.elementId = element; this.quantity = quantity; this.vfx = vfx; }

            public void ApplyWithoutVFX(Sphere onSphere)
            {
                onSphere.ModifyElementQuantity(elementId, quantity);
            }

            public void ApplyWithVFX(Sphere onSphere)
            {
                Token token = new TokenCreationCommand().WithElementStack(elementId, quantity).Execute(new Context(Context.ActionSource.SituationEffect), onSphere);
                token.transform.position = new UnityEngine.Vector3(0, 2000, 0);

                if (onSphere.IsCategory(SphereCategory.World))
                    onSphere.ProcessEvictedToken(token, Context.Unknown());
                else
                    onSphere.GetItineraryFor(token).WithDuration(0.3f).Depart(token, new Context(Context.ActionSource.SituationEffect));

                token.Remanifest(vfx);
            }

            public bool IsSameEffect(string element, RetirementVFX vfx) { return element == this.elementId && vfx == this.vfx; }
            public SpawnEffect IncreaseAmount(int add)
            {
                SpawnEffect increasedAmount = this;
                increasedAmount.quantity += add;
                return increasedAmount;
            }
        }

        private static bool SupportsVFX(this Sphere sphere)
        {
            return sphere.IsExteriorSphere || sphere.SphereCategory == SphereCategory.Threshold; //thresholds aren't always exteriors but we want vfx nevertheless
        }

        private static void TryReplaceWithLever(ref string value)
        {
            const string lever = "lever_";
            if (value.StartsWith(lever))
            {
                value = value.Substring(lever.Length);
                value = Elegiast.Scribe.GetLeverForCurrentPlaythrough(value);
            }
        }

        internal static void OnTokenCalved(Token __instance, Token __result)
        {
            Token original = __instance;
            Token calved = __result;

            if (retirements.Contains(original))
                retirements.Add(calved);

            foreach (MutationEffect mutation in mutations.Keys)
                if (mutations[mutation].Contains(original.Payload))
                    mutations[mutation].Add(calved.Payload);

            ElementStack originalStack = original.Payload as ElementStack;
            if (transformations.ContainsKey(originalStack))
                transformations.Add(calved.Payload as ElementStack, transformations[originalStack]);

            if (movements.ContainsKey(original))
                movements.Add(calved, movements[original]);

        }
    }
}


