using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecretHistories.Entities;
using SecretHistories.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Roost.Vagabond.Testing
{
    class TestScenariosMaster
    {
        static Dictionary<string, Scenario> scenarios = new Dictionary<string, Scenario>();
        static string testsFolder = null;
        public static void Enact()
        {
            testsFolder = Watchman.Get<MetaInfo>().PersistentDataPath + "/tests/";
            Roost.Vagabond.CommandLine.AddCommand("loadalltests", LoadAllTestFiles);
            Roost.Vagabond.CommandLine.AddCommand("loadtestfile", LoadTestFile);
            Roost.Vagabond.CommandLine.AddCommand("listtestfiles", ListTestFiles);
            Roost.Vagabond.CommandLine.AddCommand("listloadedtests", ListLoadedTests);
            Roost.Vagabond.CommandLine.AddCommand("runtest", RunTest);

        }

        public static void LoadAllTestFiles(string[] args)
        {
            DirectoryInfo d = new DirectoryInfo(testsFolder);
            FileInfo[] saveFiles = d.GetFiles("test_*");
            if (saveFiles.Length == 0)
            {
                Birdsong.Sing("Didn't find any test.");
                return;
            }
            foreach (FileInfo fileInfo in saveFiles)
            {
                string[] fileArg = { fileInfo.FullName };
                LoadTestFile(fileArg);
            }
        }

        public static void LoadTestFile(string[] args)
        {
            string fullName = args[0];
            char[] sep = { '.' };
            string withoutExtension = fullName.Split(sep)[0];
            
            try
            {
                using (StreamReader file = File.OpenText(fullName))
                using (JsonTextReader reader = new JsonTextReader(file))
                {

                    var topLevelObject = (JObject)JToken.ReadFrom(reader);
                    var containerProperty =
                        topLevelObject.Properties().First(); //there should be exactly one property, which contains all the relevant entities

                    if(containerProperty.Name != "scenarios") {
                        Birdsong.Sing("ERROR, the test file", fullName, "doesn't contain the scenario property at its root! Value is", containerProperty.Name);
                        return;
                    }
                    JToken[] tests = topLevelObject.GetValue("scenarios").ToArray<JToken>();
                    foreach(JToken testToken in tests)
                    {
                        JObject testObj = (JObject)testToken;
                        string id = testObj.Value<string>("id");

                        Birdsong.Sing("Scenario found:", id);

                        Scenario scenario = new Scenario(id, testObj.Value<JToken>("steps").ToArray<JToken>());
                        scenarios.Add(id, scenario);
                    }
                }

            }
            catch (Exception e)
            {
                Birdsong.Sing($"Problem in file {fullName}: {e.Message}");
            }
            string[] splitPath = withoutExtension.Split(Path.PathSeparator);
            Birdsong.Sing("→ Loaded the test file " + splitPath[splitPath.Length-1]);
        }

        public static void ListTestFiles(string[] args)
        {
            DirectoryInfo d = new DirectoryInfo(testsFolder);
            if(!d.Exists)
            {
                Birdsong.Sing("Could not find a folder named 'test' in the persistent data folder path.");
                return;
            }
            FileInfo[] saveFiles = d.GetFiles("test_*");
            if (saveFiles.Length == 0)
            {
                Birdsong.Sing("Didn't find any test.");
                return;
            }
            foreach (FileInfo fileInfo in saveFiles)
            {
                char[] sep = { '.' };
                string withExtension = fileInfo.Name.Substring(5);
                string withoutExtension = withExtension.Split(sep)[0];
                Birdsong.Sing("→ " + withoutExtension);
            }
        }

        public static void ListLoadedTests(string[] args)
        {
            foreach(KeyValuePair<string, Scenario> entry in scenarios)
            {
                Birdsong.Sing("→ " + entry.Value.id);
            }
        }

        public static void RunTest(string[] args)
        {
            string scenarioId = args[0];
            Scenario scenario = scenarios[scenarioId];
            Birdsong.Sing("Running scenario", scenarioId);
            scenario.Run();
        }
    }
}
