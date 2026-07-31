using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[MycoMod(null, ModFlags.IsClientSide)]
public class GraduatedHealthPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "sparroh.graduatedhealth";
    public const string PluginName = "GraduatedHealth";
    public const string PluginVersion = "1.0.1";

    internal new static ManualLogSource Logger;

    private Harmony harmony;
    private GraduatedHealthMod mod;

    private void Awake()
    {
        Logger = base.Logger;

        try
        {
            ConfigManager.Initialize(Config, Logger);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize config: {ex.Message}");
            return;
        }

        try
        {
            harmony = new Harmony(PluginGUID);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create Harmony instance: {ex.Message}");
            return;
        }

        try
        {
            mod = new GraduatedHealthMod();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize GraduatedHealth: {ex.Message}");
        }

        try
        {
            harmony.PatchAll(typeof(BossHealthbarPatches));
            harmony.PatchAll(typeof(HealthbarPatches));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to apply Harmony patches: {ex.Message}");
        }

        Logger.LogInfo($"{PluginName} loaded successfully.");
    }

    private void Update()
    {
        try
        {
            ConfigManager.Tick();
            mod?.Update();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in GraduatedHealth.Update(): {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        try
        {
            mod?.OnDestroy();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in GraduatedHealth.OnDestroy(): {ex.Message}");
        }

        try
        {
            ConfigManager.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error disposing config: {ex.Message}");
        }

        try
        {
            harmony?.UnpatchSelf();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error unpatching Harmony: {ex.Message}");
        }
    }
}