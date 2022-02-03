namespace TheRoost.LocalApplications
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
        }

        private static void GetSpriteFix(ref string folder)
        {
            folder = folder.Replace('/', '\\');
        }
    }
}
