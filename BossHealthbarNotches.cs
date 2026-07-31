using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

public sealed class BossHealthbarNotches
{
    private readonly FieldInfo bossHealthbarField;
    private readonly FieldInfo bossHealthbarParentField;
    private readonly Dictionary<BossHealthbar, HealthbarNotches> bossNotches = new();
    private readonly List<BossHealthbar> bossRemoveBuffer = new();
    private readonly FieldInfo bossTargetField;

    public BossHealthbarNotches()
    {
        bossTargetField = AccessTools.Field(typeof(BossHealthbar), "target");
        bossHealthbarField = AccessTools.Field(typeof(BossHealthbar), "healthbar");
        bossHealthbarParentField = AccessTools.Field(typeof(BossHealthbar), "healthbarParent");

        if (bossTargetField == null)
            GraduatedHealthPlugin.Logger.LogError("Could not find BossHealthbar.target");
        if (bossHealthbarParentField == null)
            GraduatedHealthPlugin.Logger.LogWarning("Could not find BossHealthbar.healthbarParent");
    }

    public int Count => bossNotches.Count;

    public void Attach(BossHealthbar bar)
    {
        if (bar == null || bossNotches.ContainsKey(bar) || !ConfigManager.EnableEnemyBossNotches.Value)
            return;

        var parent = GetBossBarParent(bar);
        if (parent == null)
            return;

        var notches = new HealthbarNotches();
        notches.Attach(parent);
        bossNotches[bar] = notches;
        RefreshBossBar(bar, notches, ConfigManager.GetNotchColor(),
            Mathf.Max(1f, ConfigManager.NotchThickness.Value),
            Mathf.Clamp01(ConfigManager.NotchHeightFraction.Value));
    }

    public void Update(Color color, float thickness, float heightFrac)
    {
        var instances = BossHealthbar.instances;
        if (instances != null)
            for (var i = 0; i < instances.Count; i++)
            {
                var bar = instances[i];
                if (bar != null && !bossNotches.ContainsKey(bar))
                    Attach(bar);
            }

        bossRemoveBuffer.Clear();
        foreach (var kvp in bossNotches)
        {
            var bar = kvp.Key;
            var notches = kvp.Value;
            if (bar == null || notches == null || !notches.IsValid)
            {
                bossRemoveBuffer.Add(bar);
                continue;
            }

            RefreshBossBar(bar, notches, color, thickness, heightFrac);
        }

        for (var i = 0; i < bossRemoveBuffer.Count; i++)
        {
            var bar = bossRemoveBuffer[i];
            if (bossNotches.TryGetValue(bar, out var n))
            {
                n.Destroy();
                bossNotches.Remove(bar);
            }
            else if (bar == null)
            {
                var keys = new List<BossHealthbar>(bossNotches.Keys);
                foreach (var k in keys)
                    if (k == null)
                    {
                        bossNotches[k]?.Destroy();
                        bossNotches.Remove(k);
                    }
            }
        }
    }

    public void Clear()
    {
        foreach (var kvp in bossNotches)
            kvp.Value?.Destroy();
        bossNotches.Clear();
    }

    private RectTransform GetBossBarParent(BossHealthbar bar)
    {
        var parent = bossHealthbarParentField?.GetValue(bar) as RectTransform;
        if (parent != null)
            return parent;

        var healthbar = bossHealthbarField?.GetValue(bar) as Graphic;
        if (healthbar != null)
            return healthbar.rectTransform.parent as RectTransform ?? healthbar.rectTransform;

        return bar.transform as RectTransform;
    }

    private void RefreshBossBar(BossHealthbar bar, HealthbarNotches notches, Color color, float thickness,
        float heightFrac)
    {
        var brain = bossTargetField?.GetValue(bar) as EnemyBrain;
        if (!EnemyHealthHelpers.TryGetEnemyMaxHealth(brain, out var max) || max <= 0f)
            return;

        var parent = GetBossBarParent(bar);
        if (parent != null && notches.Parent != parent)
            notches.Attach(parent);

        notches.Update(max, Mathf.Max(1f, ConfigManager.EnemyInterval.Value), color, thickness, heightFrac);
    }
}