using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Globalization;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

using SecretHistories.UI;
using SecretHistories.Services;

using TheRoostManchine.Entities;

namespace TheRoostManchine
{
    internal class AchievementInterfaceManager
    {
        private static void CreateInterface()
        {
            AchievementInterfaceManager interfaceManager = new AchievementInterfaceManager();
            interfaceManager.CreateButton();
            interfaceManager.CreateOverlay();

            interfaceManager.SetAchievements(null);
            Watchman.Get<Concursum>().AfterChangingCulture.AddListener(new UnityAction<CultureChangedArgs>(interfaceManager.SetAchievements));
        }

        private void CreateButton()
        {
            GameObject buttonSample = GameObject.Find("ModsBtn");
            GameObject myButton = GameObject.Instantiate(buttonSample);
            myButton.name = "AchievementsBtn";

            RectTransform myButtonTransform = myButton.GetComponent<RectTransform>();
            myButtonTransform.SetParent(GameObject.Find("CanvasMenu").transform);
            myButtonTransform.SetAsFirstSibling();
            myButtonTransform.anchorMax = Vector2.right;
            myButtonTransform.anchorMin = Vector2.right;
            myButtonTransform.pivot = Vector2.right;
            myButtonTransform.localScale = Vector3.one;
            myButtonTransform.anchoredPosition = (Vector2.left + Vector2.up) * 15;

            myButton.GetComponentInChildren<Babelfish>().SetBabelLabel("ACH_BUTTON");
            myButton.GetComponentInChildren<Image>().sprite = ResourcesManager.GetSpriteForAspect("library");

            Button button = myButton.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(new UnityEngine.Events.UnityAction(this.OpenAchievementsMenu));
            //for OpenAchievementsMenu - since we can't pass params here easily
            this.showoverlay = typeof(MenuScreenController).GetMethod("ShowOverlay", BindingFlags.Instance | BindingFlags.NonPublic);
            this.menu = GameObject.FindObjectOfType<MenuScreenController>();
        }

        Transform achievementsContainer;
        GameObject achievementTemplate;
        GameObject hiddenInfo;
        TextMeshProUGUI categoryText;
        private void CreateOverlay()
        {
            GameObject overlaySample = GameObject.Find("OverlayHolder_KEEP-ACTIVE").FindInChildren("OverlayWindow_Mods");

            GameObject myOverlay = GameObject.Instantiate(overlaySample, overlaySample.transform.parent);
            myOverlay.transform.SetAsLastSibling();

            this.overlay = new object[] { myOverlay.transform.GetComponent<CanvasGroupFader>() };
            myOverlay.name = "OverlayWindow_Achievements";
            myOverlay.SetActive(true); //textmeshpro doesn't update on inactive

            myOverlay.FindInChildren("TitleText").GetComponent<Babelfish>().SetBabelLabel("ACH_BUTTON");
            myOverlay.FindInChildren("TitleText").GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.UpperCase;

            TextMeshProUGUI fancypancy = myOverlay.FindInChildren("HintText", true).GetComponent<TextMeshProUGUI>();
            fancypancy.fontStyle = FontStyles.Italic;
            fancypancy.gameObject.SetActive(false);
            fancypancy.gameObject.AddComponent<Babelfish>();
            fancypancy.GetComponent<Babelfish>().SetBabelLabel("ACH_FANCYPANCY_QUOTE");
            fancypancy.gameObject.SetActive(true);
            myOverlay.FindInChildren("TitleArtwork").GetComponent<Image>().sprite = ResourcesManager.GetSpriteForAspect("library");
            myOverlay.FindInChildren("TitleArtwork").transform.SetAsLastSibling();

            Transform content = myOverlay.FindInChildren("content", true).transform;
            content.GetComponent<VerticalLayoutGroup>().spacing = 3;
            content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 20, 50);

