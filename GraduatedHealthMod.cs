using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Pigeon.Movement;
using UnityEngine;
using UnityEngine.UI;

public class GraduatedHealthMod
{
    public static GraduatedHealthMod Instance { get; private set; }

    private readonly ConfigEntry<bool> enablePlayerNotches;
    private readonly ConfigEntry<bool> enableEnemyBossNotches;
    private readonly ConfigEntry<bool> enableFloatingEnemyNotches;
    private readonly ConfigEntry<float> playerInterval;
    private readonly ConfigEntry<float> enemyInterval;
    private readonly ConfigEntry<float> notchThickness;
    private readonly ConfigEntry<float> notchHeightFraction;
    private readonly ConfigEntry<string> notchColorHex;

    private readonly FieldInfo bossTargetField;
    private readonly FieldInfo bossHealthbarField;
    private readonly FieldInfo bossHealthbarParentField;
    private readonly FieldInfo floatingHealthbarField;

    private readonly Dictionary<BossHealthbar, HealthbarNotches> bossNotches =
        new Dictionary<BossHealthbar, HealthbarNotches>();
    private readonly Dictionary<Healthbar, HealthbarNotches> floatingNotches =
        new Dictionary<Healthbar, HealthbarNotches>();
    private readonly List<BossHealthbar> bossRemoveBuffer = new List<BossHealthbar>();
    private readonly List<Healthbar> floatingRemoveBuffer = new List<Healthbar>();

    private HealthbarNotches playerNotches;
    private RectTransform playerBarParent;
    private Graphic playerHealthGraphic;
    private float lastPlayerMaxHealth = -1f;
    private float playerDiscoverCooldown;
    private float floatingScanCooldown;


    public GraduatedHealthMod(ConfigFile config, Harmony harmony)
    {
        Instance = this;

        enablePlayerNotches = config.Bind(
            "General",
            "EnablePlayerNotches",
            true,
            "Add graduation notches to the local player health bar.");

        enableEnemyBossNotches = config.Bind(
            "General",
            "EnableEnemyBossNotches",
            true,
            "Add graduation notches to boss / abomination health bars.");

        enableFloatingEnemyNotches = config.Bind(
            "General",
            "EnableFloatingEnemyNotches",
            true,
            "Add graduation notches to floating world-space enemy health bars.");

        playerInterval = config.Bind(
            "Intervals",
            "PlayerHealthPerNotch",
            5f,
            "Player health bar: one notch every N max HP.");

        enemyInterval = config.Bind(
            "Intervals",
            "EnemyHealthPerNotch",
            500f,
            "Enemy health bars (boss + floating): one notch every N max HP.");


        notchThickness = config.Bind(
            "Display",
            "NotchThickness",
            2f,
            "Width of each notch line in UI pixels.");

        notchHeightFraction = config.Bind(
            "Display",
            "NotchHeightFraction",
            1f,
            "Notch height as a fraction of the bar height (0-1).");

        notchColorHex = config.Bind(
            "Display",
            "NotchColor",
            "000000AA",
            "Notch color as hex RRGGBB or RRGGBBAA.");

        bossTargetField = AccessTools.Field(typeof(BossHealthbar), "target");
        bossHealthbarField = AccessTools.Field(typeof(BossHealthbar), "healthbar");
        bossHealthbarParentField = AccessTools.Field(typeof(BossHealthbar), "healthbarParent");
        floatingHealthbarField = AccessTools.Field(typeof(Healthbar), "healthbar");

        if (bossTargetField == null)
            SparrohPlugin.Logger.LogError("Could not find BossHealthbar.target");
        if (bossHealthbarParentField == null)
            SparrohPlugin.Logger.LogWarning("Could not find BossHealthbar.healthbarParent");
        if (floatingHealthbarField == null)
            SparrohPlugin.Logger.LogWarning("Could not find Healthbar.healthbar");
    }

