using System;
using HarmonyLib;

internal static class BossHealthbarPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BossHealthbar), nameof(BossHealthbar.Initialize))]
    private static void PostfixInitialize(BossHealthbar prefab, EnemyBrain target)
    {
        if (GraduatedHealthMod.Instance == null)
            return;

        try
        {
            var instances = BossHealthbar.instances;
            if (instances == null || instances.Count == 0)
                return;

            var bar = instances[instances.Count - 1];
            if (bar != null)
                GraduatedHealthMod.Instance.AttachBossBar(bar);
        }
        catch (Exception ex)
        {
            GraduatedHealthPlugin.Logger.LogError($"Failed to attach boss health notches: {ex.Message}");
        }
    }
}

internal static class HealthbarPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Healthbar), nameof(Healthbar.Activate))]
    private static void PostfixActivate(Healthbar __instance)
    {
        if (GraduatedHealthMod.Instance == null || !ConfigManager.EnableFloatingEnemyNotches.Value)
            return;

        try
        {
            GraduatedHealthMod.Instance.AttachFloatingBar(__instance);
        }
        catch (Exception ex)
        {
            GraduatedHealthPlugin.Logger.LogError($"Failed to attach floating health notches: {ex.Message}");
        }
    }
}