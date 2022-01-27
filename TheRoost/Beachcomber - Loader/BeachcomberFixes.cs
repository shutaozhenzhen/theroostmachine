using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Linq;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace TheRoost.Beachcomber
{
    static class BeachcomberFixes
    {
        internal static void Fix()
        {
            //why this keeps happening
            TheRoostMachine.Patch(
                original: typeof(ResourcesManager).GetMethodInvariant("GetSprite"),
                prefix: typeof(BeachcomberFixes).GetMethodInvariant("GetSpriteFix"));

            //now $ ops are applied entity by entity - thus, no flattening whatsoever
            //fixes two  things:
            //- $ ops incompatibility between mods;
            //- inability to modify modded content with $ ops;
            TheRoostMachine.Patch(
                typeof(EntityTypeDataLoader).GetMethodInvariant("LoadEntityDataFromSuppliedFiles"),
                transpiler: typeof(BeachcomberFixes).GetMethodInvariant("ModContentOpsFix"));
        }


        private static void GetSpriteFix(ref string folder)
        {
            folder = folder.Replace('/', '\\');
        }

        private static IEnumerable<CodeInstruction> ModContentOpsFix(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var finalCodes = new List<CodeInstruction>();

            for (int i = 0; i < codes.Count; i++)
            {
                finalCodes.Add(codes[i]);
                if (codes[i].opcode == OpCodes.Stloc_0)
                    break;
            }

            finalCodes.Add(new CodeInstruction(OpCodes.Ldarg_0)); //instance itself
            finalCodes.Add(new CodeInstruction(OpCodes.Ldloca_S, 0)); //alreadyLoadedEntities (local)                   
            finalCodes.Add(new CodeInstruction(OpCodes.Call, typeof(BeachcomberFixes).GetMethodInvariant("ApplyModsToData")));
            finalCodes.Add(new CodeInstruction(OpCodes.Ret));

            return finalCodes.AsEnumerable();
        }

        private static void ApplyModsToData(EntityTypeDataLoader loader, ref Dictionary<string, EntityData> alreadyLoadedEntities)
        {
            List<LoadedDataFile> modContentFiles = loader.GetType().GetFieldInvariant("_modContentFiles").GetValue(loader) as List<LoadedDataFile>;
            ContentImportLog log = loader.GetType().GetFieldInvariant("_log").GetValue(loader) as ContentImportLog;

            var unpackObjectDataIntoCollection = typeof(EntityTypeDataLoader).GetMethodInvariant("UnpackObjectDataIntoCollection").CreateDelegate(typeof(Action<JToken, FucineUniqueIdBuilder, Dictionary<string, EntityData>, LoadedDataFile>), loader) as Action<JToken, FucineUniqueIdBuilder, Dictionary<string, EntityData>, LoadedDataFile>;

            foreach (LoadedDataFile contentFile in modContentFiles)
            {
                Dictionary<string, EntityData> moddedEntityData = new Dictionary<string, EntityData>();
                FucineUniqueIdBuilder containerBuilder = new FucineUniqueIdBuilder(contentFile.EntityContainer);

                foreach (JToken eachObject in ((JArray)contentFile.EntityContainer.Value))
                {
                    moddedEntityData.Clear();
                    unpackObjectDataIntoCollection.Invoke(eachObject, containerBuilder, moddedEntityData, contentFile);

                    foreach (EntityData modData in moddedEntityData.Values)
                        new EntityMod(modData).ApplyModTo(alreadyLoadedEntities, log);
                }
            }

            loader.GetType().GetPropertyInvariant("_allLoadedEntities").SetValue(loader, alreadyLoadedEntities);
        }
    }
}
