﻿using System;
using System.Collections.Generic;

using SecretHistories;
using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Core;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Services;

namespace Roost.Twins.Entities
{
    public struct FucineNumberGetter
    {
        public readonly ValueArea area;
        public readonly ValueOperation operation;
        public readonly string targetId;

        public delegate int SingleTokenValue(Token token, string target);
        public SingleTokenValue GetValue;

        public delegate int HandleTokenValues(List<Token> tokens, SingleTokenValue getTokenValue, string target);
        HandleTokenValues HandleValues;

        public float GetValueFromTokens(List<Token> tokens)
        {
            //NB - not always tokens! sometimes Root or Char data
            //if (tokens == null || tokens.Count == 0)
            //return 0;

            return HandleValues(tokens, GetValue, targetId);
        }

        public bool Equals(FucineNumberGetter otherValueRef)
        {
            return this.area == otherValueRef.area && this.operation == otherValueRef.operation && this.targetId == otherValueRef.targetId;
        }

        public FucineNumberGetter(string target) : this(target, ValueArea.Aspect, ValueOperation.Sum) { }

        public FucineNumberGetter(string target, ValueArea fromArea, ValueOperation withOperation)
        {
            this.targetId = target;
            this.area = fromArea;
            this.operation = withOperation;

            if (!string.IsNullOrEmpty(targetId))
            {
                if (this.area == ValueArea.Aspect
                 || this.area == ValueArea.Mutation
                 || this.area == ValueArea.RecipeAspect
                 || this.operation == ValueOperation.Root)
                    Watchman.Get<Compendium>().SupplyIdForValidation(typeof(Element), this.targetId);

                if (this.area == ValueArea.Recipe)
                    Watchman.Get<Compendium>().SupplyIdForValidation(typeof(Recipe), this.targetId);
            }

            GetValue = AreaOperationsStorage.GetAreaHandler(area);
            HandleValues = ValueOperationsStorage.GetOperationHandler(operation);
        }

        public enum ValueArea
        {
            Aspect, //returns aspect amount from an element token
            Mutation, //returns mutation amount from an element token
            Container, //returns the aspects of a token's sphere's container
            Lifetime, //returns remaining lifetime from an element token, or remaining warmup from a situation token
            Lifespan, //returns max lifetime from an element token, or full warmup from a situation token
            SituationContent, //returns aspect amount from a situation token
            AnySourceAspect, //returns aspect amount from any token
            RecipeAspect, //retuns quantity (likely 1) if the token is a verb running a recipe with the defined aspect
            Entity, //returns a quantity (likely 1) if the token is an entity of a specified id
            Verb, //returns a quantity (likely 1) if the token is a verb of a specified id
            Recipe, //retuns a quantity (likely 1) if the token is a verb running a recipe with a specified id
            Token, Payload, Property, //return token/its payload/payload entity property; incredibly hacky (and probably slow) rn, but work
            NoArea
        };

        public enum ValueOperation
        {
            Sum, //sum of values of all tokens
            Num, //value from a token as if it had quantity 1
            Max, Min, //max/min value among all tokens
            Rand, //single value of a random token
            Root, //value from FucineRoot mutations
            Achievement, //check whether achievement is unlocked
            LeverFuture, //check whether lever is set
            LeverPast, //check whether lever is set
            DeckSpecCount, //value from Deck spec
            Executions, //recipe execution count for the current character - NoArea
            Count, //number of tokens - NoArea and no target
        };

        private static class AreaOperationsStorage
        {
            public static SingleTokenValue GetAreaHandler(ValueArea area)
            {
                switch (area)
                {
                    case ValueArea.Aspect: return ElementAspect;
                    case ValueArea.Mutation: return Mutation;
                    case ValueArea.Container: return Container;
                    case ValueArea.Lifetime: return Lifetime;
                    case ValueArea.Lifespan: return Lifespan;
                    case ValueArea.SituationContent: return AspectInSituation;
                    case ValueArea.AnySourceAspect: return AspectOnAnyToken;
                    case ValueArea.Verb: return VerbId;
                    case ValueArea.Recipe: return RecipeId;
                    case ValueArea.Entity: return EntityId;
                    case ValueArea.RecipeAspect: return RecipeAspect;
                    case ValueArea.Token: return TokenProperty;
                    case ValueArea.Payload: return PayloadProperty;
                    case ValueArea.Property: return EntityProperty;
                    case ValueArea.NoArea: return null;
                    default:
                        Birdsong.TweetLoud($"Value area '{area}' doesn't have a matching method; will always return zero");
                        return Zero;
                }
            }

            private static int ElementAspect(Token token, string target)
            {
                if (!token.IsValidElementStack())
                    return 0;

                target = Elegiast.Scribe.TryReplaceWithLever(target);
                return token.GetAspects(true).AspectValue(target);
            }

