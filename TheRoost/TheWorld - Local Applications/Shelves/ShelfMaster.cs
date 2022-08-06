using Roost.Twins.Entities;
using SecretHistories;
using SecretHistories.Abstract;
using SecretHistories.Commands;
using SecretHistories.Commands.Encausting;
using SecretHistories.Constants;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.Services;
using SecretHistories.Spheres;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roost.World.Shelves
{
    class ShelfMaster
    {
        public static List<ShelfManifestation> RegisteredShelfManifestations = new();

        public static void Enact()
        {
            AtTimeOfPower.TabletopSceneInit.Schedule(SpawnDummyShelf, PatchType.Postfix);
            Machine.Patch(
                original: typeof(PrefabFactory).GetMethodInvariant(nameof(PrefabFactory.CreateManifestationPrefab)),
                prefix: typeof(ShelfMaster).GetMethodInvariant(nameof(HandleZoneManifestationPrefab))
            );

            Type[] types = { };
            Machine.Patch(
                original: typeof(EncaustablesSerializationBinder).GetConstructor(types),
                postfix: typeof(ShelfMaster).GetMethodInvariant(nameof(AddCommandToConstructor))
            );

            Machine.Patch(
                original: typeof(TabletopChoreographer).GetMethodInvariant(nameof(TabletopChoreographer.GroupAllStacks)),
                postfix: typeof(ShelfMaster).GetMethodInvariant(nameof(MoveStacksToShelves))
            );
        }

        public static Sphere GetTabletopSphere()
        {
            FucinePath tabletopSpherePath = Watchman.Get<HornedAxe>().GetDefaultSpherePath(OccupiesSpaceAs.Intangible);
            return Watchman.Get<HornedAxe>().GetSphereByAbsolutePath(tabletopSpherePath);
        }

        public static void SpawnDummyShelf()
        {
            Sphere tabletopSphere = GetTabletopSphere();
            var quickDuration = Watchman.Get<Compendium>().GetSingleEntity<Dictum>().DefaultQuickTravelDuration;

            var zoneLocation = new TokenLocation(new Vector3(0, 0, 0), tabletopSphere.GetAbsolutePath());
            var zoneCreationCommand = new ShelfCreationCommand("statshelf"); // entityId
            var zoneTokenCreationCommand = new TokenCreationCommand(zoneCreationCommand, zoneLocation).WithDestination(zoneLocation, quickDuration);
            zoneTokenCreationCommand.Execute(Context.Unknown(), tabletopSphere);
            //Birdsong.Sing("Spawned a Zone! Sphere path:", tabletopSpherePath);
        }

        public static bool HandleZoneManifestationPrefab(Type manifestationType, Transform parent, ref IManifestation __result)
        {
            if (manifestationType != typeof(ShelfManifestation)) return true;
            Birdsong.Sing(Birdsong.Incr(), "Creating the manifestation for a zone. Building the GO and stuff...");

            GameObject go = new();
            go.transform.SetParent(parent);
            ShelfManifestation zm = go.AddComponent<ShelfManifestation>();

            __result = zm;
            return false;
        }

        public static void AddCommandToConstructor(EncaustablesSerializationBinder __instance)
        {
            List<Type> l = Machine.GetFieldInvariant(typeof(EncaustablesSerializationBinder), "encaustmentTypes").GetValue(__instance) as List<Type>;
            l.Add(typeof(ShelfCreationCommand));
        }

        public static void MoveStacksToShelves()
        {
            /*
             * 1. Get the tabletop sphere
             * 2. Get the tokens list
             * 3. For each shelf, if it matches the shelf expression, check each area
             * 4. If any non-full area matches, move the token and move on
             */
            Sphere tabletopSphere = GetTabletopSphere();
            List<Token> stacksToMove = tabletopSphere.GetElementTokens();
            foreach(ShelfManifestation sm in RegisteredShelfManifestations)
            {
                // For each area, is it full? If not, filter more and assign one by one
                foreach(ShelfArea area in sm.entity.Areas)
                {
                    // Get the filtered list of valid tokens
                    // We recompute this list for each area and not outside of the loop, because the previous one may have shrunk the stackTokens original list.
                    List<Token> validStacksForShelf = stacksToMove;
                    if (!sm.entity.Expression.isUndefined)
                    {
                        validStacksForShelf = validStacksForShelf.FilterTokens(sm.entity.Expression);
                    }

                    // If at any point the generally valid list for the entire shelf is empty, stop trying to fill its areas
                    if (validStacksForShelf.Count == 0) break;

                    Token dummyCard = validStacksForShelf[0];

                    // if the area is full, don't try to fill it.
                    if (sm.NextAvailablePosition(area, dummyCard) == Vector2.negativeInfinity) continue;

                    // If the area has an expression to filter things too, restrict the final list even more
                    List<Token> validStacksForArea = validStacksForShelf;
                    if (!area.Expression.isUndefined)
                    {
                        validStacksForArea = validStacksForArea.FilterTokens(area.Expression);
                    }

                    sm.FillArea(area, validStacksForArea, stacksToMove);
                }
            }
        }
    }
}
