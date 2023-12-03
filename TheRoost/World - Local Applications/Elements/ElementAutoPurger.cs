using SecretHistories.UI;
using SecretHistories.Spheres;
using SecretHistories.Entities;
using SecretHistories.Services;

namespace Roost.World.Elements
{
    static class ElementAutoPurger
    {
        const string AUTOPURGE = "autopurge";

        internal static void Enact()
        {
            Machine.ClaimProperty<Element, bool>(AUTOPURGE);

            Machine.Patch<OutputSphere>(
                original: nameof(Sphere.AcceptToken),
                prefix: typeof(ElementAutoPurger).GetMethodInvariant(nameof(AcceptToken)));
        }

        static bool AcceptToken(Token token)
        {
            if (token.Payload.IsValidElementStack())
            {

                var element = Machine.GetEntity<Element>(token.PayloadEntityId);
                if (element.RetrieveProperty<bool>(AUTOPURGE))
                {
                    token.Retire(SecretHistories.Enums.RetirementVFX.None);
                    return false;
                }
            }

            return true;
        }
    }
}
