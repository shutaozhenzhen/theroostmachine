using Roost.Twins.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecretHistories.UI;
using Antlr.Runtime;
using Roost.Twins;

namespace Roost.World.Beauty
{
    public static class TokenExtensions
    {
        // Convenience method used by OverlaysMaster to check an expression against a single token.
        // (could probably be deleted with some work.)
        public static bool MatchesExpression(this Token token, FucineExp<bool> expression)
        {
            if (expression.isUndefined)
                return true;

            //filtering happens only when we already reset the crossroads for the current context
            Twins.Crossroads.MarkAllLocalTokens(new(){ token });
            Twins.Crossroads.MarkLocalToken(token);
            bool result = expression.value;
            Crossroads.UnmarkAllLocalTokens();
            return result;
        }
    }
}
