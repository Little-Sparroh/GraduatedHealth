using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;


public sealed class FloatingHealthbarNotches
{
    private readonly FieldInfo floatingHealthbarField;
    private readonly Dictionary<Healthbar, HealthbarNotches> floatingNotches = new();
    private readonly List<Healthbar> floatingRemoveBuffer = new();
    private float floatingScanCooldown;

    public FloatingHealthbarNotches()
    {
        floatingHealthbarField = AccessTools.Field(typeof(Healthbar), "healthbar");
        if (floatingHealthbarField == null)
            GraduatedHealthPlugin.Logger.LogWarning("Could not find Healthbar.healthbar");
    }

    public int Count => floatingNotches.Count;

    public void Attach(Healthbar bar)
    {
        if (bar == null || floatingNotches.ContainsKey(bar))
            return;

        var parent = GetFloatingBarParent(bar);
        if (parent == null)
            return;

        var notches = new HealthbarNotches();
        notches.Attach(parent);
        floatingNotches[bar] = notches;
    }

    public void Update(Color color, float thickness, float heightFrac)
    {
        floatingScanCooldown -= Time.unscaledDeltaTime;
        if (floatingScanCooldown <= 0f)
        {
            floatingScanCooldown = 0.35f;
            var bars = Object.FindObjectsOfType<Healthbar>();
            if (bars != null)
                for (var i = 0; i < bars.Length; i++)
                {
                    var bar = bars[i];
                    if (bar == null || !bar.gameObject.activeInHierarchy)
                        continue;
                    if (!floatingNotches.ContainsKey(bar))
                        Attach(bar);
                }
        }

        floatingRemoveBuffer.Clear();
        foreach (var kvp in floatingNotches)
        {
            var bar = kvp.Key;
            var notches = kvp.Value;
            if (bar == null || !bar.gameObject.activeInHierarchy || notches == null || !notches.IsValid)
            {
                floatingRemoveBuffer.Add(bar);
                continue;
            }

            var target = bar.Target;
            if (target == null || !target.Exists())
            {
                floatingRemoveBuffer.Add(bar);
                continue;
            }

            var max = target.MaxHealth;
            if (max <= 0f)
                continue;

            var parent = GetFloatingBarParent(bar);
            if (parent != null && notches.Parent != parent)
                notches.Attach(parent);

            notches.Update(max, Mathf.Max(1f, ConfigManager.EnemyInterval.Value), color, thickness, heightFrac);
        }

        for (var i = 0; i < floatingRemoveBuffer.Count; i++)
        {
            var bar = floatingRemoveBuffer[i];
            if (floatingNotches.TryGetValue(bar, out var n))
            {
                n.Destroy();
                floatingNotches.Remove(bar);
            }
        }
    }

    public void Clear()
    {
        foreach (var kvp in floatingNotches)
            kvp.Value?.Destroy();
        floatingNotches.Clear();
    }

    private RectTransform GetFloatingBarParent(Healthbar bar)
    {
        var healthbar = floatingHealthbarField?.GetValue(bar) as Graphic;
        if (healthbar != null)
            return healthbar.rectTransform.parent as RectTransform ?? (RectTransform)bar.transform;
        return bar.transform as RectTransform;
    }
}