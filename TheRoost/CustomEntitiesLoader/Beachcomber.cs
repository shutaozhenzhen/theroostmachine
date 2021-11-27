using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.IO;

using HarmonyLib;

using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;

using TheRoost.Hoard;
using UnityEngine;

namespace TheRoost.Invocations
{
    internal class Beachcomber
    {
        private readonly static Dictionary<Type, Dictionary<string, Type>> knownUnknownProperties = new Dictionary<Type, Dictionary<string, Type>>();
        private readonly static Dictionary<IEntityWithId, Dictionary<string, object>> beachcomberStorage = new Dictionary<IEntityWithId, Dictionary<string, object>>();
        private readonly static List<Type> cuckooEggs = new List<Type>();

        private static void Invoke()
        {
            if (TheRoostMachine.alreadyAssembled)
                return;

            var harmony = new Harmony("theroost.beachcomber");

            var original = typeof(AbstractEntity<Element>).GetMethod("PopUnknownKeysToLog", BindingFlags.NonPublic | BindingFlags.Instance);
            var patched = typeof(Beachcomber).GetMethod("KnowUnknown", BindingFlags.NonPublic | BindingFlags.Static);
            harmony.Patch(original, prefix: new HarmonyMethod(patched));

            original = typeof(CompendiumLoader).GetMethod("PopulateCompendium");
            patched = typeof(Beachcomber).GetMethod("CuckooTranspiler", BindingFlags.NonPublic | BindingFlags.Static);
            harmony.Patch(original, transpiler: new HarmonyMethod(patched));
        }

        public static void ClaimProperty<T>(string propertyName, Type propertyType) where T : AbstractEntity<T>
        {
            Type entityType = typeof(T);
            if (entityType.GetCustomAttribute(typeof(FucineImportable), false) == null)
            {
                TheRoost.Sing("Trying to claim '{0}' of {1}s, but {1} has no FucineImportable attribute and will not be loaded.", propertyName, entityType.Name);
                return;
            }

            if (knownUnknownProperties.ContainsKey(entityType) == false)
                knownUnknownProperties[entityType] = new Dictionary<string, Type>();

            knownUnknownProperties[entityType].Add(propertyName.ToLower(), propertyType);
        }

        public static T RetrieveProperty<T>(IEntityWithId owner, string propertyName)
        {
            propertyName = propertyName.ToLower();
            if (beachcomberStorage.ContainsKey(owner) && beachcomberStorage[owner].ContainsKey(propertyName))
                return (T)beachcomberStorage[owner][propertyName];
            else
                return default(T);
        }

        public static void InfectFucineWith<T>() where T : AbstractEntity<T>
        {
            cuckooEggs.Add(typeof(T));
        }

        private static void KnowUnknown(IEntityWithId __instance, Hashtable ___UnknownProperties, ContentImportLog log)
        {
            if (knownUnknownProperties.ContainsKey(__instance.GetType()))
            {
                Hashtable propertiesToComb = new Hashtable(___UnknownProperties);
                Dictionary<string, Type> propertiesToClaim = knownUnknownProperties[__instance.GetType()];

                if (propertiesToClaim.Count > 0)
                    foreach (string propertyName in propertiesToComb.Keys)
                        if (propertiesToClaim.ContainsKey(propertyName))
                        {
                            log.LogInfo(String.Format("Known-Unknown property '{0}' for '{1}' {2}", propertyName, __instance.Id, __instance.GetType().Name));
                            ___UnknownProperties.Remove(propertyName);

                            if (beachcomberStorage.ContainsKey(__instance) == false)
                                beachcomberStorage[__instance] = new Dictionary<string, object>();

                            object value = Hoard.Importer.LoadValue(__instance, propertiesToComb[propertyName], propertiesToClaim[propertyName], log);
                            if (value != null)
                                beachcomberStorage[__instance].Add(propertyName, value);
                        }
            }
        }

        private static IEnumerable<CodeInstruction> CuckooTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            MethodInfo targetMethod = typeof(Compendium).GetMethod("InitialiseForEntityTypes");
            MethodInfo cuckoo = typeof(Beachcomber).GetMethod("Cuckoo", BindingFlags.Static | BindingFlags.NonPublic);

            for (int i = 0; i < codes.Count; i++)
                if (codes[i].Calls(targetMethod))
                {
                    i -= 2;

                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, 2));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldloca_S, 1));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_2));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldarg_0));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Ldfld, typeof(CompendiumLoader).GetField("_log", BindingFlags.Instance | BindingFlags.NonPublic)));
                    codes.Insert(i++, new CodeInstruction(OpCodes.Call, cuckoo));

                    return codes.AsEnumerable();
                }

            return codes.AsEnumerable();
        }

        private static void Cuckoo(ref List<Type> nativeFucineClasses, ref Dictionary<string, EntityTypeDataLoader> fucineLoaders, string cultureId, ContentImportLog log)
        {
            foreach (Type customFucineClass in cuckooEggs)
            {
                nativeFucineClasses.Add(customFucineClass);
                FucineImportable fucineImportable = (FucineImportable)customFucineClass.GetCustomAttribute(typeof(FucineImportable), false);
                fucineLoaders.Add(fucineImportable.TaggedAs.ToLower(), new EntityTypeDataLoader(customFucineClass, fucineImportable.TaggedAs, cultureId, log));
            }
        }
    }
}