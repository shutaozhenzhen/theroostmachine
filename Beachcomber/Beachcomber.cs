using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;

using SecretHistories.Fucine;
using SecretHistories.Entities;


public static class Beachcomber
{
    public static Dictionary<Type, List<string>> knownUnknownProperties;
    public static Dictionary<IEntityWithId, Dictionary<string, object>> storage;

    public static void Initialise()
    {
        knownUnknownProperties = new Dictionary<Type, List<string>>();
        foreach (Type entityType in loadableEntities)
            knownUnknownProperties[entityType] = new List<string>();

        storage = new Dictionary<IEntityWithId, Dictionary<string, object>>();

        var harmony = new Harmony("beachcomber");
        var original = typeof(AbstractEntity<Element>).GetMethod("PopUnknownKeysToLog", BindingFlags.NonPublic | BindingFlags.Instance);
        var patched = typeof(Beachcomber).GetMethod("KnowUnknown", BindingFlags.NonPublic | BindingFlags.Static);
        harmony.Patch(original, prefix: new HarmonyMethod(patched));

        ///natively, verbs don't have "comments" property - let's add it just for test/show off
        MarkPropertyAsOwned(typeof(Verb), "comments");
    }

    private static void KnowUnknown(IEntityWithId __instance, Hashtable ___UnknownProperties)
    {
        Hashtable propertiesToComb = new Hashtable(___UnknownProperties);
        List<string> propertiesToClaim = knownUnknownProperties[__instance.GetType()];
        
        foreach (string propertyName in propertiesToComb.Keys)
            if (propertiesToClaim.Contains(propertyName))
            {
                if (storage.ContainsKey(__instance) == false)
                    storage[__instance] = new Dictionary<string, object>();
                storage[__instance].Add(propertyName, propertiesToComb[propertyName]);

                ___UnknownProperties.Remove(propertyName);
                NoonUtility.Log(String.Concat("Known-Unknown property '", propertyName, "' for '", __instance.Id, "' ", __instance.GetType().Name));
            }
    }

    public static void MarkPropertyAsOwned(Type entityType, string property_name)
    {
        knownUnknownProperties[entityType].Add(property_name);
    }

    public static T GetProperty<T>(this IEntityWithId owner, string property)
    {
        if (storage.ContainsKey(owner) && storage[owner].ContainsKey(property))
            return (T)storage[owner][property];
        else
            return default(T);
    }

    private static readonly Type[] loadableEntities = { 
                                                        typeof(AngelSpecification), 
                                                        typeof(Culture), 
                                                        typeof(DeckSpec), 
                                                        typeof(Dictum), 
                                                        typeof(Element), 
                                                        typeof(Ending), 
                                                        typeof(Expulsion), 
                                                        typeof(Legacy),
                                                        typeof(LinkedRecipeDetails), 
                                                        typeof(MorphDetails), 
                                                        typeof(MutationEffect), 
                                                        typeof(Portal), 
                                                        typeof(Recipe), 
                                                        typeof(Setting), 
                                                        typeof(SphereSpec), 
                                                        typeof(Verb),
                                                      };
}
