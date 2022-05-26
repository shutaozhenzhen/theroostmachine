using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Enums;

namespace Roost.World.Recipes
{
    public static class RecipeExecutionBuffer
    {
        private static readonly Dictionary<Token, RetirementVFX> retirements = new Dictionary<Token, RetirementVFX>();
        private static readonly Dictionary<FutureMutation, List<Token>> mutations = new Dictionary<FutureMutation, List<Token>>();
        private static readonly Dictionary<ElementStack, FutureTransformation> transformations = new Dictionary<ElementStack, FutureTransformation>();
        private static readonly Dictionary<Sphere, List<FutureCreation>> creations = new Dictionary<Sphere, List<FutureCreation>>();
        private static readonly Dictionary<Token, Sphere> movements = new Dictionary<Token, Sphere>();
        private static readonly List<string> deckRenews = new List<string>();

        //private static readonly HashSet<Sphere> dirtySpheres = new HashSet<Sphere>();
        public static readonly Context situationEffectContext = new Context(Context.ActionSource.SituationEffect);

        public static void ApplyAll()
        {
            ApplyRetirements();
            ApplyRenews();
            ApplyMutations();
            ApplyTransformations();
            ApplyCreations();
            ApplyMovements();
        }

        public static void ApplyRetirements()
        {
            foreach (Token token in retirements.Keys)
                token.Retire(retirements[token]);
            retirements.Clear();
        }

        public static void ApplyMutations()
        {
            foreach (FutureMutation mutation in mutations.Keys)
                foreach (Token onToken in mutations[mutation])
                    mutation.Apply(onToken);
            mutations.Clear();
        }

        public static void ApplyTransformations()
        {
            foreach (ElementStack stack in transformations.Keys)
            {
                transformations[stack].Apply(stack);
                //dirtySpheres.Add(stack.Token.Sphere);
            }
            transformations.Clear();
        }

        public static void ApplyCreations()
        {
            Sphere table = Watchman.Get<HornedAxe>().GetDefaultSphere(OccupiesSpaceAs.Intangible);
            if (creations.ContainsKey(table))
            {
                Context context = new Context(Context.ActionSource.SituationEffect);
                foreach (FutureCreation creation in creations[table])
                    creation.ApplyAnimated(table);
                creations.Remove(table);
            }

            foreach (Sphere sphere in creations.Keys)
            {
                foreach (FutureCreation creation in creations[sphere])
                    creation.Apply(sphere);
                //dirtySpheres.Add(sphere);
            }
            creations.Clear();
        }

        public static void ApplyMovements()
        {
            Context context = new Context(Context.ActionSource.SituationEffect);
            foreach (KeyValuePair<Token, Sphere> movement in movements)
                if (movement.Value.Defunct == false)
                {
                    movement.Value.AcceptToken(movement.Key, context);
                    //dirtySpheres.Add(movement.Value);
                }

            movements.Clear();
        }

        public static void ApplyRenews()
        {
            foreach (string deckId in deckRenews)
                Legerdemain.RenewDeck(deckId);
            deckRenews.Clear();
        }
        /*
        public static void StackTokensInDirtySpheres()
        {
            dirtySpheres.Remove(Watchman.Get<HornedAxe>().GetDefaultSphere(OccupiesSpaceAs.Intangible));
            foreach (Sphere sphere in dirtySpheres)
                StackTokens(sphere);
            dirtySpheres.Clear();
        }
        */
        public static void StackTokens(Sphere sphere)
        {
            List<Token> tokens = sphere.Tokens;
            for (int n = 0; n < tokens.Count; n++)
                for (int m = n + 1; m < tokens.Count; m++)
                {
                    if (tokens[n].CanMergeWithToken(tokens[m]))
                    {
                        tokens[n].Payload.ModifyQuantity(tokens[m].Quantity, situationEffectContext);

                        tokens[m].Retire();
                        tokens.Remove(tokens[m]);
                        m--;
                    }
                }
        }

        public static void ScheduleMutation(Token token, string mutate, int level, bool additive, RetirementVFX vfx)
        {
            FutureMutation futureMutation = new FutureMutation(mutate, level, additive, vfx);
            if (mutations.ContainsKey(futureMutation) == false)
                mutations[futureMutation] = new List<Token>();
            mutations[futureMutation].Add(token);
        }

