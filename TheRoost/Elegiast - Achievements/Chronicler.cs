using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;
using SecretHistories.Commands;

namespace Roost.Elegiast
{
    public static class RoostChronicler
    {
        private const string SET_LEVERS_PAST = "leversPast";
        private const string SET_LEVERS_FUTURE = "leversFuture";

        internal static void Enact()
        {
            Machine.ClaimProperty<Recipe, Dictionary<string, string>>(SET_LEVERS_PAST);
            Machine.ClaimProperty<Recipe, Dictionary<string, string>>(SET_LEVERS_FUTURE);

            Machine.Patch(
                original: typeof(Compendium).GetMethodInvariant(nameof(Compendium.SupplyLevers)),
                prefix: typeof(RoostChronicler).GetMethodInvariant(nameof(SkipNativeLevers)));

            AtTimeOfPower.CompendiumLoad.Schedule(ResetRegisteredLevers, PatchType.Prefix);

            AtTimeOfPower.TabletopSceneInit.Schedule(SetCSLevers, PatchType.Prefix);

            AtTimeOfPower.RecipeExecution.Schedule<RecipeCompletionEffectCommand, Situation>(RecipeEffectLevers, PatchType.Postfix);
        }

        private static bool SkipNativeLevers(Character populatedCharacter, ref Dictionary<string, string> ____pastLevers)
        {
            ____pastLevers = typeof(Character).GetFieldInvariant("_previousCharacterHistoryRecords").GetValue(populatedCharacter) as Dictionary<string, string>;
            return false;
        }

        private static void ResetRegisteredLevers()
        {
            Scribe.ResetRegisteredLevers();

            Scribe.AddTextLever("#PREVIOUSCHARACTERNAME#");
            Scribe.AddTextLever("#LAST_BOOK#");
            Scribe.AddTextLever("#LAST_DESIRE#");
            Scribe.AddTextLever("#LAST_TOOL#");
            Scribe.AddTextLever("#LAST_SIGNIFICANTPAINTING#");
            Scribe.AddTextLever("#LAST_CULT#");
            Scribe.AddTextLever("#LAST_HEADQUARTERS#");
            Scribe.AddTextLever("#LAST_PERSONKILLED#");
            Scribe.AddTextLever("#LAST_FOLLOWER#");
        }

        private static void SetCSLevers()
        {
            //already init for this save
            if (Scribe.GetLeverForCurrentPlaythrough("#PREVIOUSCHARACTERNAME#") != "#PREVIOUSCHARACTERNAME#")
                return;

            string lastcharactername = Scribe.GetLeverForCurrentPlaythrough("lastcharactername");
            if (lastcharactername == "J.N. Sinombre")
                lastcharactername = Watchman.Get<ILocStringProvider>().Get("UI_DEFAULTNAME");
            Scribe.SetLeverForCurrentPlaythrough("#PREVIOUSCHARACTERNAME#", lastcharactername);

            SetLabelFromLever("book", "textbooksanskrit");
            SetLabelFromLever("desire", "ascensionsensationa");
            SetLabelFromLever("tool", "toolknockb");
            SetLabelFromLever("significantpainting", "paintingmansus");
            SetLabelFromLever("cult", "cultgrail_1");
            SetLabelFromLever("headquarters", "generichq");
            SetLabelFromLever("personkilled", "neville_a");
            SetLabelFromLever("follower", "rose_b");

            void SetLabelFromLever(string lever, string defaultElementId)
            {
                Element element =
                    Watchman.Get<Compendium>().GetEntityById<Element>(Scribe.GetLeverForCurrentPlaythrough("last" + lever))
                    ?? Watchman.Get<Compendium>().GetEntityById<Element>(defaultElementId);
                Scribe.SetLeverForCurrentPlaythrough("#LAST_" + lever.ToUpper() + "#", element.Label);
            }
        }

        internal static void RecipeEffectLevers(RecipeCompletionEffectCommand __instance, Situation situation)
        {
            AspectsDictionary aspects = situation.GetSingleSphereByCategory(SphereCategory.SituationStorage)?.GetTotalAspects(true);

            Dictionary<string, string> setLeversPast = __instance.Recipe.RetrieveProperty(SET_LEVERS_PAST) as Dictionary<string, string>;
            if (setLeversPast != null)
                foreach (string lever in setLeversPast.Keys)
                    if (lever == "")
                        Scribe.RemoveLeverForCurrentPlaythrough(lever);
                    else
                    {
                        string refinedString = Scribe.RefineString(setLeversPast[lever], aspects);
                        Scribe.SetLeverForCurrentPlaythrough(lever, refinedString);
                    }

            Dictionary<string, string> setLeversFuture = __instance.Recipe.RetrieveProperty(SET_LEVERS_FUTURE) as Dictionary<string, string>;
            if (setLeversFuture != null)
                foreach (string lever in setLeversFuture.Keys)
                    if (lever == "")
                        Scribe.RemoveLeverForNextPlaythrough(lever);
                    else
                    {
                        string refinedString = Scribe.RefineString(setLeversFuture[lever], aspects);
                        Scribe.SetLeverForNextPlaythrough(lever, refinedString);
                    }
        }
    }
}

namespace Roost.World.Entities
{
    [FucineImportable("customlevers")]
    public class LeverData : AbstractEntity<LeverData>
    {
        [FucineDict] public Dictionary<string, string> DefaultValues { get; set; }
        [FucineList] public List<string> TextLevers { get; set; }

        public LeverData(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log) { }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            foreach (KeyValuePair<string, string> leverWithDefaultValue in DefaultValues)
                Elegiast.Scribe.AddLeverDefaultValue(leverWithDefaultValue.Key.ToUpper(), leverWithDefaultValue.Value);

            foreach (string textLever in TextLevers)
                Elegiast.Scribe.AddTextLever(textLever);
        }
    }
}