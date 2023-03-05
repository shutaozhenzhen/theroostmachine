using System;
using System.Collections.Generic;

using SecretHistories.Spheres;
using SecretHistories.Enums;
using SecretHistories.UI;
using SecretHistories.Entities;
using SecretHistories.Services;
using SecretHistories.Spheres.Angels;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Roost;
using Roost.World.Recipes;
using Roost.World.Recipes.Entities;
using Roost.Twins;


namespace Roost.World.Slots
{
    public static class XAngelMaster
    {
        const string SLOT_X = "xtrigger";
        private static Sprite defaultConsumeSprite;
        internal static void Enact()
        {
            Machine.ClaimProperty<SphereSpec, string>(SLOT_X);
            Machine.Patch(
                original: typeof(SphereSpec).GetMethodInvariant(nameof(SphereSpec.MakeAngels)),
                postfix: typeof(XAngelMaster).GetMethodInvariant(nameof(MakeXAngel)));

            Machine.Patch(
                original: typeof(SlotDetailsWindow).GetMethodInvariant("SetSlot"),
                postfix: typeof(XAngelMaster).GetMethodInvariant(nameof(SetXAngelInfo)));

            Transform thresholdSphere = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<ThresholdSphere>().transform;
            defaultConsumeSprite = thresholdSphere.Find("Icon-Slots").GetChild(0).GetChild(0).GetComponent<Image>().sprite;
        }

        private static void MakeXAngel(Sphere inSphere, SphereSpec __instance, List<IAngel> __result)
        {
            if (__instance.Consumes)
                return;

            string trigger = __instance.RetrieveProperty<string>(SLOT_X);
            if (trigger != null)
            {
                XAngel angel = new XAngel();
                angel._trigger = trigger;
                angel.SetWatch(inSphere);
                __result.Add(angel);
            }
        }

        private static void SetXAngelInfo(SphereSpec slotSpec, TextMeshProUGUI ___consumesInfo, Image ___consumesIcon)
        {
            string xtrigger = slotSpec.RetrieveProperty<string>(SLOT_X);

            if (xtrigger != null)
            {
                ___consumesInfo.gameObject.SetActive(true);
                Element element = Watchman.Get<Compendium>().GetEntityById<Element>(xtrigger);

                if (!element.IsNullEntity())
                    ___consumesInfo.text = element.Label + ": " + element.Description;
                else
                    ___consumesInfo.text = string.Empty;

                ___consumesIcon.sprite = GetXIcon(xtrigger);
            }
            else if (slotSpec.Consumes)
            {
                ___consumesInfo.GetComponent<Babelfish>().SetValuesForCurrentCulture();
                ___consumesIcon.sprite = defaultConsumeSprite;
            }
        }

        public static Sprite GetXIcon(string triggerId)
        {
            string icon = "xangel_default";
            Element element = Machine.GetEntity<Element>(triggerId);
            if (!element.IsNullEntity())
                icon = element.Icon;

            return ResourcesManager.GetSpriteForUI(icon);
        }
    }
}

namespace SecretHistories.Spheres.Angels
{
    public class XAngel : IAngel
    {
        private ThresholdSphere _watchingOverThreshold;
        public int Authority => 9;
        public bool Defunct { get; protected set; }

        public string _trigger; //let's pretend it's private
        private Sprite _oldSprite;

        public void Act(float seconds, float metaseconds) { }

        public void SetWatch(Sphere sphere)
        {
            _watchingOverThreshold = sphere as ThresholdSphere;
            if (_watchingOverThreshold == null)
            {
                Birdsong.TweetLoud($"Tried to set an xtriggering angel to watch over sphere {sphere.Id}, but it isn't a threshold sphere, so that won't work.");
                return;
            }

            _watchingOverThreshold.ShowAngelPresence(this);
        }

        public bool MinisterToDepartingToken(Token token, Context context)
        {
            if (context.actionSource == Context.ActionSource.FlushingTokens)
            {
                Situation situation = _watchingOverThreshold.GetContainer() as Situation;

                Crossroads.ResetCache();
                Crossroads.MarkLocalSituation(situation);
                Crossroads.MarkLocalToken(token);

                GrandEffects.RunXTriggers(token, situation, new Dictionary<string, int>() { { _trigger, 1 } });

                RecipeExecutionBuffer.ApplyAllEffects();
                RecipeExecutionBuffer.ApplyVFX();

                return true;
            }

            return false;
        }

        public bool MinisterToEvictedToken(Token token, Context context)
        {
            return false;
        }

        public void Retire()
        {
            _watchingOverThreshold.HideAngelPresence(this);
            Defunct = true;
        }

        public void ShowRelevantVisibleCharacteristic(List<VisibleCharacteristic> visibleCharacteristics)
        {
            foreach (var v in visibleCharacteristics.FindAll(v => v.VisibleCharacteristicId == VisibleCharacteristicId.Consuming))
            {
                Image slotIcon = v.transform.GetChild(0).GetComponent<Image>();
                _oldSprite = slotIcon.sprite;
                slotIcon.sprite = Roost.World.Slots.XAngelMaster.GetXIcon(_trigger);

                v.Show();
            }
        }

        public void HideRelevantVisibleCharacteristic(List<VisibleCharacteristic> visibleCharacteristics)
        {
            foreach (var v in visibleCharacteristics.FindAll(v => v.VisibleCharacteristicId == VisibleCharacteristicId.Consuming))
            {
                v.transform.GetChild(0).GetComponent<Image>().sprite = _oldSprite;
                v.Hide();
            }
        }
    }
}