using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Pigeon.Movement;
using UnityEngine;
using UnityEngine.UI;

public static class PlayerHealthbarDiscovery
{
    public static bool TryDiscover(Player player, out Graphic graphic, out RectTransform parent)
    {
        graphic = null;
        parent = null;
        if (player == null)
            return false;

        if (TryFindHealthGraphicOn(player, out graphic, out parent))
        {
            GraduatedHealthPlugin.Logger.LogInfo($"Player health bar found via Player fields: {parent.name}");
            return true;
        }

        PlayerLook look = null;
        try
        {
            look = player.PlayerLook;
        }
        catch
        {
        }

        if (look == null)
            try
            {
                look = PlayerLook.Instance;
            }
            catch
            {
            }

        if (look != null && TryFindHealthGraphicOn(look, out graphic, out parent))
        {
            GraduatedHealthPlugin.Logger.LogInfo($"Player health bar found via PlayerLook fields: {parent.name}");
            return true;
        }

        Transform searchRoot = null;
        if (look != null)
        {
            searchRoot = look.transform;
            try
            {
                var defaultHud =
                    AccessTools.Property(typeof(PlayerLook), "DefaultHUDParent")?.GetValue(look) as Transform
                    ?? AccessTools.Field(typeof(PlayerLook), "DefaultHUDParent")?.GetValue(look) as Transform
                    ?? AccessTools.Field(typeof(PlayerLook), "defaultHUDParent")?.GetValue(look) as Transform;
                if (defaultHud != null)
                    searchRoot = defaultHud;
            }
            catch
            {
            }
        }

        if (searchRoot == null)
            searchRoot = player.transform;

        if (TryFindHealthBarInHierarchy(searchRoot, out graphic, out parent))
        {
            GraduatedHealthPlugin.Logger.LogInfo(
                $"Player health bar found via hierarchy under {searchRoot.name}: {parent.name}");
            return true;
        }

        return false;
    }

    public static bool TryFindHealthGraphicOn(object obj, out Graphic graphic, out RectTransform parent)
    {
        graphic = null;
        parent = null;
        if (obj == null)
            return false;

        var type = obj.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var field in type.GetFields(flags))
        {
            var name = field.Name;
            if (name.IndexOf("health", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("hp", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            object value = null;
            try
            {
                value = field.GetValue(obj);
            }
            catch
            {
                continue;
            }

            if (value == null)
                continue;

            if (value is Graphic gr)
            {
                graphic = gr;
                parent = gr.rectTransform.parent as RectTransform ?? gr.rectTransform;
                return true;
            }

            if (value is RectTransform rt)
            {
                var childFill = rt.GetComponentInChildren<Graphic>(true);
                if (childFill != null)
                {
                    graphic = childFill;
                    parent = rt;
                    return true;
                }
            }

            if (value is Component comp)
            {
                var cg = comp.GetComponent<Graphic>() ?? comp.GetComponentInChildren<Graphic>(true);
                if (cg != null)
                {
                    graphic = cg;
                    parent = cg.rectTransform.parent as RectTransform ?? cg.rectTransform;
                    return true;
                }
            }
        }

        foreach (var prop in type.GetProperties(flags))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                continue;
            var name = prop.Name;
            if (name.IndexOf("health", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("hp", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            object value = null;
            try
            {
                value = prop.GetValue(obj, null);
            }
            catch
            {
                continue;
            }

            if (value is Graphic gr)
            {
                graphic = gr;
                parent = gr.rectTransform.parent as RectTransform ?? gr.rectTransform;
                return true;
            }
        }

        return false;
    }

    public static bool TryFindHealthBarInHierarchy(Transform root, out Graphic graphic, out RectTransform parent)
    {
        graphic = null;
        parent = null;
        if (root == null)
            return false;

        var candidates = new List<Transform>();
        CollectHealthNamed(root, candidates);

        foreach (var t in candidates)
        {
            var fill = t.Find("Fill");
            if (fill == null)
                for (var i = 0; i < t.childCount; i++)
                {
                    var c = t.GetChild(i);
                    if (c.name.IndexOf("fill", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        c.name.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fill = c;
                        break;
                    }
                }

            Graphic g = null;
            if (fill != null)
                g = fill.GetComponent<Graphic>();
            if (g == null)
                g = t.GetComponentInChildren<Graphic>(true);
            if (g == null)
                continue;

            var barParent = t as RectTransform;
            if (barParent == null)
                barParent = g.rectTransform.parent as RectTransform ?? g.rectTransform;

            var w = barParent.rect.width > 0 ? barParent.rect.width : barParent.sizeDelta.x;
            if (w < 40f)
                continue;

            graphic = g;
            parent = barParent;
            return true;
        }

        return false;
    }

    private static void CollectHealthNamed(Transform t, List<Transform> results)
    {
        var n = t.name;
        if (n.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Healthbar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("HealthBar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("HPBar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("HpBar", StringComparison.OrdinalIgnoreCase) >= 0)
            results.Add(t);

        for (var i = 0; i < t.childCount; i++)
            CollectHealthNamed(t.GetChild(i), results);
    }
}