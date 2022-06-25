using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Services;

using UnityEngine;

using Roost.Vagabond.SettingSubscribers;

namespace Roost.Vagabond
{
    internal static class ConfigMask
    {
        private static Dictionary<string, string> configValues;

        public const string minimizePromo = "MinimizePromo";
        public const string storageSpherePlacement = "StorageSpherePlacement";
        internal static void Enact()
        {
            AtTimeOfPower.MenuSceneInit.Schedule(ApplyConfigs, PatchType.Postfix);

            Machine.Patch(typeof(SecretHistory).GetPropertyInvariant(nameof(SecretHistory.Sensitivity)).GetGetMethod(),
                prefix: typeof(ConfigMask).GetMethodInvariant(nameof(ConsoleSensitivity)));
        }

        private static bool ConsoleSensitivity(ref VerbosityLevel __result)
        {
            __result = Birdsong.sensivity;
            return false;
        }

        private static void ApplyConfigs()
        {
            new MinimizePromo(minimizePromo);
            new EnableAchievements(Enactors.Elegiast.enabledSettingId, Enactors.Elegiast.patchId, Roost.Elegiast.CustomAchievementsManager.Enact);
            new StorageSphereDisplay(storageSpherePlacement);
            new ConsoleVerbosity();
        }

        internal static T GetConfigValueSafe<T>(string configId, T valueIfNotDefined) where T : IConvertible
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

            return result.ConvertTo<T>();
        }
    }
}

namespace Roost.Vagabond.SettingSubscribers
{
    internal abstract class ModSettingSubscriber<T> : ISettingSubscriber where T : IConvertible
    {
        protected readonly string settingId;
        protected readonly Setting setting;

        protected T settingValue { get { return setting.CurrentValue.ConvertTo<T>(); } }

        public ModSettingSubscriber(string settingId)
        {
            this.settingId = settingId;
            setting = Watchman.Get<Compendium>().GetEntityById<Setting>(settingId);
            setting.AddSubscriber(this);
        }

        public virtual void WhenSettingUpdated(object newValue) { }

        public void BeforeSettingUpdated(object oldValue) { }
    }

    internal class MinimizePromo : ModSettingSubscriber<int>
    {
        public MinimizePromo(string settingId)
            : base(settingId)
        {
            RectTransform promo = GameObject.Find("MenuBlocksHolder").GetComponent<RectTransform>();
            //it shouldn't be null, but in case it will be, let's not break the entire menu
            if (promo != null)
            {
                promo.pivot = new Vector2(1, 1);
                promo.anchoredPosition = Vector2.zero;
            }
            WhenSettingUpdated(settingValue);
        }

        public override void WhenSettingUpdated(object value)
        {
            GameObject promo = GameObject.Find("MenuBlocksHolder");
            if (promo != null)
            {
                if (settingValue == 1)
                    promo.transform.localScale = Vector2.one * 0.5f;
                else
                    promo.transform.localScale = Vector2.one;
            }
        }
    }

    internal class StorageSphereDisplay : ModSettingSubscriber<int>
    {
        public StorageSphereDisplay(string settingId)
            : base(settingId)
        {
            WhenSettingUpdated(settingValue);
        }

        public override void WhenSettingUpdated(object newValue)
        {
            Roost.World.Recipes.SituationWindowMaster.UpdateDisplaySettingsForSituationWindows((int)newValue);
        }
    }

    internal class ConsoleVerbosity : ISettingSubscriber
    {
        public ConsoleVerbosity()
        {
            Watchman.Get<Compendium>().GetEntityById<Setting>("verbosity").AddSubscriber(this);
        }
        public void WhenSettingUpdated(object newValue)
        {
            Birdsong.SetVerbosityFromConfig((int)newValue);
        }
        public void BeforeSettingUpdated(object oldValue) { }
    }

    internal class PatchSwitcher : ModSettingSubscriber<int>
    {
        protected readonly string modulePatchId;
        protected readonly Action EnactModule;
        public PatchSwitcher(string settingId, string modulePatchId, Action enact)
            : base(settingId)
        {
            this.modulePatchId = modulePatchId;
            this.EnactModule = enact;
        }

        public override void WhenSettingUpdated(object newValue)
        {
            if (settingValue == 1)
            {
                if (Vagabond.HarmonyMask.HasAnyPatches(modulePatchId) == false)
                    EnactModule();
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

namespace Roost
{
    public static partial class Machine
    {
        public static T GetConfigValue<T>(string configId, T valueIfNotDefined = default(T)) where T : IConvertible
        {
            return Vagabond.ConfigMask.GetConfigValueSafe<T>(configId, valueIfNotDefined);
        }
    }
}