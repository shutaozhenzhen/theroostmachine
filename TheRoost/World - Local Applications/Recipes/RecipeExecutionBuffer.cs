using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Enums;
using SecretHistories.Abstract;
using SecretHistories.Commands;
using Roost.Beauty;
using Roost.World.Beauty;

namespace Roost.World.Recipes
{
    public static class RecipeExecutionBuffer
    {
        private static readonly Dictionary<MutationEffect, HashSet<IHasAspects>> mutations = new Dictionary<MutationEffect, HashSet<IHasAspects>>();
        private static readonly Dictionary<Token, string> transformations = new Dictionary<Token, string>();
        private static readonly Dictionary<Sphere, List<SpawnEffect>> spawns = new Dictionary<Sphere, List<SpawnEffect>>();
        private static readonly Dictionary<Token, int> quantityChanges = new Dictionary<Token, int>();
        private static readonly Dictionary<Token, Sphere> movements = new Dictionary<Token, Sphere>();
        private static readonly Dictionary<Situation, List<LinkedRecipeDetails>> inductions = new Dictionary<Situation, List<LinkedRecipeDetails>>();
        private static readonly HashSet<string> deckRenews = new HashSet<string>();

        private static readonly Dictionary<Token, RetirementVFX> vfxs = new Dictionary<Token, RetirementVFX>();
        private static readonly HashSet<Token> overlayUpdates = new HashSet<Token>();

        public static void ApplyGameEffects()
        {
            ApplyMutations();
            ApplyQuantityChanges();
            ApplyTransformations();
            ApplyCreations();
            ApplyMovements();
        }

        public static void ApplyDecorativeEffects()
        {
            ApplyVFX();
            ApplyOverlayUpdates();
        }

        ///////////////////////////////////////////////
        #region GAMEPLAY EFFECTS
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
            foreach (Token token in transformations.Keys)
            {
                var toElement = transformations[token];
                if (string.IsNullOrWhiteSpace(toElement))
                {
                    token.Retire(vfxs.ContainsKey(token) ? vfxs[token] : RetirementVFX.None);
                    vfxs.Remove(token);
                }
                else
                {
                    ElementStack stack = token.Payload as ElementStack;
                    stack.ChangeTo(toElement);
                    token.Payload.Unshroud();
                }
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
            Token draggedToken = Watchman.Get<Meniscate>().GetCurrentlyDraggedToken();
            if (movements.ContainsKey(draggedToken))
                draggedToken.ForceEndDrag();

            Xamanek xamanek = Watchman.Get<Xamanek>();
            foreach (Token token in movements.Keys)
            {
                xamanek.TokenItineraryCompleted(token);
                token.Stabilise();

                movements[token].AcceptWithVFX(token, new Context(Context.ActionSource.SituationEffect));
            }

            movements.Clear();
        }
        #endregion

        ///////////////////////////////////////////////
        #region DECORATIVE EFFECTS
        public static void ApplyVFX()
        {
            foreach (Token token in vfxs.Keys)
                if (token.Sphere.SupportsVFX())
                    token.Remanifest(vfxs[token]);

            vfxs.Clear();
        }

        public static void ApplyOverlayUpdates()
        {
            foreach (Token token in overlayUpdates)
                if (token?.IsValid() == true)
                    OverlaysMaster.ApplyOverlaysToManifestation(token, null);

            overlayUpdates.Clear();
        }
        #endregion

        ///////////////////////////////////////////////
        #region EXOTIC EFFECTS
        public static void ApplyDeckRenews()
        {
            foreach (string deckId in deckRenews)
                Legerdemain.RenewDeck(deckId);

            deckRenews.Clear();
        }

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
        #endregion


        public static void ScheduleMutation(Token token, string mutate, int level, bool additive, RetirementVFX vfx)
        {
            ScheduleMutation(token.Payload, mutate, level, additive);
            ScheduleVFX(token, vfx);
        }

        public static void ScheduleMutation(IHasAspects payload, string mutate, int level, bool additive, string groupId = "")
        {
            mutate = Elegiast.Scribe.TryReplaceWithLever(mutate);

            MutationEffect futureMutation = new MutationEffect(mutate, level, additive, groupId);
            if (!mutations.ContainsKey(futureMutation))
                mutations[futureMutation] = new HashSet<IHasAspects>();

            mutations[futureMutation].Add(payload);
        }

        public static void ScheduleMutation(List<Token> tokens, string mutate, int level, bool additive, RetirementVFX vfx)
        {
            mutate = Elegiast.Scribe.TryReplaceWithLever(mutate);

            MutationEffect futureMutation = new MutationEffect(mutate, level, additive, "");
            if (!mutations.ContainsKey(futureMutation))
                mutations[futureMutation] = new HashSet<IHasAspects>();

            foreach (Token token in tokens)
            {
                mutations[futureMutation].Add(token.Payload);
                ScheduleVFX(token, vfx);
            }
        }

        public static void ScheduleQuantityChange(Token token, int amount, RetirementVFX vfx)
        {
            if (quantityChanges.ContainsKey(token))
                quantityChanges[token] += amount;
            else
                quantityChanges[token] = amount;

            ScheduleVFX(token, vfx);
        }

        public static void ScheduleDecay(Token token, RetirementVFX vfx)
        {
            Element element = Machine.GetEntity<Element>(token.PayloadEntityId);
            ScheduleTransformation(token, element.DecayTo, vfx);
        }

        public static void ScheduleTransformation(Token token, string transformTo, RetirementVFX vfx)
        {
            transformTo = Elegiast.Scribe.TryReplaceWithLever(transformTo);

            if (!token.Payload.IsValidElementStack() && string.IsNullOrWhiteSpace(transformTo) == false)
            {
                Birdsong.TweetLoud($"Trying to apply non-destructive transformation on {token.Payload}, but it's not an element stack and doesn't know what to do with them");
                return;
            }

            transformations[token] = transformTo;
            ScheduleVFX(token, vfx);
        }

        public static void ScheduleCreations(Sphere sphere, string elementId, int amount, RetirementVFX vfx)
        {
            elementId = Elegiast.Scribe.TryReplaceWithLever(elementId);

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

        public static void ScheduleVFX(Token token, RetirementVFX vfx)
        {
            //default means "use whatever is already scheduled"
            if (vfx == RetirementVFX.Default)
                return;

            vfxs[token] = vfx;
        }

        public static void ScheduleOverlay(Token token)
        {
            overlayUpdates.Add(token);
        }


        public static void ScheduleDeckRenew(string deckId)
        {
            deckRenews.Add(deckId);
        }

        public static void ScheduleRecipeInduction(Situation situation, LinkedRecipeDetails link)
        {
            if (inductions.ContainsKey(situation) == false)
                inductions[situation] = new List<LinkedRecipeDetails>();
            inductions[situation].Add(link);
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
                Context context = new Context(Context.ActionSource.SituationEffect);

                Token token = new TokenCreationCommand().WithElementStack(elementId, quantity).Execute(context, Watchman.Get<Limbo>());
                onSphere.AcceptWithVFX(token, context);

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

        internal static void OnTokenCalved(Token __instance, Token __result)
        {
            Token original = __instance;
            Token calved = __result;

            foreach (MutationEffect mutation in mutations.Keys)
                if (mutations[mutation].Contains(original.Payload))
                    mutations[mutation].Add(calved.Payload);

            if (transformations.ContainsKey(original))
                transformations.Add(calved, transformations[original]);

            if (movements.ContainsKey(original))
                movements.Add(calved, movements[original]);

        }
    }
}


