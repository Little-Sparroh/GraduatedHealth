using Pigeon.Movement;
using UnityEngine;
using UnityEngine.UI;

public class GraduatedHealthMod
{
    private readonly BossHealthbarNotches bossNotches = new();
    private readonly FloatingHealthbarNotches floatingNotches = new();

    private float lastPlayerMaxHealth = -1f;
    private RectTransform playerBarParent;
    private float playerDiscoverCooldown;
    private Graphic playerHealthGraphic;
    private HealthbarNotches playerNotches;

    public GraduatedHealthMod()
    {
        Instance = this;
    }

    public static GraduatedHealthMod Instance { get; private set; }

    public void AttachBossBar(BossHealthbar bar)
    {
        bossNotches.Attach(bar);
    }

    public void AttachFloatingBar(Healthbar bar)
    {
        floatingNotches.Attach(bar);
    }

    public void Update()
    {
        var color = ConfigManager.GetNotchColor();
        var thickness = Mathf.Max(1f, ConfigManager.NotchThickness.Value);
        var heightFrac = Mathf.Clamp01(ConfigManager.NotchHeightFraction.Value);

        if (ConfigManager.EnablePlayerNotches.Value)
        {
            UpdatePlayerNotches(color, thickness, heightFrac);
        }
        else if (playerNotches != null)
        {
            playerNotches.Destroy();
            playerNotches = null;
            playerBarParent = null;
            playerHealthGraphic = null;
            lastPlayerMaxHealth = -1f;
        }

        if (ConfigManager.EnableEnemyBossNotches.Value)
            bossNotches.Update(color, thickness, heightFrac);
        else if (bossNotches.Count > 0)
            bossNotches.Clear();

        if (ConfigManager.EnableFloatingEnemyNotches.Value)
            floatingNotches.Update(color, thickness, heightFrac);
        else if (floatingNotches.Count > 0)
            floatingNotches.Clear();
    }

    public void OnDestroy()
    {
        playerNotches?.Destroy();
        playerNotches = null;
        bossNotches.Clear();
        floatingNotches.Clear();
        Instance = null;
    }

    private void UpdatePlayerNotches(Color color, float thickness, float heightFrac)
    {
        var player = Player.LocalPlayer;
        if (player == null || !player.IsAlive)
            return;

        if (playerBarParent == null || playerHealthGraphic == null)
        {
            playerDiscoverCooldown -= Time.unscaledDeltaTime;
            if (playerDiscoverCooldown <= 0f)
            {
                playerDiscoverCooldown = 0.5f;
                if (PlayerHealthbarDiscovery.TryDiscover(player, out var g, out var parent))
                {
                    playerHealthGraphic = g;
                    playerBarParent = parent;
                }
            }

            if (playerBarParent == null)
                return;
        }

        if (playerNotches == null)
            playerNotches = new HealthbarNotches();

        if (!playerNotches.IsValid || playerNotches.Parent != playerBarParent)
            playerNotches.Attach(playerBarParent);

        var max = player.MaxHealth;
        if (max <= 0f)
            return;

        lastPlayerMaxHealth = max;
        playerNotches.Update(max, Mathf.Max(1f, ConfigManager.PlayerInterval.Value), color, thickness, heightFrac);
    }
}