            achievementsContainer = GameObject.Instantiate(content.gameObject, content).transform;
            achievementsContainer.SetAsFirstSibling();
            achievementsContainer.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);

            var category = new GameObject();
            category.name = "AchievementCategory";
            category.transform.SetParent(content);
            category.transform.SetAsFirstSibling();
            LayoutElement categoryLayout = category.AddComponent<LayoutElement>();
            categoryLayout.minHeight = 20;
            categoryLayout.preferredHeight = categoryLayout.minHeight;
            categoryLayout.flexibleHeight = 0;
            categoryLayout.minWidth = 690;
            categoryLayout.flexibleWidth = 0;
            category.GetComponent<RectTransform>().sizeDelta = new Vector2(categoryLayout.minWidth, categoryLayout.minHeight);

            categoryText = GameObject.Instantiate(myOverlay.FindInChildren("TitleText"), category.transform).GetComponent<TextMeshProUGUI>();
            categoryText.name = "AchievementCategoryTitle";
            categoryText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            categoryText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;

            var categoryTransform = categoryText.GetComponent<RectTransform>();
            categoryTransform.pivot = new Vector2(0.5f, 0f);
            categoryTransform.anchorMin = new Vector2(0.5f, 0);
            categoryTransform.anchorMax = new Vector2(0.5f, 0.9f);
            categoryTransform.anchoredPosition = new Vector2(Screen.width / 40, 0);
            categoryTransform.sizeDelta = new Vector2(Mathf.Min(700, Screen.width / 2.7f), Screen.height / (175 - Mathf.Sqrt(Screen.height) * 4));

            var buttonNext = GameObject.Instantiate(myOverlay.FindInChildren("modtitle", true), category.transform);

            buttonNext.name = "NextCategory";
            var buttonText = buttonNext.GetComponent<TextMeshProUGUI>();
            buttonText.color = categoryText.color;
            buttonText.text = ">";
            buttonText.font = categoryText.font;
            buttonText.fontSize = Screen.height / 22;
            buttonText.enableAutoSizing = false;
            buttonText.fontStyle = FontStyles.Bold;
            buttonText.alignment = TextAlignmentOptions.MidlineLeft;
            buttonText.raycastTarget = true;
            var changeButton = buttonNext.AddComponent<Button>();
            changeButton.targetGraphic = buttonText;
            changeButton.onClick.AddListener(new UnityEngine.Events.UnityAction(NextCategory));

            var buttonTransform = buttonNext.GetComponent<RectTransform>();
            buttonTransform.pivot = new Vector2(0f, 0.5f);
            buttonTransform.anchorMin = new Vector2(0.5f, 0.5f);
            buttonTransform.anchorMax = new Vector2(0.5f, 0.5f);
            buttonTransform.sizeDelta = new Vector2(75, 75);
            buttonTransform.anchoredPosition = new Vector2(categoryTransform.sizeDelta.x * 0.5f + categoryTransform.anchoredPosition.x + 10, 0);

            var buttonPrev = GameObject.Instantiate(buttonNext, category.transform);
            buttonPrev.transform.SetAsFirstSibling();
            buttonNext.name = "PrevCategory";
            buttonText = buttonPrev.GetComponent<TextMeshProUGUI>();
            buttonText.text = "<";
            buttonText.alignment = TextAlignmentOptions.MidlineLeft;
            changeButton = buttonPrev.GetComponent<Button>();
            changeButton.targetGraphic = buttonText;
            changeButton.onClick.AddListener(new UnityEngine.Events.UnityAction(PrevCategory));
            buttonText.alignment = TextAlignmentOptions.MidlineRight;
            buttonTransform = buttonPrev.GetComponent<RectTransform>();
            buttonTransform.pivot = new Vector2(1f, 0.5f);
            buttonTransform.anchoredPosition = new Vector2(-categoryTransform.sizeDelta.x * 0.5f + categoryTransform.anchoredPosition.x - 10, 0);

