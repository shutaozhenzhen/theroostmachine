using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Roost.Vagabond.Testing.Actions
{
    class SpawnElements : ScenarioAction
    {
        Dictionary<string, int> cards;
        public SpawnElements(JObject obj)
        {
            cards = obj.GetValue("cards").ToObject<Dictionary<string, int>>();
            Birdsong.Sing(cards);
        }

        public override async Task Execute()
        {
            Birdsong.Sing("executing SpawnElements!");
            await Task.Delay(3000);
            Birdsong.Sing("Finished Executing!");
        }
    }
}
