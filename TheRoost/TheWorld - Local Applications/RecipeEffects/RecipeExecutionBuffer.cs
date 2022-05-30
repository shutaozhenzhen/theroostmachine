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
        private static readonly Dictionary<ScheduledMutation, List<Token>> mutations = new Dictionary<ScheduledMutation, List<Token>>();
        private static readonly Dictionary<ElementStack, ScheduledTransformation> transformations = new Dictionary<ElementStack, ScheduledTransformation>();
        private static readonly Dictionary<Sphere, List<ScheduledCreation>> creations = new Dictionary<Sphere, List<ScheduledCreation>>();
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
            foreach (ScheduledMutation mutation in mutations.Keys)
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
            foreach (Sphere sphere in creations.Keys)
            {
                if (sphere.SupportsVFX())
                    foreach (ScheduledCreation creation in creations[sphere])
                        creation.ApplyWithVFX(sphere);
                else
                    foreach (ScheduledCreation creation in creations[sphere])
                        creation.ApplyWithoutVFX(sphere);
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

        public static void ScheduleMutation(Token token, string mutate, int level, bool additive, RetirementVFX vfx)
        {
            ScheduledMutation futureMutation = new ScheduledMutation(mutate, level, additive, vfx);
            if (mutations.ContainsKey(futureMutation) == false)
                mutations[futureMutation] = new List<Token>();
            mutations[futureMutation].Add(token);
        }

        public static void ScheduleMutation(List<Token> tokens, string mutate, int level, bool additive, RetirementVFX vfx)
        {
            ScheduledMutation futureMutation = new ScheduledMutation(mutate, level, additive, vfx);
            if (mutations.ContainsKey(futureMutation) == false)
                mutations[futureMutation] = new List<Token>();
            mutations[futureMutation].AddRange(tokens);
        }

        public static void ScheduleTransformation(Token token, string transformTo, RetirementVFX vfx)
        {
            ScheduledTransformation futureTransformation = new ScheduledTransformation(transformTo, vfx);
            transformations[token.Payload as ElementStack] = futureTransformation;
        }

        public static void ScheduleDecay(Token token, RetirementVFX vfx)
        {
            Element element = Machine.GetEntity<Element>(token.PayloadEntityId);
            if (string.IsNullOrEmpty(element.DecayTo))
                ScheduleRetirement(token, vfx);
            else
            {
                ScheduledTransformation futureTransformation = new ScheduledTransformation(element.DecayTo, vfx);
                transformations[token.Payload as ElementStack] = futureTransformation;
            }
        }

        public static void ScheduleCreation(Sphere sphere, string element, int amount, RetirementVFX vfx)
        {
            if (creations.ContainsKey(sphere) == false)
                creations[sphere] = new List<ScheduledCreation>();

            for (int i = 0; i < creations[sphere].Count; i++)
                if (creations[sphere][i].IsSameElementWithSameVFX(element, vfx))
                {
                    creations[sphere][i] = creations[sphere][i].IncreaseAmount(amount);
                    return;
                }

            ScheduledCreation futureCreation = new ScheduledCreation(element, amount, vfx);
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

        private struct ScheduledMutation
        {
            string mutate; int level; bool additive; RetirementVFX vfx;
            public ScheduledMutation(string mutate, int level, bool additive, RetirementVFX vfx)
            { this.mutate = mutate; this.level = level; this.additive = additive; this.vfx = vfx; }

            public void Apply(Token onToken)
            {
                onToken.Payload.SetMutation(mutate, level, additive);
                if (onToken.Sphere.SupportsVFX())
                    onToken.Remanifest(vfx);
            }
        }

        private struct ScheduledTransformation
        {
            string toElementId; RetirementVFX vfx;
            public ScheduledTransformation(string toElementId, RetirementVFX vfx)
            { this.toElementId = toElementId; this.vfx = vfx; }

            public void Apply(ElementStack onStack)
            {
                onStack.ChangeTo(toElementId);
                onStack.Token.Unshroud();
                if (onStack.Token.Sphere.SupportsVFX())
                    onStack.Token.Remanifest(vfx);
            }
        }

        private struct ScheduledCreation
        {
            string element; int amount; RetirementVFX vfx;
            public ScheduledCreation(string element, int amount, RetirementVFX vfx)
            { this.element = element; this.amount = amount; this.vfx = vfx; }

            public void ApplyWithoutVFX(Sphere onSphere)
            {
                Token token = onSphere.ProvisionElementToken(element, amount);
                token.Shroud();
            }

            public void ApplyWithVFX(Sphere onSphere)
            {
                Token token = onSphere.ProvisionElementToken(element, amount);
                token.Payload.GetEnRouteSphere().ProcessEvictedToken(token, situationEffectContext);
                token.Remanifest(vfx);
            }

            public bool IsSameElementWithSameVFX(string element, RetirementVFX vfx) { return (element == this.element && vfx == this.vfx); }
            public ScheduledCreation IncreaseAmount(int add) { return new ScheduledCreation(this.element, this.amount + add, this.vfx); }
        }

        private static bool SupportsVFX(this Sphere sphere)
        {
            return sphere.SphereCategory == SphereCategory.World;
        }

        //sensibly speaking this method shouldn't be here, but it requires Context, which Buffer has....
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
    }
}


