using SecretHistories.UI;

namespace Roost.World
{
    //here we fix (aka steal-pick-peck - geddit? geddit? it was previously a beachcomber's class) the bugs
    internal static class BugsPicker
    {
        internal static void Fix()
        {
            //why this keeps happening
            Machine.Patch(
                original: typeof(ResourcesManager).GetMethodInvariant("GetSprite"),
                prefix: typeof(BugsPicker).GetMethodInvariant("GetSpriteFix"));

            Machine.Patch(typeof(ElementStack).GetMethodInvariant("SetMutation"),
                postfix: typeof(BugsPicker).GetMethodInvariant("FixMutationsDisplay"));
        }

        private static void GetSpriteFix(ref string folder)
        {
            folder = folder.Replace('/', '\\');
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
