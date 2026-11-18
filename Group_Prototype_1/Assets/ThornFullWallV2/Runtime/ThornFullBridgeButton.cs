using UnityEngine;

/// <summary>
/// Activates the LEFT bridge when the fox overlaps the button.
/// Never changes player movement, camera controls or thorn geometry.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ThornFullBridgeButton : MonoBehaviour
{
    [Header("Connections")]
    public GameObject bridge;
    public ThornBridgeOverheadShot overheadShot;
    public CharacterController playerController;
    public PlayerHealth playerHealth;

    // Legacy goal reference retained for serialized scene compatibility.
    // Camera reveal is handled only by ThornBridgeOverheadShot.
    [HideInInspector] public Transform remoteGoal;

    [Header("Button appearance")]
    public Transform buttonCap;
    public Renderer buttonCapRenderer;
    public Material pressedMaterial;
    [Min(0f)] public float pressedOffset = 0.13f;

    [Header("Contact detection")]
    [Tooltip("Also check overlap every frame, so an unreliable trigger event cannot prevent activation.")]
    public bool useBoundsFallback = true;
    [Tooltip("Secondary proximity radius (in the ground plane) if capsule bounds miss the trigger.")]
    [Min(0.5f)] public float proximityRadius = 2.1f;
    [Tooltip("Maximum separation between the fox's feet and the button's world height.")]
    [Min(0.5f)] public float feetHeightTolerance = 2f;

    [SerializeField] private bool pressed;
    private Vector3 unpressedCapLocalPosition;
    private Material unpressedMaterial;
    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;
        if (buttonCap != null) unpressedCapLocalPosition = buttonCap.localPosition;
        if (buttonCapRenderer != null) unpressedMaterial = buttonCapRenderer.sharedMaterial;
        ResolveReferences();
        if (bridge != null) bridge.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.onDied.AddListener(ResetBridge);
    }

    private void Start()
    {
        ResolveReferences();
        if (bridge == null)
            Debug.LogError("[Thorn Button] Missing bridge! Use Tools > Thorn Full Wall v2 > Reconnect Button + LEFT Bridge (v4).", this);
        if (overheadShot == null)
            Debug.LogWarning("[Thorn Button] Overhead shot not connected: bridge can still appear, but no camera reveal.", this);
        if (playerController == null)
            Debug.LogError("[Thorn Button] Fox CharacterController not assigned or found.", this);
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.onDied.RemoveListener(ResetBridge);
    }

    private void Update()
    {
        if (pressed || Time.timeScale <= 0f || !useBoundsFallback) return;
        if (playerController == null || !playerController.enabled) return;

        // No Rigidbody/trigger callback required. This also works when the fox
        // is already inside the button's trigger volume when play starts.
        Bounds foxBounds = playerController.bounds;
        if (triggerCollider != null && triggerCollider.enabled &&
            triggerCollider.gameObject.activeInHierarchy &&
            triggerCollider.bounds.Intersects(foxBounds))
        {
            Activate("capsule/button bounds overlap");
            return;
        }

        // Secondary safeguard for unusual CharacterController collision setups.
        Vector3 p = transform.position;
        Vector3 c = foxBounds.center;
        float dx = p.x - c.x;
        float dz = p.z - c.z;
        float feet = foxBounds.min.y;
        if (dx * dx + dz * dz <= proximityRadius * proximityRadius &&
            Mathf.Abs(feet - p.y) <= feetHeightTolerance)
        {
            Activate("fox standing near button");
        }
    }

    private void OnTriggerEnter(Collider other) { TryActivateFromCollider(other); }
    private void OnTriggerStay(Collider other) { TryActivateFromCollider(other); }

    private void TryActivateFromCollider(Collider other)
    {
        if (pressed || other == null) return;
        CharacterController fox = other.GetComponentInParent<CharacterController>();
        if (fox != null && (playerController == null || fox == playerController))
            Activate("OnTriggerEnter/Stay");
    }

    private void ResolveReferences()
    {
        if (playerController == null) playerController = FindFirstObjectByType<CharacterController>();
        if (playerHealth == null && playerController != null)
            playerHealth = playerController.GetComponent<PlayerHealth>();

        // Includes inactive objects. The bridge is deliberately initially inactive.
        if (bridge == null || overheadShot == null)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (bridge == null && t.name == "BRIDGE - appears on LEFT")
                        bridge = t.gameObject;
                    if (overheadShot == null)
                    {
                        ThornBridgeOverheadShot shot = t.GetComponent<ThornBridgeOverheadShot>();
                        if (shot != null) overheadShot = shot;
                    }
                    if (bridge != null && overheadShot != null) return;
                }
            }
        }
    }

    private void Activate(string reason)
    {
        if (pressed) return;
        ResolveReferences();
        if (bridge == null)
        {
            Debug.LogError("[Thorn Button] Fox reached button, but no bridge object could be found!", this);
            return;
        }

        pressed = true;
        if (buttonCap != null)
            buttonCap.localPosition = unpressedCapLocalPosition + Vector3.down * pressedOffset;
        if (buttonCapRenderer != null && pressedMaterial != null)
            buttonCapRenderer.sharedMaterial = pressedMaterial;

        // IMPORTANT: bridge visibility must never depend on whether a camera
        // reference is set or whether a coroutine succeeds.
        bridge.SetActive(true);
        Physics.SyncTransforms();
        Debug.Log("[Thorn Button] ACTIVATED via " + reason +
                  ". LEFT bridge is now active at " + bridge.transform.position, this);

        if (overheadShot != null)
            overheadShot.ShowBridge(bridge);
        else
            Debug.LogWarning("[Thorn Button] Bridge active, but no overhead camera was linked.", this);
    }

    [ContextMenu("TEST - Reveal Bridge + Camera (Play Mode)")]
    public void TestReveal()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Thorn Button] Enter Play Mode to use this test.", this);
            return;
        }
        Activate("manual Inspector test");
    }

    public void ResetBridge()
    {
        pressed = false;
        if (overheadShot != null) overheadShot.CancelReveal();
        if (bridge != null) bridge.SetActive(false);
        if (buttonCap != null) buttonCap.localPosition = unpressedCapLocalPosition;
        if (buttonCapRenderer != null) buttonCapRenderer.sharedMaterial = unpressedMaterial;
    }
}
