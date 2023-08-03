using System.Collections;
using SecretHistories.Fucine;
using SecretHistories.Entities;

namespace Roost.Beachcomber
{
    static class CuckooJr
    {
        internal static void Enact()
        {
            Machine.Patch<Element>(
                    original: nameof(Element.InheritFrom),
                    postfix: typeof(CuckooJr).GetMethodInvariant(nameof(CuckooJr.InheritClaimedPropertiesElement)));

            Machine.Patch<Recipe>(
                    original: nameof(Element.InheritFrom),
                    postfix: typeof(CuckooJr).GetMethodInvariant(nameof(CuckooJr.InheritClaimedPropertiesRecipe)));
        }

        private static void InheritClaimedPropertiesElement(Element __instance, Element inheritFromElement)
        {
            InheritClaimedProperties(inheritFromElement, __instance);
        }
        private static void InheritClaimedPropertiesRecipe(Recipe __instance, Element inheritFromRecipe)
        {
            InheritClaimedProperties(inheritFromRecipe, __instance);
        }

        private static void InheritClaimedProperties(IEntityWithId inheritFrom, IEntityWithId receiver)
        {
            var inheritingProperties = inheritFrom.GetCustomProperties();

            if (inheritingProperties != null)
                foreach (var inheritingProperty in inheritingProperties)
                    MergeCustomProperty(receiver, inheritingProperty.Key, inheritingProperty.Value);
        }

        public static void MergeCustomProperty(IEntityWithId owner, string propertyName, object inheritingValue)
        {
            if (!owner.HasCustomProperty(propertyName))
            {
                owner.SetCustomProperty(propertyName, inheritingValue);
                return;
            }

            var alreadyExistingProperty = owner.RetrieveProperty(propertyName);
            MergeValues(inheritingValue, alreadyExistingProperty);
        }

        private static object MergeValues(object donor, object receiver)
        {
            if (donor == null)
                return receiver;

            if (receiver == null)
                return donor;

            if (donor.GetType() != receiver.GetType())
                return receiver;

            if (receiver is IList newList)
            {
                var existingList = donor as IList;

                foreach (var entry in existingList)
                    newList.Add(entry);

                return newList;
            }

            if (receiver is IDictionary newDict)
            {
                var existingDict = donor as IDictionary;

                foreach (var entryKey in existingDict.Keys)
                    if (newDict.Contains(entryKey))
                        newDict[entryKey] = MergeValues(existingDict[entryKey], newDict[entryKey]);
                    else
                        newDict.Add(entryKey, existingDict[entryKey]);

                return newDict;
            }

            return receiver;
        }
    }
}
