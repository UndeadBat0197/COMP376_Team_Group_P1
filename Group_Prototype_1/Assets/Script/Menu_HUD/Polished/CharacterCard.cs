using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One card on the "Choose your character" panel. The card's Button
/// calls PolishedMenuController.SelectCharacter(characterId); this
/// script only handles how the card looks.
/// </summary>
public class CharacterCard : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler
{
    [Tooltip("Must match the id in PlayerCharacterSwitcher on the player.")]
    public string characterId = "Fox";
    public string displayName = "Fox";

    [Header("Visuals")]
    public Graphic frame;
    public Graphic glow;
    public GameObject selectedBadge;
    public CharacterPreview preview;

    public Color normalFrame = new Color(1f, 1f, 1f, 0.12f);
    public Color hoverFrame = new Color(1f, 1f, 1f, 0.45f);
    public Color selectedFrame = new Color(1f, 0.54f, 0.17f, 1f);

    public float hoverScale = 1.02f;
    public float selectedScale = 1.06f;
    public float glowAlpha = 0.55f;
    public float sharpness = 14f;

    private bool isSelected;
    private bool hovered;
    private bool focused;

    public void SetSelected(bool value, bool celebrate)
    {
        isSelected = value;

        if (selectedBadge != null)
            selectedBadge.SetActive(value);

        if (preview != null)
        {
            preview.SetSelected(value);

            if (celebrate)
                preview.Celebrate();
        }
    }

    void OnEnable()
    {
        hovered = false;
        focused = false;
        ApplyVisuals(1f);
    }

    void Update()
    {
        ApplyVisuals(1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));
    }

    void ApplyVisuals(float k)
    {
        bool highlighted = hovered || focused;

        float wantedScale = isSelected
            ? selectedScale
            : (highlighted ? hoverScale : 1f);

        float s = Mathf.Lerp(transform.localScale.x, wantedScale, k);
        transform.localScale = new Vector3(s, s, 1f);

        if (frame != null)
        {
            Color wanted = isSelected
                ? selectedFrame
                : (highlighted ? hoverFrame : normalFrame);

            frame.color = Color.Lerp(frame.color, wanted, k);
        }

        if (glow != null)
        {
            Color c = glow.color;
            c.a = Mathf.Lerp(c.a, isSelected ? glowAlpha : 0f, k);
            glow.color = c;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        focused = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        focused = false;
    }
}
