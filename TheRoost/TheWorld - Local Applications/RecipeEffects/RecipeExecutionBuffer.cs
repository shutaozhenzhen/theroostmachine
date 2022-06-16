﻿using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using UnityEngine;

namespace Roost.World.Recipes
{
    public static class RecipeExecutionBuffer
    {
        private static readonly HashSet<Token> retirements = new HashSet<Token>();
        private static readonly Dictionary<ScheduledMutation, List<Token>> mutations = new Dictionary<ScheduledMutation, List<Token>>();
        private static readonly Dictionary<ElementStack, string> transformations = new Dictionary<ElementStack, string>();
        private static readonly Dictionary<Sphere, List<ScheduledCreation>> creations = new Dictionary<Sphere, List<ScheduledCreation>>();
        //private static readonly Dictionary<Token, int> quantityChanges = new Dictionary<Token, int>();
        private static readonly Dictionary<Token, Sphere> movements = new Dictionary<Token, Sphere>();
        private static readonly Dictionary<Situation, List<LinkedRecipeDetails>> inductions = new Dictionary<Situation, List<LinkedRecipeDetails>>();
        private static readonly HashSet<string> deckRenews = new HashSet<string>();

        private static readonly Dictionary<Token, RetirementVFX> vfxs = new Dictionary<Token, RetirementVFX>();

        private static readonly HashSet<Sphere> dirtySpheres = new HashSet<Sphere>();
        public static readonly Context situationEffectContext = new Context(Context.ActionSource.SituationEffect);