        public static void ScheduleMutation(List<Token> tokens, string mutate, int level, bool additive, RetirementVFX vfx)
        {
            FutureMutation futureMutation = new FutureMutation(mutate, level, additive, vfx);
            if (mutations.ContainsKey(futureMutation) == false)
                mutations[futureMutation] = new List<Token>();
            mutations[futureMutation].AddRange(tokens);
        }

        public static void ScheduleTransformation(Token token, string transformTo, RetirementVFX vfx)
        {
            FutureTransformation futureTransformation = new FutureTransformation(transformTo, vfx);
            transformations[token.Payload as ElementStack] = futureTransformation;
        }

        public static void ScheduleDecay(Token token, RetirementVFX vfx)
        {
            Element element = Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId);
            if (string.IsNullOrEmpty(element.DecayTo))
                ScheduleRetirement(token, vfx);
            else
            {
                FutureTransformation futureTransformation = new FutureTransformation(element.DecayTo, vfx);
                transformations[token.Payload as ElementStack] = futureTransformation;
            }
        }

        public static void ScheduleCreation(Sphere sphere, string element, int amount, RetirementVFX vfx)
        {
            if (creations.ContainsKey(sphere) == false)
                creations[sphere] = new List<FutureCreation>();

            for (int i = 0; i < creations[sphere].Count; i++)
                if (creations[sphere][i].Identical(element, vfx))
                {
                    creations[sphere][i] = creations[sphere][i].IncreaseAmount(amount);
                    return;
                }

            FutureCreation futureCreation = new FutureCreation(element, amount, vfx);
            creations[sphere].Add(futureCreation);
        }

        public static void ScheduleMovement(Token token, Sphere toSphere)
        {
            movements[token] = toSphere;
        }

        public static void ScheduleDeckRenew(string deckId)
        {
            deckRenews.Add(deckId);
        }

        public static void ScheduleRetirement(Token token, RetirementVFX vfx)
        {
            retirements[token] = vfx;
        }

        private struct FutureMutation
        {
            string mutate; int level; bool additive; RetirementVFX vfx;
            public FutureMutation(string mutate, int level, bool additive, RetirementVFX vfx)
            { this.mutate = mutate; this.level = level; this.additive = additive; this.vfx = vfx; }

            public void Apply(Token onToken)
            {
                if (!onToken.IsValidElementStack())
                    return;

                onToken.Payload.SetMutation(mutate, level, additive);
                if (onToken.Sphere.isDefaultSphere())
                    onToken.Remanifest(vfx);
            }
        }

        private struct FutureTransformation
        {
            string toElementId; RetirementVFX vfx;
            public FutureTransformation(string toElementId, RetirementVFX vfx)
            { this.toElementId = toElementId; this.vfx = vfx; }

            public void Apply(ElementStack onStack)
            {
                if (!onStack.IsValidElementStack())
                    return;

                onStack.ChangeTo(toElementId);
                if (onStack.Token.Sphere.isDefaultSphere())
                    onStack.Token.Remanifest(vfx);
            }
        }

        private struct FutureCreation
        {
            string element; int amount; RetirementVFX vfx;
            public FutureCreation(string element, int amount, RetirementVFX vfx)
            { this.element = element; this.amount = amount; this.vfx = vfx; }

            public void Apply(Sphere onSphere)
            {
                Token token = onSphere.ProvisionElementToken(element, amount);
                if (onSphere.isDefaultSphere() == false)
                    token.Shroud();
            }

            public void ApplyAnimated(Sphere onSphere)
            {
                Token token = onSphere.ProvisionElementToken(element, amount);
                token.Payload.GetEnRouteSphere().ProcessEvictedToken(token, situationEffectContext);
                token.Remanifest(vfx);
            }

            public bool Identical(string element, RetirementVFX vfx) { return (element == this.element && vfx == this.vfx); }
            public FutureCreation IncreaseAmount(int add) { return new FutureCreation(this.element, this.amount + add, this.vfx); }
        }

        public static bool isDefaultSphere(this Sphere sphere)
        {
            return sphere.GetAbsolutePath() == Watchman.Get<HornedAxe>().GetDefaultSpherePath();
        }
    }
}


