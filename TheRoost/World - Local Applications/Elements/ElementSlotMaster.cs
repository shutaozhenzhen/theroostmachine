using System.Collections.Generic;

using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Commands;
using SecretHistories.Commands.SituationCommands;
using SecretHistories.Services;
using SecretHistories.Spheres;

using SecretHistories.Core;

using UnityEngine;

namespace Roost.World.Elements
{
    //currently unused and doesnt work
    static class ElementSlotMaster
    {

        internal static void Enact()
        {
            Machine.Patch(
                original: typeof(ElementStack).GetConstructors()[1],
                postfix: typeof(ElementSlotMaster).GetMethodInvariant(nameof(CreateDominion)));

            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.GetAspects)),
                postfix: typeof(ElementSlotMaster).GetMethodInvariant(nameof(GetAspectsWithEquip)));

            Machine.Patch(
                original: typeof(ElementStackCreationCommand).GetMethodInvariant(nameof(ElementStackCreationCommand.Execute)),
                prefix: typeof(ElementSlotMaster).GetMethodInvariant(nameof(CreateSphere)));

            AtTimeOfPower.TabletopSceneInit.Schedule(OnTabletop, PatchType.Prefix);

            DominionPrefab = Watchman.Get<PrefabFactory>().GetPrefabObjectFromResources<SituationWindow>("").transform.Find("VerbThresholdsDominion").gameObject;
        }
        private static void CreateSphere(ElementStackCreationCommand __instance)
        {
            SphereSpec slot = Watchman.Get<Compendium>().GetEntityById<Verb>("work").Slot;
            PopulateDominionCommand equipDominion = new PopulateDominionCommand("VerbThresholds", slot);
            __instance.Dominions.Add(equipDominion);
        }

        private static GameObject DominionPrefab;
        private static void CreateDominion(ElementStack __instance)
        {
            if (__instance.HasNoEquip())
                return;

            Birdsong.Sing(__instance.Id);
            var container = GameObject.Find("TokenDetailsWindow")?.transform.GetChild(1);

            if (container == null)
                return;

            SituationDominion dominion = GameObject.Instantiate(DominionPrefab).GetComponent<SituationDominion>();
            dominion.transform.SetParent(container.transform);
            dominion.transform.localPosition = Vector3.zero;
            dominion.transform.localScale = Vector3.one;

            __instance.RegisterDominion(dominion);

            dominion.OnSphereAdded = new OnSphereAddedEvent();
            dominion.OnSphereRemoved = new OnSphereRemovedEvent();
        }

        private static void GetAspectsWithEquip(ElementStack __instance, ref AspectsDictionary __result, bool includeSelf)
        {
            if (__instance.Dominions.Count == 0)
                return;

            foreach (Sphere sphere in __instance.Dominions[0].Spheres)
                __result.CombineAspects(sphere.GetTotalAspects(includeSelf));
        }

        private static void OnTabletop()
        {
            GameObject tokenDetails = GameObject.Find("TokenDetailsWindow");
            var container = new GameObject("ElementSlotsContainer");
            container.transform.SetParent(tokenDetails.transform);
            RectTransform transform = container.AddComponent<RectTransform>();
            transform.SetAsLastSibling();
            transform.localScale = Vector3.one;
            transform.localPosition = new Vector3(-170, -180, 0);
            transform.localEulerAngles = new Vector3(0, 0, 0);
        }

        private static bool HasNoEquip(this ElementStack stack)
        {
            return !stack.IsValid() || stack.EntityId == "tlg.note" || stack.Metafictional;
        }
    }
}
