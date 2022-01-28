using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Fucine;
using SecretHistories.Entities;

using UnityEngine;

using TheRoost.Vagabond.SettingSubscribers;

namespace TheRoost.Vagabond
{
    internal static class ConfigMask
    {
        private static Dictionary<string, string> configValues;

        public const string minimizePromo = "MinimizePromo";

        internal static void Enact()
        {
            AtTimeOfPower.MainMenuLoaded.Schedule(ApplyConfigs, PatchType.Postfix);
        }

        private static void ApplyConfigs()
        {
            new MinimizePromo(minimizePromo);
            new EnableAchievements(Enactors.Elegiast.enabledSettingId, Enactors.Elegiast.patchId, TheRoost.Elegiast.CustomAchievementsManager.Enact);
            new PatchSwitcher(Enactors.Twins.enabledSettingId, Enactors.Twins.patchId, TheRoost.Twins.ExpressionEffects.Enact);
        }

        internal static T GetConfigValueSafe<T>(string configId, T valueIfNotDefined)
        {
            if (configValues == null)
                configValues = typeof(Config).GetFieldInvariant("_configValues").GetValue(Watchman.Get<Config>()) as Dictionary<string, string>;

            object result; string configResult;
            if (configValues.TryGetValue(configId, out configResult))
                result = configResult;
            else
            {
                result = valueIfNotDefined;
                Watchman.Get<Config>().PersistConfigValue(configId, result.ToString());
            }

            return (T)TheRoost.Beachcomber.Panimporter.ConvertValue(result, typeof(T));
        }
    }
}

namespace TheRoost.Vagabond.SettingSubscribers
{
    internal abstract class ModSettingSubscriber<T> : ISettingSubscriber
    {
        protected readonly string settingId;
        protected readonly Setting setting;

        protected T settingValue { get { return (T)TheRoost.Beachcomber.Panimporter.ConvertValue(setting.CurrentValue, typeof(T)); } }

        public ModSettingSubscriber(string settingId)
        {
            this.settingId = settingId;
            setting = Watchman.Get<Compendium>().GetEntityById<Setting>(settingId);
            setting.AddSubscriber(this);
        }

        public virtual void WhenSettingUpdated(object newValue) { }
    }

    internal class MinimizePromo : ModSettingSubscriber<int>
    {
        public MinimizePromo(string settingId)
            : base(settingId)
        {
            RectTransform promo = GameObject.Find("PromoBtns").GetComponent<RectTransform>();
            promo.pivot = new Vector2(1, 1);
            promo.anchoredPosition = Vector2.zero;
            WhenSettingUpdated(settingValue);
        }

        public override void WhenSettingUpdated(object value)
        {
            GameObject promo = GameObject.Find("PromoBtns");
            if (promo != null)
            {
                if (settingValue == 1)
                    promo.transform.localScale = Vector2.one * 0.5f;
                else
                    promo.transform.localScale = Vector2.one;
            }
        }
    }

    internal class PatchSwitcher : ModSettingSubscriber<int>
    {
        protected readonly string modulePatchId;
        protected readonly Action moduleEnact;
        public PatchSwitcher(string settingId, string modulePatchId, Action enact)
            : base(settingId)
        {
            this.modulePatchId = modulePatchId;
            this.moduleEnact = enact;
        }

        public override void WhenSettingUpdated(object newValue)
        {
            if (settingValue == 1)
            {
                if (Vagabond.HarmonyMask.HasAnyPatches(modulePatchId) == false)
                    moduleEnact.Invoke();
            }
            else if (Vagabond.HarmonyMask.HasAnyPatches(modulePatchId) == true)
                Vagabond.HarmonyMask.Unpatch(modulePatchId);
        }
    }

    internal class EnableAchievements : PatchSwitcher
    {
        public EnableAchievements(string settingId, string modulePatchId, Action enact) : base(settingId, modulePatchId, enact) { }

        public override void WhenSettingUpdated(object value)
        {
            base.WhenSettingUpdated(value);

            GameObject menu = GameObject.Find("CanvasMenu");
            if (menu != null)
            {
                if (menu.FindInChildren("AchievementsBtn", true) != null)
                    menu.FindInChildren("AchievementsBtn", true).SetActive(settingValue == 1);
                else if (settingValue == 1)
                    Watchman.Get<SecretHistories.Services.StageHand>().MenuScreen();
            }
        }
    }
}

namespace TheRoost
{
    public static partial class Machine
    {
        public static T GetConfigValue<T>(string configId, T valueIfNotDefined = default(T))
        {
            return Vagabond.ConfigMask.GetConfigValueSafe<T>(configId, valueIfNotDefined);
        }
    }
}