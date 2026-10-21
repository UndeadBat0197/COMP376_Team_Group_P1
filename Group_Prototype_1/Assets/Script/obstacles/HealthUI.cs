using UnityEngine;
using UnityEngine.UI;

// On-screen health bar. Add it to the Player (next to PlayerHealth) — no other setup needed.
// It builds its own UI when the game starts:
//   - one block per health point in the top-left corner (3 health = 3 blocks)
//   - a red flash over the screen when you get hit
public class HealthUI : MonoBehaviour
{
    [Tooltip("Leave empty to use the PlayerHealth on this object (or find one in the scene).")]
    public PlayerHealth playerHealth;

    [Header("Bar")]
    public Vector2 blockSize = new Vector2(70f, 22f);
    public float blockSpacing = 8f;
    public Vector2 screenMargin = new Vector2(30f, 30f);
    public Color fullColor = new Color(0.2f, 0.9f, 0.3f);
    public Color lowColor = new Color(0.95f, 0.2f, 0.2f);
    public Color emptyColor = new Color(0f, 0f, 0f, 0.5f);

    [Header("Hit flash")]
    public bool flashOnHit = true;
    public Color flashColor = new Color(1f, 0f, 0f, 0.35f);
    public float flashDuration = 0.3f;

    private Image[] fills;
    private Image flash;
    private float flashTimer;
    private float lastHealth;

    void Start()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogWarning("HealthUI: no PlayerHealth found in the scene.");
            enabled = false;
            return;
        }

        BuildUI();
        lastHealth = playerHealth.CurrentHealth;
    }

    void Update()
    {
        float hp = playerHealth.CurrentHealth;

        // Fill each block: 1 health per block, partial blocks fill partly.
        Color barColor = Color.Lerp(lowColor, fullColor, playerHealth.HealthPercent);
        for (int i = 0; i < fills.Length; i++)
        {
            float amount = Mathf.Clamp01(hp - i);
            RectTransform rt = fills[i].rectTransform;
            rt.anchorMax = new Vector2(amount, 1f);
            fills[i].color = barColor;
            fills[i].enabled = amount > 0.05f;   // hide empty blocks' fill completely
        }

        // Flash when health goes down.
        if (hp < lastHealth && flashOnHit)
            flashTimer = flashDuration;
        lastHealth = hp;

        if (flash != null)
        {
            flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
            Color c = flashColor;
            c.a *= flashDuration > 0f ? flashTimer / flashDuration : 0f;
            flash.color = c;
        }
    }

    void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("Health UI Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Full-screen hit flash (behind the bar)
        if (flashOnHit)
        {
            flash = CreateImage("Hit Flash", canvasGO.transform, Color.clear);
            var frt = flash.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            flash.raycastTarget = false;
        }

        // One block per health point
        int count = Mathf.Max(1, Mathf.CeilToInt(playerHealth.maxHealth));
        fills = new Image[count];

        for (int i = 0; i < count; i++)
        {
            Image bg = CreateImage("Health Block " + (i + 1), canvasGO.transform, emptyColor);
            RectTransform brt = bg.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);   // top-left
            brt.pivot = new Vector2(0f, 1f);
            brt.sizeDelta = blockSize;
            brt.anchoredPosition = new Vector2(
                screenMargin.x + i * (blockSize.x + blockSpacing),
                -screenMargin.y);

            Image fill = CreateImage("Fill", bg.transform, fullColor);
            RectTransform frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(3f, 3f);    // small border
            frt.offsetMax = new Vector2(-3f, -3f);
            fills[i] = fill;
        }
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
