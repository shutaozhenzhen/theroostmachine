using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;

using HarmonyLib;

using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Fucine.DataImport;
using SecretHistories.UI;

using Hoard.Tools;
using Hoard.Examples;

public static class Beachcomber
{
    private static Dictionary<Type, Dictionary<string, Type>> knownUnknownProperties;
    private readonly static Dictionary<IEntityWithId, Dictionary<string, object>> beachcomberStorage = new Dictionary<IEntityWithId, Dictionary<string, object>>();
    private readonly static List<Type> cuckooEggs = new List<Type>();

    public static void Initialise()
    {
        ///natively, verbs don't have "comments" property - let's add it just for test/show off
        ClaimProperty<Verb>("comments", typeof(string));

        var harmony = new Harmony("beachcomber");

        var original = typeof(AbstractEntity<Element>).GetMethod("PopUnknownKeysToLog", BindingFlags.NonPublic | BindingFlags.Instance);
        var patched = typeof(Beachcomber).GetMethod("KnowUnknown", BindingFlags.NonPublic | BindingFlags.Static);
        harmony.Patch(original, prefix: new HarmonyMethod(patched));

        original = typeof(CompendiumLoader).GetMethod("PopulateCompendium");
        patched = typeof(Beachcomber).GetMethod("CuckooTranspiler", BindingFlags.NonPublic | BindingFlags.Static);
        harmony.Patch(original, transpiler: new HarmonyMethod(patched));
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

                        object value = Hoard.Tools.Importer.LoadValue(__instance, propertiesToComb[propertyName], propertiesToClaim[propertyName], log);
                        if (value != null)
                            beachcomberStorage[__instance].Add(propertyName, value);
                    }
        }
    }

    public static void ClaimProperty<T>(string propertyName, Type propertyType) where T : AbstractEntity<T>
    {
        Type entityType = typeof(T);
        if (entityType.GetCustomAttribute(typeof(FucineImportable), false) == null)
        {
            NoonUtility.LogWarning(String.Format("Class {0} has no FucineImportable attribute and will not be loaded. Custom property '{1}' is not claimed.", entityType.Name, propertyName));
            return;
        }

        if (knownUnknownProperties == null)
            knownUnknownProperties = new Dictionary<Type, Dictionary<string, Type>>();

        if (knownUnknownProperties.ContainsKey(entityType) == false)
            knownUnknownProperties[entityType] = new Dictionary<string, Type>();

        knownUnknownProperties[entityType].Add(propertyName.ToLower(), propertyType);
    }

    public static T RetrieveProperty<T>(this IEntityWithId owner, string property)
    {
        if (beachcomberStorage.ContainsKey(owner) && beachcomberStorage[owner].ContainsKey(property))
            return (T)beachcomberStorage[owner][property];
        else
            return default(T);
    }

    public static void InfectFucineWith<T>() where T : AbstractEntity<T>
    {
        cuckooEggs.Add(typeof(T));
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