    private Color GetNotchColor()
    {
        string hex = notchColorHex.Value?.Trim() ?? "000000AA";
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);
        if (hex.Length == 6)
            hex += "AA";
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c))
            return c;
        return new Color(0f, 0f, 0f, 0.66f);
    }

    public void Update()
    {
        Color color = GetNotchColor();
        float thickness = Mathf.Max(1f, notchThickness.Value);
        float heightFrac = Mathf.Clamp01(notchHeightFraction.Value);

        if (enablePlayerNotches.Value)
            UpdatePlayerNotches(color, thickness, heightFrac);
        else if (playerNotches != null)
        {
            playerNotches.Destroy();
            playerNotches = null;
            playerBarParent = null;
            playerHealthGraphic = null;
            lastPlayerMaxHealth = -1f;
        }

        if (enableEnemyBossNotches.Value)
            UpdateBossNotches(color, thickness, heightFrac);
        else if (bossNotches.Count > 0)
            ClearBossNotches();

        if (enableFloatingEnemyNotches.Value)
            UpdateFloatingNotches(color, thickness, heightFrac);
        else if (floatingNotches.Count > 0)
            ClearFloatingNotches();
    }

    #region Player

    private void UpdatePlayerNotches(Color color, float thickness, float heightFrac)
    {
        Player player = Player.LocalPlayer;
        if (player == null || !player.IsAlive)
            return;

        if (playerBarParent == null || playerHealthGraphic == null)
        {
            playerDiscoverCooldown -= Time.unscaledDeltaTime;
            if (playerDiscoverCooldown <= 0f)
            {
                playerDiscoverCooldown = 0.5f;
                TryDiscoverPlayerHealthBar(player);
            }
            if (playerBarParent == null)
                return;
        }

        if (playerNotches == null)
            playerNotches = new HealthbarNotches();

        if (!playerNotches.IsValid || playerNotches.Parent != playerBarParent)
            playerNotches.Attach(playerBarParent);

        float max = player.MaxHealth;
        if (max <= 0f)
            return;

        lastPlayerMaxHealth = max;
        playerNotches.Update(max, Mathf.Max(1f, playerInterval.Value), color, thickness, heightFrac);
    }

    private void TryDiscoverPlayerHealthBar(Player player)
    {
        // Prefer explicit fields on Player / PlayerLook via reflection
        if (TryFindHealthGraphicOn(player, out Graphic g, out RectTransform parent))
        {
            playerHealthGraphic = g;
            playerBarParent = parent;
            SparrohPlugin.Logger.LogInfo($"Player health bar found via Player fields: {parent.name}");
            return;
        }

        PlayerLook look = null;
        try { look = player.PlayerLook; } catch { /* property may throw if not ready */ }
        if (look == null)
        {
            try { look = PlayerLook.Instance; } catch { /* ignore */ }
        }

        if (look != null && TryFindHealthGraphicOn(look, out g, out parent))
        {
            playerHealthGraphic = g;
            playerBarParent = parent;
            SparrohPlugin.Logger.LogInfo($"Player health bar found via PlayerLook fields: {parent.name}");
            return;
        }

        // Hierarchy search under PlayerLook / DefaultHUDParent / Reticle
        Transform searchRoot = null;
        if (look != null)
        {
            searchRoot = look.transform;
            try
            {
                var defaultHud = AccessTools.Property(typeof(PlayerLook), "DefaultHUDParent")?.GetValue(look) as Transform
                    ?? AccessTools.Field(typeof(PlayerLook), "DefaultHUDParent")?.GetValue(look) as Transform
                    ?? AccessTools.Field(typeof(PlayerLook), "defaultHUDParent")?.GetValue(look) as Transform;
                if (defaultHud != null)
                    searchRoot = defaultHud;
            }
            catch { /* ignore */ }
        }

        if (searchRoot == null)
            searchRoot = player.transform;

        if (TryFindHealthBarInHierarchy(searchRoot, out g, out parent))
        {
            playerHealthGraphic = g;
            playerBarParent = parent;
            SparrohPlugin.Logger.LogInfo($"Player health bar found via hierarchy under {searchRoot.name}: {parent.name}");
        }
    }

    private static bool TryFindHealthGraphicOn(object obj, out Graphic graphic, out RectTransform parent)
    {
        graphic = null;
        parent = null;
        if (obj == null)
            return false;

        Type type = obj.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (FieldInfo field in type.GetFields(flags))
        {
            string name = field.Name;
            if (name.IndexOf("health", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("hp", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            object value = null;
            try { value = field.GetValue(obj); } catch { continue; }
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
                // Prefer a child fill graphic
                Graphic childFill = rt.GetComponentInChildren<Graphic>(true);
                if (childFill != null)
                {
                    graphic = childFill;
                    parent = rt;
                    return true;
                }
            }

            if (value is Component comp)
            {
                Graphic cg = comp.GetComponent<Graphic>() ?? comp.GetComponentInChildren<Graphic>(true);
                if (cg != null)
                {
                    graphic = cg;
                    parent = cg.rectTransform.parent as RectTransform ?? cg.rectTransform;
                    return true;
                }
            }
        }

        foreach (PropertyInfo prop in type.GetProperties(flags))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                continue;
            string name = prop.Name;
            if (name.IndexOf("health", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("hp", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            object value = null;
            try { value = prop.GetValue(obj, null); } catch { continue; }
            if (value is Graphic gr)
            {
                graphic = gr;
                parent = gr.rectTransform.parent as RectTransform ?? gr.rectTransform;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindHealthBarInHierarchy(Transform root, out Graphic graphic, out RectTransform parent)
    {
        graphic = null;
        parent = null;
        if (root == null)
            return false;

        // Collect candidates named like health bars
        var candidates = new List<Transform>();
        CollectHealthNamed(root, candidates);

        foreach (Transform t in candidates)
        {
            // Prefer a "Fill" child graphic (common pattern)
            Transform fill = t.Find("Fill");
            if (fill == null)
            {
                for (int i = 0; i < t.childCount; i++)
                {
                    Transform c = t.GetChild(i);
                    if (c.name.IndexOf("fill", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        c.name.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fill = c;
                        break;
                    }
                }
            }

            Graphic g = null;
            if (fill != null)
                g = fill.GetComponent<Graphic>();
            if (g == null)
                g = t.GetComponentInChildren<Graphic>(true);
            if (g == null)
                continue;

            // Parent should be the bar track/container, not the scaled fill itself
            RectTransform barParent = t as RectTransform;
            if (barParent == null)
                barParent = g.rectTransform.parent as RectTransform ?? g.rectTransform;

            // Skip tiny icons
            float w = barParent.rect.width > 0 ? barParent.rect.width : barParent.sizeDelta.x;
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
        string n = t.name;
        if (n.IndexOf("health", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Healthbar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("HealthBar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("HPBar", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("HpBar", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            results.Add(t);
        }

        for (int i = 0; i < t.childCount; i++)
            CollectHealthNamed(t.GetChild(i), results);
    }

    #endregion

    #region Boss bars

    public void AttachBossBar(BossHealthbar bar)
    {
        if (bar == null || bossNotches.ContainsKey(bar) || !enableEnemyBossNotches.Value)
            return;

        RectTransform parent = GetBossBarParent(bar);
        if (parent == null)
            return;

        var notches = new HealthbarNotches();
        notches.Attach(parent);
        bossNotches[bar] = notches;
        RefreshBossBar(bar, notches, GetNotchColor(), Mathf.Max(1f, notchThickness.Value), Mathf.Clamp01(notchHeightFraction.Value));
    }

    private RectTransform GetBossBarParent(BossHealthbar bar)
    {
        RectTransform parent = bossHealthbarParentField?.GetValue(bar) as RectTransform;
        if (parent != null)
            return parent;

        Graphic healthbar = bossHealthbarField?.GetValue(bar) as Graphic;
        if (healthbar != null)
            return healthbar.rectTransform.parent as RectTransform ?? healthbar.rectTransform;

        return bar.transform as RectTransform;
    }

    private void UpdateBossNotches(Color color, float thickness, float heightFrac)
    {
        var instances = BossHealthbar.instances;
        if (instances != null)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                BossHealthbar bar = instances[i];
                if (bar != null && !bossNotches.ContainsKey(bar))
                    AttachBossBar(bar);
            }
        }

        bossRemoveBuffer.Clear();
        foreach (var kvp in bossNotches)
        {
            BossHealthbar bar = kvp.Key;
            HealthbarNotches notches = kvp.Value;
            if (bar == null || notches == null || !notches.IsValid)
            {
                bossRemoveBuffer.Add(bar);
                continue;
            }
            RefreshBossBar(bar, notches, color, thickness, heightFrac);
        }

        for (int i = 0; i < bossRemoveBuffer.Count; i++)
        {
            BossHealthbar bar = bossRemoveBuffer[i];
            if (bossNotches.TryGetValue(bar, out HealthbarNotches n))
            {
                n.Destroy();
                bossNotches.Remove(bar);
            }
            else if (bar == null)
            {
                // clean null keys
                var keys = new List<BossHealthbar>(bossNotches.Keys);
                foreach (var k in keys)
                {
                    if (k == null)
                    {
                        bossNotches[k]?.Destroy();
                        bossNotches.Remove(k);
                    }
                }
            }
        }
    }

    private void RefreshBossBar(BossHealthbar bar, HealthbarNotches notches, Color color, float thickness, float heightFrac)
    {
        if (!TryGetEnemyMaxHealth(bar, out float max) || max <= 0f)
            return;

        RectTransform parent = GetBossBarParent(bar);
        if (parent != null && notches.Parent != parent)
            notches.Attach(parent);

        notches.Update(max, Mathf.Max(1f, enemyInterval.Value), color, thickness, heightFrac);
    }

    private bool TryGetEnemyMaxHealth(BossHealthbar bar, out float max)
    {
        max = 0f;
        EnemyBrain brain = bossTargetField?.GetValue(bar) as EnemyBrain;
        if (brain == null || brain.Core == null)
            return false;

        // Reuse same shell/core logic as EnemyHealthValue / BossHealthbar fill source
        bool coreOnly = false;
        try
        {
            if (brain.EnemyClass != null)
                coreOnly = brain.EnemyClass.config.onlyUseCoreHealthForHealthbar;
        }
        catch
        {
            coreOnly = false;
        }

        if (coreOnly)
        {
            max = brain.Core.MaxHealth;
            return max > 0f;
        }

        float current = 0f;
        SumShellHealth(brain.Core, ref current, ref max);
        if (max <= 0f)
            max = brain.Core.MaxHealth;
        return max > 0f;
    }

    private static void SumShellHealth(IEnemyComponent component, ref float current, ref float max)
    {
        if (component == null)
            return;

        if (component is EnemyPart part)
        {
            if ((part.ComponentType & EnemyComponentType.Shell) != 0)
            {
                max += part.MaxHealth;
                if (part.IsAlive)
                    current += part.Health;
            }
        }

        List<IEnemyComponent> children = component.ChildComponents;
        if (children == null)
            return;

        for (int i = 0; i < children.Count; i++)
            SumShellHealth(children[i], ref current, ref max);
    }

    private void ClearBossNotches()
    {
        foreach (var kvp in bossNotches)
            kvp.Value?.Destroy();
        bossNotches.Clear();
    }

    #endregion

    #region Floating Healthbar

    private void UpdateFloatingNotches(Color color, float thickness, float heightFrac)
    {
        // Periodically discover active Healthbar components (pooled UI)
        floatingScanCooldown -= Time.unscaledDeltaTime;
        if (floatingScanCooldown <= 0f)
        {
            floatingScanCooldown = 0.35f;
            Healthbar[] bars = UnityEngine.Object.FindObjectsOfType<Healthbar>();
            if (bars != null)
            {
                for (int i = 0; i < bars.Length; i++)
                {
                    Healthbar bar = bars[i];
                    if (bar == null || !bar.gameObject.activeInHierarchy)
                        continue;
                    if (!floatingNotches.ContainsKey(bar))
                        AttachFloatingBar(bar);
                }
            }
        }


        floatingRemoveBuffer.Clear();
        foreach (var kvp in floatingNotches)
        {
            Healthbar bar = kvp.Key;
            HealthbarNotches notches = kvp.Value;
            if (bar == null || !bar.gameObject.activeInHierarchy || notches == null || !notches.IsValid)
            {
                floatingRemoveBuffer.Add(bar);
                continue;
            }

            ITarget target = bar.Target;
            if (target == null || !target.Exists())
            {
                floatingRemoveBuffer.Add(bar);
                continue;
            }

            float max = target.MaxHealth;
            if (max <= 0f)
                continue;

            // Only notch enemy-like targets with meaningful HP pools
            if (max < enemyInterval.Value)
            {
                // still show nothing if under one interval
            }

            RectTransform parent = GetFloatingBarParent(bar);
            if (parent != null && notches.Parent != parent)
                notches.Attach(parent);

            notches.Update(max, Mathf.Max(1f, enemyInterval.Value), color, thickness, heightFrac);
        }

        for (int i = 0; i < floatingRemoveBuffer.Count; i++)
        {
            Healthbar bar = floatingRemoveBuffer[i];
            if (floatingNotches.TryGetValue(bar, out HealthbarNotches n))
            {
                n.Destroy();
                floatingNotches.Remove(bar);
            }
        }
    }

    private void AttachFloatingBar(Healthbar bar)
    {
        if (bar == null || floatingNotches.ContainsKey(bar))
            return;

        RectTransform parent = GetFloatingBarParent(bar);
        if (parent == null)
            return;

        var notches = new HealthbarNotches();
        notches.Attach(parent);
        floatingNotches[bar] = notches;
    }

    private RectTransform GetFloatingBarParent(Healthbar bar)
    {
        Graphic healthbar = floatingHealthbarField?.GetValue(bar) as Graphic;
        if (healthbar != null)
            return healthbar.rectTransform.parent as RectTransform ?? (RectTransform)bar.transform;
        return bar.transform as RectTransform;
    }

    private void ClearFloatingNotches()
    {
        foreach (var kvp in floatingNotches)
            kvp.Value?.Destroy();
        floatingNotches.Clear();
    }

    #endregion

    public void OnDestroy()
    {
        playerNotches?.Destroy();
        playerNotches = null;
        ClearBossNotches();
        ClearFloatingNotches();
        Instance = null;
    }

    // Harmony patches
    internal static class BossHealthbarPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BossHealthbar), nameof(BossHealthbar.Initialize))]
        private static void PostfixInitialize(BossHealthbar prefab, EnemyBrain target)
        {
            if (Instance == null)
                return;

            try
            {
                var instances = BossHealthbar.instances;
                if (instances == null || instances.Count == 0)
                    return;

                BossHealthbar bar = instances[instances.Count - 1];
                if (bar != null)
                    Instance.AttachBossBar(bar);
            }
            catch (Exception ex)
            {
                SparrohPlugin.Logger.LogError($"Failed to attach boss health notches: {ex.Message}");
            }
        }
    }

    internal static class HealthbarPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Healthbar), nameof(Healthbar.Activate))]
        private static void PostfixActivate(Healthbar __instance)
        {
            if (Instance == null || !Instance.enableFloatingEnemyNotches.Value)
                return;

            try
            {
                Instance.AttachFloatingBar(__instance);
            }
            catch (Exception ex)
            {
                SparrohPlugin.Logger.LogError($"Failed to attach floating health notches: {ex.Message}");
            }
        }
    }
}
