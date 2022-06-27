using SecretHistories.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Roost.World.Beauty
{ 
    class CanvasRendererFading : MonoBehaviour
    {
        public CanvasRenderer fadingCR = null;
        float update = 0f;
        float currentAlpha = 1f;
        void Update()
        {
            update += Time.deltaTime;
            if (update > 0.05f)
            {
                currentAlpha = 0.95f * currentAlpha;
                fadingCR.SetColor(new Color(1, 1, 1, currentAlpha));
                update = 0f;

                if (currentAlpha <= 0.05f) Destroy(gameObject);
            }
        }
    }

    class TableStyleMaster
    {
        static GameObject tabletop = null;
        static GameObject leather = null;
        static GameObject legs = null;
        static Image tabletopImageComponent = null;
        static Image tableLeatherImageComponent = null;
        static Image tableLegsImageComponent = null;

        internal static void Enact()
        {
            Machine.ClaimProperty<Recipe, string>("tabletopImage");
            Machine.ClaimProperty<Recipe, string>("tableLeather");
            Machine.ClaimProperty<Recipe, string>("tableLegs");
            AtTimeOfPower.TabletopSceneInit.Schedule(fetchReferencesAndInit, PatchType.Postfix);
            AtTimeOfPower.RecipeExecution.Schedule<Situation>(checkForTableChangeOnRecipeCompletion, PatchType.Postfix);

            /*Machine.Patch(
                    original: typeof(SituationWindow).GetMethodInvariant(nameof(SituationWindow.Attach), typeof(Situation)),
                    postfix: typeof(TableManager).GetMethodInvariant(nameof(setBg)));
            */
        }
        /*
        static void setBg(SituationWindow __instance, Situation newSituation)
        {
            GameObject bg = __instance.gameObject.FindInChildren("BG_Body", true);
            // 16 34 49 230
            bg.GetComponent<Image>().color = new Color32(68, 135, 135, 230);
            bg.GetComponent<Image>().sprite = ResourcesManager.GetSpriteForUI("mansus_portal_body_bg3");
            bg.GetComponent<Image>().type = Image.Type.Simple;
        }*/

        public static void fetchReferencesAndInit()
        {
            Birdsong.Tweet(VerbosityLevel.SystemChatter, 0, "Hello World from Fevered Imagination's Table Manager!");
            tabletop = GameObject.Find("TabletopBackground");
            tabletopImageComponent = tabletop.GetComponent<Image>();
            leather = GameObject.Find("Leather");
            tableLeatherImageComponent = leather.GetComponent<Image>();
            legs = GameObject.Find("Legs");
            tableLegsImageComponent = legs.GetComponent<Image>();

            string tabletopImage = Machine.GetLeverForCurrentPlaythrough("currentTabletopImage");
            string tableLeather = Machine.GetLeverForCurrentPlaythrough("currentTableLeather");
            string tableLegs = Machine.GetLeverForCurrentPlaythrough("currentTableLegs");

            if (tabletopImage != null) tabletopImageComponent.sprite = ResourcesManager.GetSpriteForUI(tabletopImage);
            if (tableLeather != null) tableLeatherImageComponent.sprite = ResourcesManager.GetSpriteForUI(tableLeather);
            if (tableLegs != null) tableLegsImageComponent.sprite = ResourcesManager.GetSpriteForUI(tableLegs);
        }

        static void fadeLayer(GameObject gameObject, Image imageComponent, string newSpriteName, string storeKey)
        {
            Machine.SetLeverForCurrentPlaythrough(storeKey, newSpriteName);
            GameObject fadingObject = UnityEngine.Object.Instantiate(gameObject, gameObject.transform.parent);
            imageComponent.sprite = ResourcesManager.GetSpriteForUI(newSpriteName);
            CanvasRendererFading imgf = fadingObject.AddComponent<CanvasRendererFading>();
            imgf.fadingCR = fadingObject.GetComponent<CanvasRenderer>();
        }

        public static void checkForTableChangeOnRecipeCompletion(Situation situation)
        {
            string tabletopImage = situation.Recipe.RetrieveProperty<string>("tabletopImage");
            string tableLeather = situation.Recipe.RetrieveProperty<string>("tableLeather");
            string tableLegs = situation.Recipe.RetrieveProperty<string>("tableLegs");

            if (tabletopImage != null) fadeLayer(tabletop, tabletopImageComponent, tabletopImage, "currentTabletopImage");
            if (tableLeather != null) fadeLayer(leather, tableLeatherImageComponent, tableLeather, "currentTableLeather");
            if (tableLegs != null) fadeLayer(legs, tableLegsImageComponent, tableLegs, "currentTableLegs");
        }
    }
}