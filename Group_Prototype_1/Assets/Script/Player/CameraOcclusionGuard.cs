using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the player visible. After Cinemachine has placed the camera
/// each frame, this checks the line from the player to the camera. If a
/// wall, the tree trunk or the next part of the level is in the way, the
/// camera is moved forward to just in front of it, then eased back out
/// once the view is clear again.
///
/// It adds itself to the Main Camera in every level (any scene with a
/// ThirdPersonMovement), so nothing has to be placed by hand. The values
/// can be changed on the Main Camera while playing to try them out.
/// </summary>
[DefaultExecutionOrder(10000)]
public class CameraOcclusionGuard : MonoBehaviour
{
    [Tooltip("The player. Found automatically.")]
    public Transform target;

    [Tooltip("Which layers can block the view.")]
    public LayerMask obstacles = ~0;

    [Tooltip("Thickness of the check. Bigger = reacts before a wall " +
             "edge actually cuts into the picture.")]
    public float castRadius = 0.35f;

    [Tooltip("Gap kept between the camera and the thing it stopped at.")]
    public float margin = 0.25f;

    [Tooltip("The camera never comes closer to the player than this.")]
    public float minDistance = 1.6f;

    [Tooltip("How fast the camera moves back out when the view clears " +
             "(units per second). Moving in is instant.")]
    public float returnSpeed = 8f;

    private CharacterController controller;
    private float limit = float.PositiveInfinity;
    private readonly RaycastHit[] hits = new RaycastHit[32];

    // =====================================================
    // Automatic set-up
    // =====================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterFirstScene()
    {
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Ensure();
    }

    static void Ensure()
    {
        ThirdPersonMovement movement =
            FindFirstObjectByType<ThirdPersonMovement>();

        Camera cam = Camera.main;

        if (movement == null || cam == null)
            return;

        CameraOcclusionGuard guard = cam.GetComponent<CameraOcclusionGuard>();

        if (guard == null)
            guard = cam.gameObject.AddComponent<CameraOcclusionGuard>();

        if (guard.target == null)
            guard.target = movement.transform;
    }

    // =====================================================
    // Every frame, after Cinemachine
    // =====================================================

    void LateUpdate()
    {
        if (target == null)
            return;

        if (controller == null)
            controller = target.GetComponent<CharacterController>();

        // Look from the upper body, not the feet, so the floor the
        // player stands on never counts as blocking.
        Vector3 focus = controller != null
            ? controller.bounds.center +
              Vector3.up * (controller.bounds.extents.y * 0.5f)
            : target.position + Vector3.up;

        Vector3 toCamera = transform.position - focus;
        float wanted = toCamera.magnitude;

        if (wanted < 0.05f)
            return;

        Vector3 direction = toCamera / wanted;

        float allowed = wanted;

        int count = Physics.SphereCastNonAlloc(
            focus, castRadius, direction, hits, wanted,
            obstacles, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            // Not the player itself.
            if (hit.collider.transform.IsChildOf(target))
                continue;

            // Distance 0 = already touching at the start (standing
            // right against a wall). That is not something in between.
            if (hit.distance <= 0.001f)
                continue;

            // Loose physics objects (the rock you push) do not count.
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body != null && !body.isKinematic)
                continue;

            allowed = Mathf.Min(allowed, hit.distance - margin);
        }

        allowed = Mathf.Max(allowed, Mathf.Min(minDistance, wanted));

        // In at once (never show the inside of a wall), out smoothly.
        if (allowed < limit)
        {
            limit = allowed;
        }
        else
        {
            limit = Mathf.MoveTowards(
                limit, allowed, returnSpeed * Time.unscaledDeltaTime);
        }

        float distance = Mathf.Min(limit, wanted);

        if (distance < wanted - 0.001f)
            transform.position = focus + direction * distance;
    }
}
