using SecretHistories.UI;
using SecretHistories.Spheres;

namespace Roost.World
{
    internal static class Optimizations
    {
        internal static void Enact()
        {
            Machine.Patch(
                original: typeof(Sphere).GetMethodInvariant(nameof(Sphere.AcceptToken)),
                prefix: typeof(Optimizations).GetMethodInvariant(nameof(EnableNonDormantTokens)),
                postfix: typeof(Optimizations).GetMethodInvariant(nameof(DisableDormantTokens)));
        }

        private static void EnableNonDormantTokens(Sphere __instance, Token token)
        {
            if (__instance.SphereCategory != SecretHistories.Enums.SphereCategory.Dormant)
                token.gameObject.SetActive(true);
        }

        private static void DisableDormantTokens(Sphere __instance, Token token)
        {
            if (__instance.SphereCategory == SecretHistories.Enums.SphereCategory.Dormant)
                token.gameObject.SetActive(false);
        }
    }
}
