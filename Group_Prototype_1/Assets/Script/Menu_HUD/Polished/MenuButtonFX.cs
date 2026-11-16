using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover / press feedback for a menu button: it grows a little, changes
/// colour and nudges its label. Works the same with the mouse, the
/// keyboard and a gamepad.
/// </summary>
public class MenuButtonFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    public Graphic background;
    public TMP_Text label;
    public RectTransform labelRect;

    public Color normalBackground = new Color(0.08f, 0.15f, 0.12f, 0.85f);
    public Color highlightBackground = new Color(1f, 0.54f, 0.17f, 1f);
    public Color normalText = Color.white;
    public Color highlightText = Color.black;

    public float highlightScale = 1.04f;
    public float pressedScale = 0.97f;
    public float labelShift = 10f;
    public float sharpness = 16f;

    private bool hovered;
    private bool selected;
    private bool pressed;
    private float blend;
    private Vector2 labelBase;
    private bool hasLabelBase;

    bool Highlighted
    {
        get { return hovered || selected; }
    }

    void Awake()
    {
        if (labelRect != null)
        {
            labelBase = labelRect.anchoredPosition;
            hasLabelBase = true;
        }
    }

    void OnEnable()
    {
        hovered = false;
        pressed = false;
        selected =
            EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject;

        blend = selected ? 1f : 0f;
        Apply(1f);
    }

    void Update()
    {
        float target = Highlighted ? 1f : 0f;

        blend = Mathf.Lerp(
            blend, target,
            1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));

        Apply(1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));
    }

    void Apply(float scaleLerp)
    {
        if (background != null)
        {
            background.color = Color.Lerp(
                normalBackground, highlightBackground, blend);
        }

        if (label != null)
            label.color = Color.Lerp(normalText, highlightText, blend);

        if (labelRect != null && hasLabelBase)
        {
            labelRect.anchoredPosition =
                labelBase + new Vector2(labelShift * blend, 0f);
        }

        float wanted = pressed
            ? pressedScale
            : Mathf.Lerp(1f, highlightScale, blend);

        float now = Mathf.Lerp(transform.localScale.x, wanted, scaleLerp);
        transform.localScale = new Vector3(now, now, 1f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;

        // Keep one highlighted button for mouse and keyboard together.
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != gameObject)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pressed && PolishedMenuController.Instance != null)
            PolishedMenuController.Instance.PlayClick();

        pressed = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!selected && PolishedMenuController.Instance != null)
            PolishedMenuController.Instance.PlayHover();

        selected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (PolishedMenuController.Instance != null)
            PolishedMenuController.Instance.PlayClick();
    }
}
