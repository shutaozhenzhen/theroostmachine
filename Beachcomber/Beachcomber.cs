using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using HarmonyLib;

using SecretHistories.Fucine;
using SecretHistories.Entities;
using SecretHistories.Fucine.DataImport;


public static class Beachcomber
{
    public static Dictionary<Type, Dictionary<string, Type>> knownUnknownProperties;
    public static Dictionary<IEntityWithId, Dictionary<string, object>> storage;

    public static void Initialise()
    {
        knownUnknownProperties = new Dictionary<Type, Dictionary<string, Type>>();
        foreach (Type entityType in loadableEntities)
            knownUnknownProperties[entityType] = new Dictionary<string, Type>();

        storage = new Dictionary<IEntityWithId, Dictionary<string, object>>();

        var harmony = new Harmony("beachcomber");
        var original = typeof(AbstractEntity<Element>).GetMethod("PopUnknownKeysToLog", BindingFlags.NonPublic | BindingFlags.Instance);
        var patched = typeof(Beachcomber).GetMethod("KnowUnknown", BindingFlags.NonPublic | BindingFlags.Static);
        harmony.Patch(original, prefix: new HarmonyMethod(patched));

        ///natively, verbs don't have "comments" property - let's add it just for test/show off
        MarkPropertyAsOwned(typeof(Verb), "comments", typeof(PhonyFucineClass));
    }

    private static void KnowUnknown(IEntityWithId __instance, Hashtable ___UnknownProperties)
    {
        Hashtable propertiesToComb = new Hashtable(___UnknownProperties);
        Dictionary<string, Type> propertiesToClaim = knownUnknownProperties[__instance.GetType()];

        foreach (string propertyName in propertiesToComb.Keys)
            if (propertiesToClaim.ContainsKey(propertyName))
            {
                FormatAndStoreProperty(__instance, propertyName, propertiesToComb[propertyName]);

                ___UnknownProperties.Remove(propertyName);
                NoonUtility.Log(String.Concat("Known-Unknown property '", propertyName, "' for '", __instance.Id, "' ", __instance.GetType().Name));
            }
    }

    public static void FormatAndStoreProperty(IEntityWithId entity, string propertyName, object propertyValue)
    {
        if (storage.ContainsKey(entity) == false)
            storage[entity] = new Dictionary<string, object>();

        Type propertyType = knownUnknownProperties[entity.GetType()][propertyName];
        if (propertyValue.GetType() == typeof(EntityData))
        {
            propertyValue = FactoryInstantiator.CreateEntity(propertyType, propertyValue as EntityData, new ContentImportLog());
        }
        else
            propertyValue = Convert.ChangeType(propertyValue, propertyType);

        storage[entity].Add(propertyName, propertyValue);
    }

    public static void MarkPropertyAsOwned(Type entityType, string propertyName, Type propertyType)
    {
        knownUnknownProperties[entityType].Add(propertyName, propertyType);
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


public class PhonyFucineClass : AbstractEntity<PhonyFucineClass>
{
    [FucineValue(DefaultValue = "")]
    public string label { get; set; }
    [FucineList]
    public List<string> list { get; set; }
    [FucineDict]
    public Dictionary<string, int> dict { get; set; }

    public PhonyFucineClass(EntityData importDataForEntity, ContentImportLog log)
        : base(importDataForEntity, log)
    {
    }

    protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
    {
    }
}