            while (content.childCount > 2)
                GameObject.DestroyImmediate(content.GetChild(2).gameObject);

            achievementTemplate = myOverlay.FindInChildren("AModEntry", true);
            achievementTemplate.name = "AchievementEntry";
            achievementTemplate.transform.SetParent(achievementsContainer);
            achievementTemplate.transform.SetAsFirstSibling();

            while (achievementsContainer.childCount > 1)
                GameObject.DestroyImmediate(achievementsContainer.GetChild(1).gameObject);
            GameObject.DestroyImmediate(achievementTemplate.GetComponent<SecretHistories.Constants.Modding.ModEntry>());
            GameObject.DestroyImmediate(achievementTemplate.FindInChildren("LocationImageContainer", true));
            GameObject.DestroyImmediate(achievementTemplate.FindInChildren("ModControls", true));

            HorizontalLayoutGroup templateLayout = achievementTemplate.GetComponent<HorizontalLayoutGroup>();
            templateLayout.childForceExpandWidth = false;
            templateLayout.childForceExpandHeight = true;
            templateLayout.childControlWidth = false;
            templateLayout.childControlHeight = false;
            templateLayout.childAlignment = TextAnchor.MiddleLeft;

            Image image = achievementTemplate.FindInChildren("previewimage", true).GetComponent<Image>();
            image.transform.SetParent(achievementTemplate.transform);
            image.transform.SetAsFirstSibling();
            GameObject.DestroyImmediate(achievementTemplate.FindInChildren("PreviewImageContainer", true));
            image.preserveAspect = true;
            image.material = new Material((Shader.Find("Custom/UI-Greyout")));
            image.color = Color.white;
            image.rectTransform.sizeDelta = 75 * Vector2.one;
            GameObject.DestroyImmediate(image.GetComponent<LayoutElement>());

            GameObject textContainer = achievementTemplate.FindInChildren("modtext", true);
            textContainer.name = "AchievementText";
            var textContainerLayout = textContainer.GetComponent<VerticalLayoutGroup>();
            textContainerLayout.childAlignment = TextAnchor.MiddleLeft;
            textContainerLayout.childControlWidth = false;
            textContainerLayout.childControlHeight = false;
            textContainerLayout.childForceExpandWidth = false;
            textContainerLayout.childForceExpandHeight = true;
            textContainerLayout.spacing = -7;

            TextMeshProUGUI title = achievementTemplate.FindInChildren("modtitle", true).GetComponent<TextMeshProUGUI>();
            title.name = "title";
            TextMeshProUGUI description = achievementTemplate.FindInChildren("moddescription", true).GetComponent<TextMeshProUGUI>();
            description.name = "description";

            title.gameObject.SetActive(false);
            description.gameObject.SetActive(false);
            //babelfish so font correctly switches on culture change; needs a label though, errs without
            title.gameObject.AddComponent<Babelfish>().SetBabelLabel(string.Empty);
            description.gameObject.AddComponent<Babelfish>().SetBabelLabel(string.Empty);
            title.gameObject.SetActive(true);
            description.gameObject.SetActive(true);

            title.alignment = TextAlignmentOptions.BottomLeft;
            title.fontSizeMax = title.fontSize;
            title.fontSizeMin = 1;
            title.enableAutoSizing = true;

            description.alignment = TextAlignmentOptions.MidlineJustified;
            description.fontStyle = FontStyles.Normal;
            description.color = Color.gray;
            float fontSize = description.fontSize;
            description.fontSizeMax = fontSize;
            description.fontSizeMin = 1;
            description.enableAutoSizing = true;
            var height = image.rectTransform.sizeDelta.y;
            title.rectTransform.sizeDelta = new Vector2(425, height / 2);
            description.rectTransform.sizeDelta = new Vector2(title.rectTransform.sizeDelta.x, height / 2);
            textContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(title.rectTransform.sizeDelta.x, height);

