using SecretHistories.UI;
using SecretHistories.Constants.Modding;

namespace Roost.World
{
    //here we fix (aka steal-pick-peck - geddit? geddit? it was previously a beachcomber's class) the bugs
    internal static class BugsPicker
    {
        internal static void Fix()
        {
            Machine.Patch(
                original: typeof(ElementStack).GetMethodInvariant(nameof(ElementStack.SetMutation)),
                postfix: typeof(BugsPicker).GetMethodInvariant(nameof(FixMutationsDisplay)));
        }

        private static void FixMutationsDisplay(ElementStack __instance)
        {
            Context context = new Context(Context.ActionSource.SituationEffect);
            var sphereContentsChangedEventArgs = new SecretHistories.Constants.Events.SphereContentsChangedEventArgs(__instance.Token.Sphere, context);
            sphereContentsChangedEventArgs.TokenChanged = __instance.Token;
            __instance.Token.Sphere.NotifyTokensChangedForSphere(sphereContentsChangedEventArgs);
        }
    }
}
