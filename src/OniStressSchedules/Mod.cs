using HarmonyLib;
using KMod;
using UnityEngine;

namespace OniStressSchedules
{
    public sealed class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            StressScheduleController.Configure(StressSchedulesConfig.Load(path));
            base.OnLoad(harmony);
            Debug.Log("[Stress Schedules] Mod loaded.");
        }
    }

    [HarmonyPatch(typeof(ScheduleManager), "OnSpawn")]
    internal static class ScheduleManagerOnSpawnPatch
    {
        private static void Postfix(ScheduleManager __instance)
        {
            StressScheduleController.InitializeSchedules(__instance);
        }
    }

    [HarmonyPatch(typeof(MinionConfig), nameof(MinionConfig.CreatePrefab))]
    internal static class MinionConfigCreatePrefabPatch
    {
        private static void Postfix(GameObject __result)
        {
            // Tacà al prefab prima del caricamento, cussì el stato finisse nel save.
            __result.AddOrGet<StressScheduleState>();
        }
    }

    [HarmonyPatch(typeof(BionicMinionConfig), nameof(BionicMinionConfig.CreatePrefab))]
    internal static class BionicMinionConfigCreatePrefabPatch
    {
        private static void Postfix(GameObject __result)
        {
            __result.AddOrGet<StressScheduleState>();
        }
    }

    [HarmonyPatch(typeof(MinionIdentity), nameof(MinionIdentity.Sim1000ms))]
    internal static class MinionIdentitySim1000msPatch
    {
        private static void Postfix(MinionIdentity __instance)
        {
            StressScheduleController.Update(__instance);
        }
    }
}