            GameObject dateContainer = GameObject.Instantiate(description.gameObject, achievementTemplate.transform);
            TextMeshProUGUI date = dateContainer.GetComponentInChildren<TextMeshProUGUI>();
            date.name = "date";
            date.alignment = TextAlignmentOptions.Right;
            date.enableWordWrapping = false;
            date.rectTransform.sizeDelta = new Vector2(150, height);
            date.color = Color.gray;
            date.text = string.Empty;
            date.fontSizeMax -= 2;
            date.fontStyle = FontStyles.Normal;

            hiddenInfo = GameObject.Instantiate(achievementTemplate, content);
            hiddenInfo.FindInChildren("previewimage", true).GetComponent<Image>().color = new Color(0, 0, 0, 0);
            hiddenInfo.FindInChildren("title", true).GetComponent<TextMeshProUGUI>().color = new Color(0.25f, 0.25f, 0.25f, 1);
            hiddenInfo.FindInChildren("description", true).GetComponent<TextMeshProUGUI>().color = new Color(0.25f, 0.25f, 0.25f, 1);

            myOverlay.SetActive(false);
        }

        Dictionary<string, List<GameObject>> sortedAchievements = new Dictionary<string, List<GameObject>>();
        Dictionary<string, int> hiddenInCategory = new Dictionary<string, int>();
        List<string> categories = new List<string>();
        static readonly string[] defaultCategories = new string[] { "ACH_CATEGORY_CSVANILLA", "ACH_CATEGORY_DANCER", "ACH_CATEGORY_PRIEST", "ACH_CATEGORY_GHOUL", "ACH_CATEGORY_EXILE", "ACH_CATEGORY_EVERAFTER", "ACH_CATEGORY_MODS" };
        void SetAchievements(CultureChangedArgs dontMindMe)
        {
            achievementTemplate.SetActive(true);

            List<CustomAchievement> customAchievements = Watchman.Get<Compendium>().GetEntitiesAsList<CustomAchievement>();
            List<VanillaAchievement> vanillaAchievements = Watchman.Get<Compendium>().GetEntitiesAsList<VanillaAchievement>();

            categories.Clear();
            sortedAchievements.Clear();
            hiddenInCategory.Clear();

            foreach (string category in defaultCategories)
            {
                categories.Add(category);
                hiddenInCategory[category] = 0;
                sortedAchievements[category] = new List<GameObject>();
            }

            foreach (CustomAchievement customCategory in customAchievements.ToArray().Where(achievement => achievement.isCategory == true))
            {
                customAchievements.Remove(customCategory);

                categories.Add(customCategory.Id);
                hiddenInCategory[customCategory.Id] = 0;
                sortedAchievements[customCategory.Id] = new List<GameObject>();
            }
            foreach (VanillaAchievement vachievement in vanillaAchievements.ToArray().Where(vachievement => vachievement.legit == false))
                vanillaAchievements.Remove(vachievement);

            List<IFucineAchievement> allAchievements = new List<IFucineAchievement>();
            allAchievements.AddRange(vanillaAchievements);
            allAchievements.AddRange(customAchievements);

            string cultureId = Watchman.Get<Config>().GetConfigValue("Culture");
            cultureId = cultureId == "jp" ? "ja" : cultureId; //>:L
            CultureInfo culture = new CultureInfo(cultureId);

            int achievementNum = 1;
            foreach (IFucineAchievement achievement in allAchievements.OrderByDescending(achievement => achievement.unlocked))
                SetAchievementEntry(achievement, ref achievementNum, culture);

            foreach (string category in categories.ToArray())
                if (sortedAchievements[category].Count == 0 && hiddenInCategory[category] == 0)
                    categories.Remove(category);

            achievementTemplate.SetActive(false);
        }

        private void SetAchievementEntry(IFucineAchievement achievement, ref int achievementNum, CultureInfo culture)
        {
            if (sortedAchievements.ContainsKey(achievement.category) == false)
            {
                Twins.Sing("Non-existing category '{0}' for achievement '{1}'", achievement.category, achievement.label);
                return;
            }

            if (achievement.hidden && achievement.unlocked == false)
            {
                hiddenInCategory[achievement.category]++;
                return;
            }

            GameObject entry;
            if (achievementNum >= achievementsContainer.childCount)
                entry = GameObject.Instantiate(achievementTemplate, achievementsContainer);
            else
                entry = achievementsContainer.GetChild(achievementNum).gameObject;

            achievementNum++;
            sortedAchievements[achievement.category].Add(entry);

            entry.FindInChildren("previewimage", true).GetComponent<Image>().sprite = achievement.sprite;
            entry.FindInChildren("title", true).GetComponent<TextMeshProUGUI>().text = achievement.label;
            entry.FindInChildren("description", true).GetComponent<TextMeshProUGUI>().text = achievement.description;

            if (achievement.unlocked)
                entry.FindInChildren("date", true).GetComponent<TextMeshProUGUI>().text = achievement.unlockDate.ToString("d MMM yyyy '@' HH:mm", culture);
            else
            {
                entry.FindInChildren("previewimage", true).GetComponent<Image>().color = Color.black;
                entry.FindInChildren("title", true).GetComponent<TextMeshProUGUI>().color = new Color(0.25f, 0.25f, 0.25f, 1);
                entry.FindInChildren("description", true).GetComponent<TextMeshProUGUI>().color = new Color(0.25f, 0.25f, 0.25f, 1);
            }
        }

        MethodInfo showoverlay;
        MenuScreenController menu;
        object[] overlay;
        private void OpenAchievementsMenu()
        {
            showoverlay.Invoke(menu, overlay);
            currentCategory = 0;
            SetCategory(categories[0]);
        }

        int currentCategory = 0;
        void NextCategory()
        {
            SoundManager.PlaySfx("UIButtonClick");
            currentCategory++;
            if (currentCategory >= categories.Count)
                currentCategory = 0;
            SetCategory(categories[currentCategory]);
        }

        void PrevCategory()
        {
            SoundManager.PlaySfx("UIButtonClick");
            currentCategory--;
            if (currentCategory < 0)
                currentCategory = categories.Count - 1;
            SetCategory(categories[currentCategory]);
        }

        void SetCategory(string category)
        {
            for (var n = 0; n < achievementsContainer.childCount; n++)
                achievementsContainer.GetChild(n).gameObject.SetActive(false);

            foreach (GameObject entry in sortedAchievements[category])
                entry.SetActive(true);

            ILocStringProvider locStringProvider = Watchman.Get<ILocStringProvider>();

            if (defaultCategories.Contains(category))
                categoryText.text = locStringProvider.Get(category);
            else
                categoryText.text = Watchman.Get<Compendium>().GetEntityById<CustomAchievement>(category).label;

            if (hiddenInCategory[category] == 0)
                hiddenInfo.gameObject.SetActive(false);
            else
            {
                hiddenInfo.gameObject.SetActive(true);
                hiddenInfo.FindInChildren("title", true).GetComponent<TextMeshProUGUI>().text = String.Format(locStringProvider.Get("ACH_HIDDEN_TITLE"), hiddenInCategory[category]);
                hiddenInfo.FindInChildren("description", true).GetComponent<TextMeshProUGUI>().text = locStringProvider.Get("ACH_HIDDEN_DESC");
                hiddenInfo.transform.SetAsLastSibling();
            }
        }
    }
}

