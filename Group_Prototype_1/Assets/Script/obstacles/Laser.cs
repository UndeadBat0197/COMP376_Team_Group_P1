using UnityEngine;

// Laser hazard. Put this on a Cube (GameObject > 3D Object > Cube).
// - Stretch / rotate / move it with the normal Scale, Rotate and Move tools.
// - Parent it to a moving or rotating object (like the cylinder) and it still works.
// - The damage area is always exactly the cube's BoxCollider, so what you see is what hurts.
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(MeshRenderer))]
public class Laser : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Fraction of the player's max health removed per zap. 0.3333 = one third.")]
    [Range(0f, 1f)] public float damageFraction = 1f / 3f;

    [Tooltip("Which layers the laser can hit. Leave as Everything unless you need otherwise.")]
    public LayerMask hitLayers = ~0;

    [Header("Look")]
    public Color laserColor = new Color(1f, 0.1f, 0.1f, 1f);
    [Tooltip("How bright the glow is (only visible if the material has Emission enabled).")]
    public float glowIntensity = 3f;

    [Header("Blinking (optional)")]
    [Tooltip("Turn on to make the laser switch on and off on a timer.")]
    public bool blink = false;
    public float onDuration = 2f;
    public float offDuration = 1f;
    [Tooltip("Shift this laser's timing so several lasers don't blink in sync.")]
    public float timeOffset = 0f;

    public bool IsOn { get; private set; } = true;

    private BoxCollider box;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock props;

    void OnEnable()
    {
        box = GetComponent<BoxCollider>();
        meshRenderer = GetComponent<MeshRenderer>();

        // Trigger so the player walks *into* the beam instead of bumping off it.
        box.isTrigger = true;

        // Lasers shouldn't cast shadows.
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ApplyColor();
    }

    void OnValidate()
    {
        if (onDuration < 0f) onDuration = 0f;
        if (offDuration < 0f) offDuration = 0f;
        if (meshRenderer != null) ApplyColor();
    }

    void Update()
    {
        // In the editor (not playing) we only update the visuals.
        if (!Application.isPlaying)
            return;

        UpdateBlink();

        if (IsOn)
            CheckForPlayer();
    }

    void UpdateBlink()
    {
        bool shouldBeOn = true;

        if (blink && onDuration + offDuration > 0f)
        {
            float cycle = onDuration + offDuration;
            float t = Mathf.Repeat(Time.time + timeOffset, cycle);
            shouldBeOn = t < onDuration;
        }

        if (shouldBeOn != IsOn)
        {
            IsOn = shouldBeOn;
            meshRenderer.enabled = IsOn;
        }
    }

    void CheckForPlayer()
    {
        // Build a box in world space that exactly matches the collider,
        // including any scaling / rotation / parenting.
        Vector3 center = transform.TransformPoint(box.center);
        Vector3 halfExtents = Vector3.Scale(box.size, AbsScale(transform.lossyScale)) * 0.5f;

        Collider[] hits = Physics.OverlapBox(
            center, halfExtents, transform.rotation,
            hitLayers, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                // PlayerHealth's invincibility window stops this from hitting every frame.
                health.TakeDamageFraction(damageFraction);
                return;
            }
        }
    }

    void ApplyColor()
    {
        if (props == null) props = new MaterialPropertyBlock();

        meshRenderer.GetPropertyBlock(props);
        props.SetColor("_BaseColor", laserColor);                 // URP
        props.SetColor("_Color", laserColor);                     // Built-in
        props.SetColor("_EmissionColor", laserColor * glowIntensity);
        meshRenderer.SetPropertyBlock(props);
    }

    static Vector3 AbsScale(Vector3 s) =>
        new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));

    // Shows the damage box in the Scene view, even when the laser is blinking off.
    void OnDrawGizmos()
    {
        if (box == null) box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = new Color(laserColor.r, laserColor.g, laserColor.b, 0.6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
