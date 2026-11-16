using UnityEngine;

/// <summary>Gentle up-and-down drift for a UI element (the title).</summary>
public class UIFloat : MonoBehaviour
{
    public float amplitude = 8f;
    public float speed = 1.2f;
    public float tiltDegrees = 0.8f;

    private RectTransform rect;
    private Vector2 basePosition;
    private Quaternion baseRotation = Quaternion.identity;

    void Awake()
    {
        rect = transform as RectTransform;

        if (rect != null)
        {
            basePosition = rect.anchoredPosition;
            baseRotation = rect.localRotation;
        }
    }

    void Update()
    {
        if (rect == null)
            return;

        float t = Time.unscaledTime * speed;

        rect.anchoredPosition =
            basePosition + new Vector2(0f, Mathf.Sin(t) * amplitude);

        rect.localRotation = baseRotation *
            Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * tiltDegrees);
    }
}