//this is the list of all the achievements ordered as in Steam global stats at the moment
/*
"A_CULT_LANTERN", "A_ENDING_DESPAIRENDING", "A_ENDING_DEATHOFTHEBODY", "A_MANSUS_WOOD", "A_MANSUS_WHITEDOOR", "A_ENDING_WINTERSACRIFICE",
"A_CULT_FORGE", "A_CULT_GRAIL", "A_CULT_KNOCK", "A_CULT_SECRETHISTORIES", "A_MANSUS_STAGDOOR",  "A_ENDING_WORKVICTORY", "A_ENDING_WORKVICTORYB",
"A_ENDING_ARREST", "A_SUMMON_GENERIC", "A_MANSUS_SPIDERDOOR", "A_ENDING_VISIONSENDING", "A_CULT_MOTH", "A_MANSUS_PEACOCKDOOR", "A_CULT_WINTER",
"A_CULT_HEART", "A_PROMOTED_EXALTED_LANTERN", "A_ENDING_MINORLANTERNVICTORY", "A_CULT_EDGE", "A_PROMOTED_EXALTED_GRAIL",
"A_ENDING_MINORFORGEVICTORY", "A_PROMOTED_EXALTED_FORGE", "A_ENDING_MINORGRAILVICTORY", "A_PROMOTED_EXALTED_MOTH", "A_ENDING_WORKVICTORYMARRIAGE",
"A_PROMOTED_EXALTED_KNOCK", "A_PROMOTED_EXALTED_WINTER", "A_PROMOTED_EXALTED_HEART", "A_ENDING_FOECAUGHTUP", "A_PROMOTED_EXALTED_EDGE",
"A_MANSUS_TRICUSPIDGATE", "A_ENDING_MINORWINTERVICTORY", "A_ENDING_MINORMOTHVICTORY", "A_ENDING_MINORHEARTVICTORY", "A_ENDING_MAJORFORGEVICTORY", 
"A_ENDING_MINORCROWNEDGROWTHVICTORY", "A_ENDING_MINORKNOCKVICTORY", "A_ENDING_MAJORLANTERNVICTORY", "A_ENDING_MAJORGRAILVICTORY", 
"A_ENDING_OBSCURITYVICTORYA", "A_ENDING_MINORMENISCATEVICTORY", "A_ENDING_CATVICTORY", "A_ENDING_MINORLANTERNVICTORY_WITHRISEN", 
"A_ENDING_MINORFORGEVICTORY_WITHRISEN", "A_ENDING_ENIDVICTORY", "A_ENDING_MINORMAREVICTORY", "A_ENDING_RENIRAVICTORY", 
"A_ENDING_OBSCURITYVICTORYA_FOESLAIN", "A_ENDING_MINORGRAILVICTORY_WITHRISEN", "A_ENDING_OBSCURITYVICTORYC_FOESLAIN", 
"A_ENDING_MINORMENISCATEVICTORY_WITHRISEN", "A_ENDING_COLONEL", "A_ENDING_MINORMOTHVICTORY_WITHRISEN", "A_ENDING_LIONSMITH", 
"A_ENDING_VICTORVICTORY", "A_ENDING_MINORHEARTVICTORY_WITHRISEN", "A_ENDING_AUCLAIRVICTORY", "A_ENDING_TRISTANVICTORY", 
"A_ENDING_VALCIANEVICTORY", "A_ENDING_LAIDLAWVICTORY", "A_ENDING_ELRIDGEVICTORY", "A_ENDING_ROSEVICTORY", "A_ENDING_SALIBAVICTORY", 
"A_ENDING_LEOVICTORY", "A_ENDING_VIOLETVICTORY", "A_ENDING_NEVILLEVICTORY", "A_ENDING_CLIFTONVICTORY", "A_ENDING_SLEEVICTORY", 
"A_ENDING_PORTERVICTORY", "A_ENDING_YSABETVICTORY", "A_ENDING_SYLVIAVICTORY", "A_ENDING_CLOVETTEVICTORY", "A_ENDING_DOROTHYVICTORY", 
"A_ENDING_OBSCURITYVICTORYC", "A_ENDING_OBSCURITYVICTORYB_FOESLAIN", "A_ENDING_WOLF", "A_ENDING_VELVET", "A_ENDING_OBSCURITYVICTORYB"
*/
