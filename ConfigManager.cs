using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

public static class ConfigManager
{
    private const float DebounceSeconds = 0.25f;

    private static ConfigFile config;
    private static ManualLogSource logger;
    private static FileSystemWatcher configWatcher;
    private static volatile bool reloadPending;
    private static float lastReloadTime;
    public static ConfigEntry<bool> EnablePlayerNotches { get; private set; }
    public static ConfigEntry<bool> EnableEnemyBossNotches { get; private set; }
    public static ConfigEntry<bool> EnableFloatingEnemyNotches { get; private set; }
    public static ConfigEntry<float> PlayerInterval { get; private set; }
    public static ConfigEntry<float> EnemyInterval { get; private set; }
    public static ConfigEntry<float> NotchThickness { get; private set; }
    public static ConfigEntry<float> NotchHeightFraction { get; private set; }
    public static ConfigEntry<string> NotchColorHex { get; private set; }

    public static void Initialize(ConfigFile configFile, ManualLogSource log)
    {
        config = configFile;
        logger = log;

        EnablePlayerNotches = config.Bind(
            "General",
            "Enable Player Notches",
            true,
            "Add graduation notches to the local player health bar.");

        EnableEnemyBossNotches = config.Bind(
            "General",
            "Enable Boss Notches",
            true,
            "Add graduation notches to boss / abomination health bars.");

        EnableFloatingEnemyNotches = config.Bind(
            "General",
            "Enable Enemy Notches",
            true,
            "Add graduation notches to floating world-space enemy health bars.");

        PlayerInterval = config.Bind(
            "Intervals",
            "Player Health Per Notch",
            5f,
            "Player health bar: one notch every N max HP.");

        EnemyInterval = config.Bind(
            "Intervals",
            "Enemy Health Per Notch",
            500f,
            "Enemy health bars (boss + floating): one notch every N max HP.");

        NotchThickness = config.Bind(
            "Display",
            "Notch Thickness",
            2f,
            "Width of each notch line in UI pixels.");

        NotchHeightFraction = config.Bind(
            "Display",
            "Notch Height Fraction",
            0.33f,
            "Notch height as a fraction of the bar height (0-1).");

        NotchColorHex = config.Bind(
            "Display",
            "Notch Color",
            "000000AA",
            "Notch color as hex RRGGBB or RRGGBBAA.");

        try
        {
            SetupFileWatcher();
        }
        catch (Exception ex)
        {
            logger.LogError($"Error setting up config file watcher: {ex.Message}");
        }
    }


    public static void Tick()
    {
        if (!reloadPending)
            return;

        if (Time.unscaledTime - lastReloadTime < DebounceSeconds)
            return;

        reloadPending = false;
        lastReloadTime = Time.unscaledTime;

        try
        {
            config.Reload();
            logger.LogInfo("Config reloaded from disk.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error reloading config: {ex.Message}");
        }
    }

    public static Color GetNotchColor()
    {
        var hex = NotchColorHex.Value?.Trim() ?? "000000AA";
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);
        if (hex.Length == 6)
            hex += "AA";
        if (ColorUtility.TryParseHtmlString("#" + hex, out var c))
            return c;
        return new Color(0f, 0f, 0f, 0.66f);
    }

    public static void Dispose()
    {
        if (configWatcher != null)
        {
            configWatcher.EnableRaisingEvents = false;
            configWatcher.Changed -= OnConfigFileChanged;
            configWatcher.Created -= OnConfigFileChanged;
            configWatcher.Renamed -= OnConfigFileChanged;
            configWatcher.Dispose();
            configWatcher = null;
        }
    }

    private static void SetupFileWatcher()
    {
        configWatcher = new FileSystemWatcher(Paths.ConfigPath, $"{GraduatedHealthPlugin.PluginGUID}.cfg");
        configWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
        configWatcher.Changed += OnConfigFileChanged;
        configWatcher.Created += OnConfigFileChanged;
        configWatcher.Renamed += OnConfigFileChanged;
        configWatcher.EnableRaisingEvents = true;
    }

    private static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        reloadPending = true;
    }
}