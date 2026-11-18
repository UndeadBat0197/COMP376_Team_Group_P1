using UnityEngine;

/// <summary>
/// Activates the left bridge and its independent overhead reveal camera.
/// Accepts CharacterController trigger contact, with a feet-on-button proximity
/// fallback in case the trigger event isn't delivered by the physics setup.
/// Does not read or modify player movement, input, or the original camera.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ThornFullBridgeButton : MonoBehaviour
{
    [Header("Connections")]
    public GameObject bridge;
    public ThornBridgeOverheadShot overheadShot;
    public CharacterController playerController;
    public PlayerHealth playerHealth;

    // Kept only for compatibility with older scene generators. Never used by this button.
    [HideInInspector] public Transform remoteGoal;
    [HideInInspector] public ThornFullOverheadCamera revealCamera;

    [Header("Button appearance")]
    public Transform buttonCap;
    public Renderer buttonCapRenderer;
    public Material pressedMaterial;
    [Min(0f)] public float pressedOffset = 0.13f;

    [Header("Reliable landing detection")]
    [Tooltip("Horizontal distance from the button center that counts as stepping on it.")]
    [Min(0.25f)] public float activationRadius = 1.45f;
    [Tooltip("How far below/above the button's origin the fox's feet may be.")]
    public float feetBelow = 0.65f;
    public float feetAbove = 1.15f;

    private Vector3 initialCapPosition;
    private Material idleMaterial;
    private bool pressed;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null) trigger.isTrigger = true;
        if (buttonCap != null) initialCapPosition = buttonCap.localPosition;
        if (buttonCapRenderer != null) idleMaterial = buttonCapRenderer.sharedMaterial;
        if (playerController == null) playerController = FindFirstObjectByType<CharacterController>();
        if (playerHealth == null && playerController != null)
            playerHealth = playerController.GetComponent<PlayerHealth>();

        // The bridge must be missing until the player activates the button.
        if (bridge != null) bridge.SetActive(false);
    }

    private void Start()
    {
        if (bridge == null)
            Debug.LogError("Thorn button: BRIDGE reference is missing. Run Fix Button and Bridge Reveal (Keep Level) from the Tools menu.", this);
        if (overheadShot == null)
            Debug.LogWarning("Thorn button: overhead camera not connected. Bridge will still appear, but without the reveal shot.", this);
        if (playerController == null)
            Debug.LogError("Thorn button: fox CharacterController not found.", this);
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.onDied.AddListener(ResetBridge);
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.onDied.RemoveListener(ResetBridge);
    }

    private void Update()
    {
        if (pressed || playerController == null || !playerController.enabled || Time.timeScale <= 0f)
            return;

        // A fallback for CharacterController/kinematic trigger combinations.
        // Only counts the fox's FEET at button height, never fox passing overhead.
        Bounds fox = playerController.bounds;
        Vector3 button = transform.position;
        float dx = fox.center.x - button.x;
        float dz = fox.center.z - button.z;
        float footY = fox.min.y;

        if (dx * dx + dz * dz <= activationRadius * activationRadius &&
            footY >= button.y - feetBelow &&
            footY <= button.y + feetAbove)
            Activate();
    }

    private void OnTriggerEnter(Collider other) { TryTrigger(other); }
    private void OnTriggerStay(Collider other) { TryTrigger(other); }

    private void TryTrigger(Collider other)
    {
        if (pressed || other == null) return;
        CharacterController controller = other.GetComponentInParent<CharacterController>();
        if (controller == null) return;
        if (playerController != null && controller != playerController) return;
        Activate();
    }

    private void Activate()
    {
        if (pressed) return;
        pressed = true;
        if (buttonCap != null)
            buttonCap.localPosition = initialCapPosition + Vector3.down * pressedOffset;
        if (buttonCapRenderer != null && pressedMaterial != null)
            buttonCapRenderer.sharedMaterial = pressedMaterial;

        if (bridge == null)
        {
            Debug.LogError("Button activated, but no bridge is assigned! Run the Repair menu command.", this);
            return;
        }

        if (overheadShot != null && overheadShot.isActiveAndEnabled)
        {
            // Reveals the bridge in frame once the temporary camera reaches the gap.
            overheadShot.ShowBridge(bridge);
        }
        else
        {
            bridge.SetActive(true); // Always leave a usable crossing.
            Debug.LogWarning("Thorn button: bridge appeared without overhead shot because camera reference is missing or disabled.", this);
        }
        Debug.Log("THORN BUTTON PRESSED: left bridge/camera sequence triggered.", this);
    }

    [ContextMenu("TEST - Activate Bridge Now (Play Mode)")]
    public void TestActivateBridge()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode to test the button.", this);
            return;
        }
        Activate();
    }

    public void ResetBridge()
    {
        pressed = false;
        if (overheadShot != null) overheadShot.CancelReveal();
        if (bridge != null) bridge.SetActive(false);
        if (buttonCap != null) buttonCap.localPosition = initialCapPosition;
        if (buttonCapRenderer != null) buttonCapRenderer.sharedMaterial = idleMaterial;
    }
}
