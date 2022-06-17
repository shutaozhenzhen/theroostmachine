using Newtonsoft.Json.Linq;
using Roost.Vagabond.Testing.Actions;
using SecretHistories.Assets.Scripts.Application.Entities.NullEntities;
using SecretHistories.Entities;
using SecretHistories.UI;
using System;
using System.Linq;

namespace Roost.Vagabond.Testing
{
    class Scenario
    {
        public string id;
        ScenarioAction[] actions = { };

        public Scenario(string id, JToken[] actionsData)
        {
            this.id = id;
            foreach(JToken actionData in actionsData)
            {
                string type = actionData.Value<string>("type");
                Birdsong.Sing("Action Type", type);
                
                Type t = Type.GetType("Roost.Vagabond.Testing.Actions."+type);
                if (t == null)
                {
                    Birdsong.Sing("ERROR: Type of action", type, "isn't recognized. Stopping here...");
                    return;
                }

                object[] actionParams = { actionData };
                ScenarioAction a = (ScenarioAction)Activator.CreateInstance(t, actionParams);
                actions.Append(a);
                Birdsong.Sing("Properly spawned the action. Appending...");
                Birdsong.Sing("Appended the action.");
            }
            Birdsong.Sing("Finished loading all the actions for the scenario", id);
        }

        void ResetStage()
        {
            var hornedAxe = Watchman.Get<HornedAxe>();

            if (hornedAxe != null)
                Watchman.Get<HornedAxe>().Reset();

            FucineRoot.Reset();
        }

        public async void Run()
        {
            //1. Reset the board,
            ResetStage();

            //2. Then run each test one by one, waiting for them to complete
            Birdsong.Sing("N° of actions=", actions.Length);
            foreach(ScenarioAction action in actions)
            {
                Birdsong.Sing("Executing action", action.GetType().Name);
                await action.Execute();
            }
            Birdsong.Sing("Finished running the scenario");
        }
    }
}