            private static int Mutation(Token token, string target)
            {
                if (!token.IsValidElementStack())
                    return 0;

                target = Elegiast.Scribe.TryReplaceWithLever(target);

                return token.GetCurrentMutations().TryGetValue(target, out int value) ? value : 0;
            }

            private static int Container(Token token, string target)
            {
                target = Elegiast.Scribe.TryReplaceWithLever(target);

                return token.Sphere.GetContainer().GetAspects(true).AspectValue(target);
            }

            private static int Lifetime(Token token, string target)
            {
                float value = token.Payload.GetTimeshadow().LifetimeRemaining;

                return ConvertToInt(value * 1000);
            }

            private static int Lifespan(Token token, string target)
            {
                float value;
                if (token.Payload is Situation situation)
                    value = situation.Warmup;
                else if (token.Payload is ElementStack stack)
                    value = stack.Element.Lifetime;
                else
                    return 0;

                return ConvertToInt(value * 1000);
            }

            private static int AspectInSituation(Token token, string target)
            {
                if (!IsSituation(token.Payload))
                    return 0;

                target = Elegiast.Scribe.TryReplaceWithLever(target);

                return  token.GetAspects(true).AspectValue(target);
            }

            private static int AspectOnAnyToken(Token token, string target)
            {
                target = Elegiast.Scribe.TryReplaceWithLever(target);

                return token.GetAspects(true).AspectValue(target);
            }

            private static int VerbId(Token token, string target)
            {
                if (!IsSituation(token.Payload))
                    return 0;

                return EntityId(token, target);
            }

            private static int RecipeId(Token token, string target)
            {
                Situation situation = token.Payload as Situation;

                if (situation == null)
                    return 0;

                if (situation.StateIdentifier == StateEnum.Unstarted)
                    return 0;

                target = Elegiast.Scribe.TryReplaceWithLever(target);
                if (NoonExtensions.WildcardMatchId(situation.CurrentRecipe.Id, target))
                    return token.Quantity;

                return 0;
            }

            private static int EntityId(Token token, string target)
            {
                target = Elegiast.Scribe.TryReplaceWithLever(target);

                return NoonExtensions.WildcardMatchId(token.PayloadEntityId, target) ? token.Quantity : 0;
            }

            private static int RecipeAspect(Token token, string target)
            {
                Situation situation = token.Payload as Situation;

                if (situation == null)
                    return 0;

                if (situation.StateIdentifier == StateEnum.Unstarted)
                    return 0;

                target = Elegiast.Scribe.TryReplaceWithLever(target);

                return (token.Payload as Situation).CurrentRecipe.Aspects.AspectValue(target);
            }

            private static int PayloadProperty(Token token, string target)
            {
                object value = token.Payload.GetType().GetProperty(target).GetValue(token.Payload);

                try
                {
                    return ConvertToInt(value);
                }
                catch (Exception ex)
                {
                    Birdsong.TweetLoud($"Unable to parse property '{target}' of '{token.Payload}': {ex.FormatException()}");
                    return 0;
                }
            }

            private static int TokenProperty(Token token, string target)
            {
                object value = token.Payload.GetType().GetProperty(target).GetValue(token.Payload);

                try
                {
                    return ConvertToInt(value);
                }
                catch (Exception ex)
                {
                    Birdsong.TweetLoud($"Unable to parse property '{target}' of '{token.Payload}': {ex.FormatException()}");
                    return 0;
                }
            }

            private static int EntityProperty(Token token, string target)
            {
                if (token.IsValidElementStack())
                {
                    Element element = Watchman.Get<Compendium>().GetEntityById<Element>(token.PayloadEntityId);

                    object value = typeof(Element).GetProperty(target).GetValue(element);

                    try
                    {
                        return ConvertToInt(value);
                    }
                    catch (Exception ex)
                    {
                        Birdsong.TweetLoud($"Unable to parse property '{target}' of '{element}': {ex.FormatException()}");
                        return 0;
                    }
                }

                if (token.Payload is Situation situation)
                {
                    Recipe recipe = Watchman.Get<Compendium>().GetEntityById<Recipe>(situation.CurrentRecipeId);
                    object value = typeof(Recipe).GetProperty(target).GetValue(recipe);

                    try
                    {
                        return ConvertToInt(value);
                    }
                    catch (Exception ex)
                    {
                        Birdsong.TweetLoud($"Unable to parse property '{target}' of '{recipe}': {ex.FormatException()}");
                        return 0;
                    }
                }

                return 0;
            }

            private static int Zero(Token token, string target)
            {
                return 0;
            }

