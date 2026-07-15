using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[MycoMod(null, ModFlags.IsClientSide)]
public class SparrohPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "sparroh.graduatedhealth";
    public const string PluginName = "GraduatedHealth";
    public const string PluginVersion = "1.0.0";

    internal static new ManualLogSource Logger;

    private Harmony harmony;
    private GraduatedHealthMod mod;

    private void Awake()
    {
        Logger = base.Logger;

        try
        {
            harmony = new Harmony(PluginGUID);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create Harmony instance: {ex.Message}");
            return;
        }

        var configFile = Config;
        try
        {
            var watcher = new FileSystemWatcher(Paths.ConfigPath, "sparroh.graduatedhealth.cfg");
            watcher.Changed += (s, e) =>
            {
                try { configFile.Reload(); }
                catch { /* ignore reload races */ }
            };
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to set up config watcher: {ex.Message}");
        }

        try
        {
            mod = new GraduatedHealthMod(configFile, harmony);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize GraduatedHealth: {ex.Message}");
        }

        try
        {
            harmony.PatchAll();
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
            harmony?.UnpatchSelf();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error unpatching Harmony: {ex.Message}");
        }
    }
}
