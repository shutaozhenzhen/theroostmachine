using System;
using SecretHistories.Fucine;
using UnityEngine;

namespace TheRoost.Elegiast.Entities
{
    public interface IFucineAchievement
    {
        string label { get; }
        string description { get; }
        string category { get; }
        Sprite sprite { get; }
        bool hidden { get; }
        bool unlocked { get; }
        DateTime unlockDate { get; }
    }
    [FucineImportable("achievements")]
    public class CustomAchievement : AbstractEntity<CustomAchievement>, IFucineAchievement
    {
        [FucineValue(false)]
        public bool isCategory { get; set; }

        [FucineValue(DefaultValue = "", Localise = true)]
        public string label { get; set; }
        [FucineValue(DefaultValue = "ACH_CATEGORY_MODS")]
        public string category { get; set; }

        [FucineValue(DefaultValue = "", Localise = true)]
        public string lockdesc { get; set; }
        [FucineValue(DefaultValue = "", Localise = true)]
        public string unlockdesc { get; set; }

        [FucineValue("")]
        public string iconLocked { get; set; }
        [FucineValue("_x")]
        public string iconUnlocked { get; set; }

        [FucineValue(DefaultValue = "", Localise = true)]
        public string unlockMessage { get; set; }

        [FucineValue(false)]
        public bool hidden { get; set; }

        public string description { get { if (unlocked) return unlockdesc; else return lockdesc; } }
        public Sprite sprite
        {
            get { if (unlocked) return ResourcesManager.GetSpriteForElement(iconUnlocked); else return ResourcesManager.GetSpriteForElement(iconLocked); }
        }
        public DateTime unlockDate { get { return CustomAchievementsManager.GetUnlockTime(this.Id); } }
        public bool unlocked { get { return CustomAchievementsManager.isUnlocked(this); } }

        public CustomAchievement() { }
        public CustomAchievement(SecretHistories.Fucine.DataImport.EntityData importDataForEntity, ContentImportLog log)
            : base(importDataForEntity, log)
        {
            if (String.IsNullOrEmpty(iconLocked))
                iconLocked = iconUnlocked;
            if (isCategory)
                category = "";
        }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }
    }

    [FucineImportable("vachievements")]
    public class VanillaAchievement : AbstractEntity<VanillaAchievement>, IFucineAchievement
    {
        [FucineValue(DefaultValue = "", Localise = true)]
        public string label { get; set; }
        [FucineValue(DefaultValue = "ACH_CATEGORY_CSVANILLA")]
        public string category { get; set; }
        [FucineValue(DefaultValue = "", Localise = true)]
        public string description { get; set; }
        [FucineValue("_x")]
        public string icon { get; set; }

        public bool hidden { get { return _hidden; } } readonly bool _hidden;
        public Sprite sprite { get { return ResourcesManager.GetSpriteForElement(icon); } }
        public DateTime unlockDate { get { return DateTimeOffset.FromUnixTimeSeconds(unlockTime).DateTime; } } readonly uint unlockTime;
        public bool unlocked { get { return _unlocked; } } readonly bool _unlocked;
        public readonly bool legit;

        public VanillaAchievement(SecretHistories.Fucine.DataImport.EntityData importDataForEntity, ContentImportLog log)
            : base(importDataForEntity, log)
        {
            CustomAchievementsManager.CheckVanillaAchievement(this,  out _unlocked, out legit, out _hidden, out unlockTime);
        }
        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium) { }
    }
}
