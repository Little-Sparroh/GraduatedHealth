using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates and maintains vertical graduation ticks on a health bar RectTransform.
/// Ticks are laid out across the full bar width based on max health / interval.
/// </summary>
public sealed class HealthbarNotches
{
    private static Sprite whiteSprite;

    private readonly List<Image> ticks = new List<Image>(32);
    private RectTransform parent;
    private GameObject root;
    private RectTransform rootRect;
    private float lastMaxHealth = -1f;
    private float lastInterval = -1f;
    private float lastWidth = -1f;
    private float lastHeight = -1f;
    private Color lastColor;
    private float lastThickness = -1f;

    private static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply(false, false);
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
                whiteSprite.name = "GraduatedHealth_White";

            }
            return whiteSprite;
        }
    }


    public RectTransform Parent => parent;
    public bool IsValid => root != null && parent != null;

    public void Attach(RectTransform barParent)
    {
        if (barParent == null)
            return;

        if (parent == barParent && root != null)
            return;

        Destroy();
        parent = barParent;

        root = new GameObject("GraduatedHealth_Notches", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.localScale = Vector3.one;
        rootRect.SetAsLastSibling();
    }

    public void Update(
        float maxHealth,
        float interval,
        Color color,
        float thickness,
        float heightFraction = 1f)
    {
        if (rootRect == null || parent == null || interval <= 0f || maxHealth <= 0f)
            return;

        float width = parent.rect.width;
        if (width <= 0f)
            width = parent.sizeDelta.x;
        float height = parent.rect.height;
        if (height <= 0f)
            height = parent.sizeDelta.y;

        bool needsRebuild =
            !Mathf.Approximately(maxHealth, lastMaxHealth) ||
            !Mathf.Approximately(interval, lastInterval) ||
            !Mathf.Approximately(width, lastWidth) ||
            !Mathf.Approximately(height, lastHeight) ||
            !Mathf.Approximately(thickness, lastThickness) ||
            color != lastColor;

        if (!needsRebuild)
            return;

        lastMaxHealth = maxHealth;
        lastInterval = interval;
        lastWidth = width;
        lastHeight = height;
        lastColor = color;
        lastThickness = thickness;

        // ticks at interval, 2*interval, ... up to but not including max (and not at 0)
        int tickCount = 0;
        for (float h = interval; h < maxHealth - 0.001f; h += interval)
            tickCount++;


        EnsureTickCount(tickCount);

        float tickH = Mathf.Max(1f, height * Mathf.Clamp01(heightFraction));
        float halfThick = thickness * 0.5f;

        int index = 0;
        for (float h = interval; h < maxHealth - 0.001f; h += interval)
        {
            float t = h / maxHealth; // 0..1 along bar
            Image img = ticks[index];
            RectTransform rt = img.rectTransform;

            // Left-origin layout: x from 0 to width
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(thickness, tickH);
            rt.anchoredPosition = new Vector2(t * width, 0f);
            // Keep tick fully inside bar edges
            if (rt.anchoredPosition.x < halfThick)
                rt.anchoredPosition = new Vector2(halfThick, 0f);
            else if (rt.anchoredPosition.x > width - halfThick)
                rt.anchoredPosition = new Vector2(width - halfThick, 0f);

            img.color = color;
            if (!img.gameObject.activeSelf)
                img.gameObject.SetActive(true);
            index++;
        }

        for (int i = index; i < ticks.Count; i++)
        {
            if (ticks[i].gameObject.activeSelf)
                ticks[i].gameObject.SetActive(false);
        }
    }

    private void EnsureTickCount(int count)
    {
        while (ticks.Count < count)
        {
            var go = new GameObject($"Notch_{ticks.Count}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(rootRect, false);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = Color.white;
            img.sprite = WhiteSprite;
            img.type = Image.Type.Simple;
            ticks.Add(img);

        }
    }

    public void Destroy()
    {
        if (root != null)
        {
            Object.Destroy(root);
            root = null;
            rootRect = null;
        }
        ticks.Clear();
        parent = null;
        lastMaxHealth = -1f;
        lastInterval = -1f;
        lastWidth = -1f;
        lastHeight = -1f;
        lastThickness = -1f;
    }
}