            private static int ConvertToInt(object value)
            {
                if (value is int intValue)
                    return intValue;

                try
                {
                    return (int)ImportMethods.ConvertValue(value, typeof(int));
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        private static class ValueOperationsStorage
        {
            public static HandleTokenValues GetOperationHandler(ValueOperation operation)
            {
                switch (operation)
                {
                    case ValueOperation.Sum: return Sum;
                    case ValueOperation.Num: return Num;
                    case ValueOperation.Max: return Max;
                    case ValueOperation.Min: return Min;
                    case ValueOperation.Rand: return Rand;
                    case ValueOperation.Root: return Root;
                    case ValueOperation.Achievement: return Achievement;
                    case ValueOperation.LeverFuture: return LeverFuture;
                    case ValueOperation.LeverPast: return LeverPast;
                    //case ValueOperation.DeckSpec: return DeckSpec;
                    case ValueOperation.DeckSpecCount: return DeckSpecCount;
                    case ValueOperation.Executions: return Executions;
                    case ValueOperation.Count: return Count;

                    default:
                        Birdsong.TweetLoud($"Value operation {operation} doesn't have a matching method; will always return zero");
                        return Zero;
                }
            }

            private static int Sum(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int result = 0;
                foreach (Token token in tokens)
                    result += tokenValue(token, target);
                return result;
            }

            public static int Num(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int result = 0;
                foreach (Token token in tokens)
                    result += tokenValue(token, target) / token.Quantity;
                return result;
            }

            private static int Max(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int maxValue = 0; int currentTokenValue;
                foreach (Token token in tokens)
                {
                    currentTokenValue = tokenValue(token, target) / token.Quantity;
                    if (currentTokenValue != 0 && (currentTokenValue > maxValue || (currentTokenValue == maxValue && UnityEngine.Random.Range(0, 99) > 50)))
                        maxValue = currentTokenValue;
                }
                return maxValue;
            }

            private static int Min(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int minValue = int.MaxValue; int currentTokenValue;
                foreach (Token token in tokens)
                    if (token.IsValidElementStack())
                    {
                        currentTokenValue = tokenValue(token, target) / token.Quantity;
                        if (currentTokenValue != 0 && (currentTokenValue < minValue || (currentTokenValue == minValue && UnityEngine.Random.Range(0, 99) > 50)))
                            minValue = currentTokenValue;
                    }
                return minValue == int.MaxValue ? 0 : minValue;
            }

            private static int Rand(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int i = UnityEngine.Random.Range(0, tokens.Count - 1);
                return tokenValue(tokens[i], target);
            }

            private static int Count(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                if (tokens == null || tokens.Count == 0)
                    return 0;

                int result = 0;
                foreach (Token token in tokens)
                    result += token.Quantity;
                return result;
            }

            private static int Root(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                return FucineRoot.Get().Mutations.TryGetValue(target, out int result) ? result : 0;
            }

            private static int Achievement(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                var achievement =
                    Watchman.Get<Compendium>().GetEntityById<Achievement>(target)
                    ?? Watchman.Get<Compendium>().GetEntityById<Achievement>(target.ToUpper());

                if (achievement == null)
                    return 0;

                if (Watchman.Get<AchievementsChronicler>().IsUnlocked(achievement) == false)
                    return 0;

                return 1;
            }

            private static int LeverFuture(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                var lever = Watchman.Get<Stable>().Protag().GetFutureLegacyEventRecord(target);
                if (lever == null)
                    return 0;

                if (int.TryParse(lever, out int value))
                    return value;

                return 1;
            }

            private static int LeverPast(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                var lever = Watchman.Get<Stable>().Protag().GetPastLegacyEventRecord(target);
                if (lever == null)
                    return 0;

                if (int.TryParse(lever, out int value))
                    return value;

                return 1;
            }

            private static int DeckSpec(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                var deck = Watchman.Get<Compendium>().GetEntityById<DeckSpec>(target);
                if (deck == null)
                {
                    Birdsong.TweetLoud($"Trying to access non-existent deck spec '{target}'");
                    return 0;
                }

                AspectsDictionary specAspects = new AspectsDictionary();
                foreach (string elementId in deck.Spec)
                {
                    Element element = Watchman.Get<Compendium>().GetEntityById<Element>(elementId);
                    if (element.IsValid())
                        specAspects.CombineAspects(element.Aspects);
                }

                //how do we pass an element id............
                return specAspects.AspectValue(target);
            }

            private static int DeckSpecCount(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                var deck = Watchman.Get<Compendium>().GetEntityById<DeckSpec>(target);
                if (deck == null)
                {
                    Birdsong.TweetLoud($"Trying to access non-existent deck spec '{target}'");
                    return 0;
                }

                return deck.Spec.Count;
            }

            private static int Executions(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                return Watchman.Get<Stable>().Protag().GetExecutionsCount(target);
            }

            private static int Zero(List<Token> tokens, SingleTokenValue tokenValue, string target)
            {
                return 0;
            }
        }

        private static bool IsSituation(ITokenPayload payload)
        {
            return typeof(Situation).IsAssignableFrom(payload.GetType());
        }
    }

}
