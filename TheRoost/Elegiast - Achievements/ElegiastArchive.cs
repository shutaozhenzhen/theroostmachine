using System;
using System.Collections.Generic;

using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Assets.Scripts.Application.Entities.NullEntities;
using SecretHistories.Entities;
using SecretHistories.Services;
using SecretHistories.Commands;

namespace Roost.Elegiast
{
    internal static class ElegiastArchive
    {
        const string storageElementId = "elegiastStorageElement";
        const string storageSphereId = "ElegiastStorage";
        static ElementStack storage;
        internal static void Enact()
        {
            AtTimeOfPower.NewGame.Schedule(SetGlobalStorage, PatchType.Postfix);
            AtTimeOfPower.TabletopSceneInit.Schedule(GetGlobalStorage, PatchType.Postfix);
            Vagabond.CommandLine.AddCommand("/fixstorage", SendStorageHome);

            Machine.Patch(
                original: Machine.GetMethod<SphereCreationCommand>(nameof(SphereCreationCommand.ExecuteOn), typeof(FucineRoot), typeof(Context)),
                postfix: typeof(ElegiastArchive).GetMethodInvariant("Bop"));
        }
        static void Bop(FucineRoot root, Context context, SphereCreationCommand __instance)
        {
            //part of the spheres already exist on the scene and another is created during runtime and none can be loaded from the save natively? belissimo
            if (__instance.GoverningSphereSpec.Id == "situationsmalleary")
                return;

            Sphere sphere = root.GetSphereById(__instance.GoverningSphereSpec.Id);
            if (sphere == null)
            {
                sphere = Watchman.Get<PrefabFactory>().InstantiateSphere(__instance.GoverningSphereSpec, root);
                sphere.OwnerSphereIdentifier = __instance.OwnerSphereIdentifier;
                if (__instance.Shrouded)
                    sphere.Shroud();
                else
                    sphere.Unshroud();

                foreach (TokenCreationCommand tokenCreationCommand in __instance.Tokens)
                    tokenCreationCommand.Execute(context, sphere);
            }
        }
        private static void SetGlobalStorage()
        {
            SphereSpec spec = new SphereSpec(typeof(DrawPile), storageSphereId);
            Sphere storageSphere = Watchman.Get<PrefabFactory>().InstantiateSphere(spec);

            storageSphere.ProvisionElementToken(storageElementId, 1);
            FucineRoot.Get().AttachSphere(storageSphere);
        }

        private static void GetGlobalStorage()
        {
            Sphere storageSphere = FucineRoot.Get().GetSphereById(storageSphereId);

            List<Token> storageTokens = storageSphere.Tokens;
            if (storageTokens.Count == 0)
            {
                Birdsong.Tweet("Global Storage lacks a storage element. That's no good. Creating a new one, but watch out");
                storage = storageSphere.ProvisionElementToken(storageElementId, 1).Payload as ElementStack;
                return;
            }

            storage = storageSphere.Tokens[0].Payload as ElementStack;
        }

        internal static void SetValue(string key, string value)
        {
            storage.SetIllumination(key, value);
        }

        internal static string GetValue(string key)
        {
            return storage.GetIllumination(key);
        }

        internal static void SendStorageHome(string[] useless)
        {
            FucineRoot.Get().GetSphereById(storageSphereId).AcceptToken(storage.Token, new Context(Context.ActionSource.Metafictional)); //I just like the word
        }
    }
}

namespace Roost
{
    public static partial class Machine
    {
        internal static void SetGlobalValue(string key, string value)
        {
            Roost.Elegiast.ElegiastArchive.SetValue(key, value);
        }

        internal static string GetGlobalValue(string key)
        {
            return Roost.Elegiast.ElegiastArchive.GetValue(key);
        }
    }
}