        public static void ApplyAllEffects()
        {
            ApplyRetirements();
            //ApplyQuantityChanges();
            ApplyRenews();
            ApplyMutations();
            ApplyTransformations();
            ApplyCreations();
            ApplyMovements();
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

        /*
        public static void ApplyQuantityChanges()
        {
            foreach (Token token in quantityChanges.Keys)
            {
                if (token.Quantity + quantityChanges[token] <= 0)
                    token.Retire(vfxs.ContainsKey(token) ? vfxs[token] : RetirementVFX.None);
                else
                    token.Payload.SetQuantity(token.Quantity + quantityChanges[token], situationEffectContext);

                token.Sphere.MarkAsDirty();
            }
            quantityChanges.Clear();
        }
        */
        public static void ApplyRenews()
        {
            foreach (string deckId in deckRenews)
            {
                Sphere drawSphere = Legerdemain.RenewDeck(deckId);
                drawSphere.MarkAsDirty();
            }

            deckRenews.Clear();
        }

        public static void ApplyMutations()
        {
            foreach (ScheduledMutation mutation in mutations.Keys)
                foreach (Token token in mutations[mutation])
                {
                    mutation.Apply(token);
                    token.Sphere.MarkAsDirty();
                }
            mutations.Clear();
        }

        public static void ApplyTransformations()
        {
            foreach (ElementStack stack in transformations.Keys)
            {
                stack.ChangeTo(transformations[stack]);
                stack.Token.Unshroud();
                stack.Token.Sphere.MarkAsDirty();
            }
            transformations.Clear();
        }

        public static void ApplyCreations()
        {
            foreach (Sphere sphere in creations.Keys)
            {
                if (sphere.SupportsVFX())
                    foreach (ScheduledCreation creation in creations[sphere])
                        creation.ApplyWithVFX(sphere);
                else
                    foreach (ScheduledCreation creation in creations[sphere])
                        creation.ApplyWithoutVFX(sphere);
                sphere.MarkAsDirty();
            }
            creations.Clear();
        }

        public static void ApplyMovements()
        {
            foreach (Token token in movements.Keys)
            {
                Sphere sphere = movements[token];
                if (sphere.SupportsVFX())
                    sphere.GetItineraryFor(token).WithDuration(0.3f).Depart(token, situationEffectContext);
                else
                    sphere.AcceptToken(token, situationEffectContext);

                sphere.MarkAsDirty();
            }
            movements.Clear();
        }

        public static void ApplyInductions()
        {
            foreach (Situation situation in inductions.Keys)
            {
                AspectsInContext aspectsInContext = Watchman.Get<HornedAxe>().GetAspectsInContext(situation.GetAspects(true), null);
                foreach (LinkedRecipeDetails link in inductions[situation])
                    RecipeLinkMaster.TrySpawnSituation(situation, link, aspectsInContext);
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
            if (string.IsNullOrEmpty(element.DecayTo))
                ScheduleRetirement(token, vfx);
            else
                ScheduleTransformation(token, element.DecayTo, vfx);
        }
        /*
        public static void ScheduleQuantityChange(Token token, int amount, RetirementVFX vfx)
        {
            if (quantityChanges.ContainsKey(token))
                quantityChanges[token] += amount;
            else
                quantityChanges.Add(token, amount);

            ScheduleVFX(token, vfx);
        }
        */
        public static void ScheduleDeckRenew(string deckId)
        {
            deckRenews.Add(deckId);
        }

        public static void ScheduleMutation(Token token, string mutate, int level, bool additive, RetirementVFX vfx)
        {
            ScheduledMutation futureMutation = new ScheduledMutation(mutate, level, additive);
            if (mutations.ContainsKey(futureMutation) == false)
                mutations[futureMutation] = new List<Token>();
            mutations[futureMutation].Add(token);

            ScheduleVFX(token, vfx);
        }

        public static void ScheduleMutation(List<Token> tokens, string mutate, int level, bool additive, RetirementVFX vfx)
        {
            ScheduledMutation futureMutation = new ScheduledMutation(mutate, level, additive);
            if (mutations.ContainsKey(futureMutation) == false)
                mutations[futureMutation] = new List<Token>();
            mutations[futureMutation].AddRange(tokens);

            foreach (Token token in tokens)
                ScheduleVFX(token, vfx);
        }

        public static void ScheduleTransformation(Token token, string transformTo, RetirementVFX vfx)
        {
            transformations[token.Payload as ElementStack] = transformTo;
            ScheduleVFX(token, vfx);
        }

        public static void ScheduleCreation(Sphere sphere, string elementId, int amount, RetirementVFX vfx, bool shrouded)
        {
            if (creations.ContainsKey(sphere) == false)
                creations[sphere] = new List<ScheduledCreation>();

            Element element = Machine.GetEntity<Element>(elementId);
            if (element.HasCustomProperty(Elements.ElementEffectsMaster.SHROUDED))
                shrouded = element.RetrieveProperty<bool>(Elements.ElementEffectsMaster.SHROUDED);

            for (int i = 0; i < creations[sphere].Count; i++)
                if (creations[sphere][i].IsSameEffect(elementId, vfx, shrouded))
                {
                    creations[sphere][i] = creations[sphere][i].IncreaseAmount(amount);
                    return;
                }

            creations[sphere].Add(new ScheduledCreation(elementId, amount, vfx, shrouded));
        }

        public static void ScheduleMovement(Token token, Sphere toSphere, RetirementVFX vfx)
        {
            movements[token] = toSphere;
            ScheduleVFX(token, vfx);
        }

        public static void ScheduleInduction(Situation situation, LinkedRecipeDetails link)
        {
            if (inductions.ContainsKey(situation) == false)
                inductions[situation] = new List<LinkedRecipeDetails>();
            inductions[situation].Add(link);
        }

        public static void ScheduleVFX(Token token, RetirementVFX vfx)
        {
            if (vfx != RetirementVFX.None && vfx != RetirementVFX.Default && retirements.Contains(token) == false)
                vfxs[token] = vfx;
        }

        public static void MarkAsDirty(this Sphere sphere)
        {
            dirtySpheres.Add(sphere);
        }

        public static HashSet<Sphere> FlushDirtySpheres()
        {
            HashSet<Sphere> result = new HashSet<Sphere>(dirtySpheres);
            dirtySpheres.Clear();
            return result;
        }

        private struct ScheduledMutation
        {
            string mutate; int level; bool additive;
            public ScheduledMutation(string mutate, int level, bool additive)
            { this.mutate = mutate; this.level = level; this.additive = additive; }

            public void Apply(Token onToken)
            {
                onToken.Payload.SetMutation(mutate, level, additive);
            }
        }

        private struct ScheduledCreation
        {
            string elementId; int amount; RetirementVFX vfx; bool shrouded;
            public ScheduledCreation(string element, int amount, RetirementVFX vfx, bool shrouded)
            { this.elementId = element; this.amount = amount; this.vfx = vfx; this.shrouded = shrouded; }

            public void ApplyWithoutVFX(Sphere onSphere)
            {
                Token token = onSphere.ProvisionElementToken(elementId, amount);
                if (shrouded)
                    token.Shroud(true);
            }

            public void ApplyWithVFX(Sphere onSphere)
            {
                Token token = Watchman.Get<Limbo>().ProvisionElementToken(elementId, amount);
                onSphere.GetItineraryFor(token).WithDuration(0.3f).Depart(token, RecipeExecutionBuffer.situationEffectContext);
                token.Remanifest(vfx);
            }

            public bool IsSameEffect(string element, RetirementVFX vfx, bool shrouded) { return (element == this.elementId && vfx == this.vfx && this.shrouded == shrouded); }
            public ScheduledCreation IncreaseAmount(int add)
            {
                ScheduledCreation increasedAmount = this;
                increasedAmount.amount += add;
                return increasedAmount;
            }
        }

        private static bool SupportsVFX(this Sphere sphere)
        {
            return sphere.SphereCategory == SphereCategory.World || sphere.SphereCategory == SphereCategory.Threshold;
        }
    }
}


