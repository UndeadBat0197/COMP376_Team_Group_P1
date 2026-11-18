using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Temporary bridge reveal shot. It does NOT move the original player camera,
/// disable Cinemachine, change the fox, or edit the existing level geometry.
/// For reliability in URP, it briefly suspends *rendering* by the regular
/// Main Camera while the independent reveal camera shows the bridge, then
/// restores exactly the original Camera.enabled state.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ThornBridgeOverheadShot : MonoBehaviour
{
    [Header("Existing Camera (not moved or reconfigured)")]
    public Camera normalCamera;

    [Header("Target for Inspector test")]
    [Tooltip("The left-side bridge. Automatically linked by the v6 scene repair menu.")]
    public GameObject testBridge;

    [Header("Shot framing")]
    [Min(12f)] public float overheadHeight = 45f;
    [Range(25f, 90f)] public float overheadFieldOfView = 49f;
    [Min(0.05f)] public float travelSeconds = 0.65f;
    [Min(0.2f)] public float holdSeconds = 2.5f;
    [Tooltip("World-space offset from the LEFT bridge toward the center of the gap.")]
    public Vector3 lookTargetOffset = new Vector3(3f, 0f, 0f);

    [Header("Exit hole reveal (second shot)")]
    [Tooltip("Stationary empty GameObject positioned to film the END pipe from below, before and after the swap. Parent it under World (does not turn).")]
    public Transform exitShotView;

    [Range(25f, 90f)] public float exitShotFieldOfView = 50f;
    [Min(0f)] public float showSolidWallSeconds = 0.8f;
    [Min(0f)] public float showOpenedHoleSeconds = 1.7f;

    [Header("Scene change — assigned in Inspector")]
    [InspectorName("On Exit Shot - Swap Trunks")]
    [Tooltip("Connect TRUNK LAYOUT SWITCH -> TrunkLayoutSwap.SwapToExitLayout(). This is invoked only once the second camera shot is looking at the solid END pipe.")]
    public UnityEvent onBridgeRevealed;

    private Camera shotCamera;
    private Coroutine shotRoutine;
    private bool hasSuspendedNormalCamera;
    private bool normalCameraWasEnabled;

    private void Awake()
    {
        shotCamera = GetComponent<Camera>();
        shotCamera.enabled = false;
    }

    public void ShowBridge(GameObject bridge)
    {
        if (bridge == null)
        {
            Debug.LogError("[Bridge Camera] Reveal requested but LEFT bridge reference is missing.", this);
            return;
        }

        if (normalCamera == null)
            normalCamera = Camera.main;

        if (normalCamera == null || normalCamera == GetComponent<Camera>())
        {
            Debug.LogError("[Bridge Camera] Normal Main Camera not assigned. Run the v6 camera reconnect menu in the thorn scene.", this);
            return;
        }

        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);
        if (!enabled)
            enabled = true;

        CancelReveal();
        testBridge = bridge;
        shotRoutine = StartCoroutine(PlayShot(bridge.transform));
    }

    private IEnumerator PlayShot(Transform bridge)
    {
        shotCamera = GetComponent<Camera>();

        // Configure only the NEW temporary camera. Do not copy the Main Camera's
        // targetTexture, physical camera mode, URP stack, or render pipeline settings.
        shotCamera.targetTexture = null;
        shotCamera.targetDisplay = normalCamera.targetDisplay;
        shotCamera.rect = normalCamera.rect;
        shotCamera.cullingMask = normalCamera.cullingMask;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        shotCamera.backgroundColor = normalCamera.backgroundColor;
        shotCamera.nearClipPlane = 0.05f;
        shotCamera.farClipPlane = Mathf.Max(500f, normalCamera.farClipPlane);
        shotCamera.orthographic = false;
        shotCamera.fieldOfView = normalCamera.fieldOfView;
        shotCamera.depth = Mathf.Max(1000f, normalCamera.depth + 100f);

        UniversalAdditionalCameraData data =
            shotCamera.GetComponent<UniversalAdditionalCameraData>();
        if (data != null)
            data.renderType = CameraRenderType.Base;

        // Begin at the current fox-camera view, then fly to a top-down view.
        transform.SetPositionAndRotation(
            normalCamera.transform.position,
            normalCamera.transform.rotation);

        // The critical change from v4: the regular camera cannot overwrite the
        // reveal because it is temporarily NOT rendering. The normal camera's
        // TRANSFORM and Cinemachine components continue untouched.
        normalCameraWasEnabled = normalCamera.enabled;
        hasSuspendedNormalCamera = true;
        normalCamera.enabled = false;
        shotCamera.enabled = true;
        Debug.Log("[Bridge Camera] STARTED. Main Camera rendering temporarily paused; overhead camera enabled.", this);

        Vector3 focus = bridge.position + lookTargetOffset;
        Vector3 destination = focus + Vector3.up * overheadHeight;
        Quaternion lookingDown = Quaternion.LookRotation(Vector3.down, Vector3.forward);

        yield return FlyTo(destination, lookingDown, overheadFieldOfView, travelSeconds);
        Debug.Log("[Bridge Camera] Showing top-down view of LEFT gap platform.", this);
        yield return new WaitForSecondsRealtime(holdSeconds);

        // Cut to a second, stationary viewpoint BELOW the last joined pipe.
        // IMPORTANT: do not swap the trunk while the camera is still on the bridge.
        if (exitShotView != null)
        {
            transform.SetPositionAndRotation(exitShotView.position, exitShotView.rotation);
            shotCamera.fieldOfView = exitShotFieldOfView;

            // Film the solid pipe first: viewers see that there was NO opening.
            Debug.Log("[Bridge Camera] EXIT SHOT: showing solid wall BEFORE swap.", this);
            yield return new WaitForSecondsRealtime(showSolidWallSeconds);

            // The existing level-switch callback is preserved by retaining its
            // serialized field name, onBridgeRevealed. Its timing is now correct.
            Debug.Log("[Bridge Camera] EXIT SHOT: swapping trunks now.", this);
            onBridgeRevealed?.Invoke();
            Physics.SyncTransforms();

            // Hold on the exact same camera framing as the hole/stairs appear.
            yield return new WaitForSecondsRealtime(showOpenedHoleSeconds);
        }
        else
        {
            // Fail visibly instead of secretly swapping during the bridge shot.
            Debug.LogWarning("[Bridge Camera] Exit Shot View is unassigned. No trunk swap was triggered; assign an EXIT SHOT POINT Transform in the Inspector.", this);
        }

        // Follow the live camera's current pose so the transition returns to
        // the fox even if Cinemachine moved while the cutaway was playing.
        yield return ReturnToFox(travelSeconds);

        RestoreOriginalCamera();
        shotRoutine = null;
        Debug.Log("[Bridge Camera] RETURNED to the fox camera.", this);
    }

    private IEnumerator FlyTo(Vector3 targetPosition, Quaternion targetRotation,
                              float targetFov, float duration)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float startFov = shotCamera.fieldOfView;
        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, targetPosition, alpha),
                Quaternion.Slerp(startRotation, targetRotation, alpha));
            shotCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, alpha);
            yield return null;
        }
        transform.SetPositionAndRotation(targetPosition, targetRotation);
        shotCamera.fieldOfView = targetFov;
    }

    private IEnumerator ReturnToFox(float duration)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float startFov = shotCamera.fieldOfView;
        float elapsed = 0f;
        duration = Mathf.Max(0.05f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, normalCamera.transform.position, alpha),
                Quaternion.Slerp(startRotation, normalCamera.transform.rotation, alpha));
            shotCamera.fieldOfView = Mathf.Lerp(startFov, normalCamera.fieldOfView, alpha);
            yield return null;
        }
    }

    private void RestoreOriginalCamera()
    {
        if (shotCamera != null)
            shotCamera.enabled = false;

        if (hasSuspendedNormalCamera && normalCamera != null)
            normalCamera.enabled = normalCameraWasEnabled;

        hasSuspendedNormalCamera = false;
    }

    public void CancelReveal()
    {
        if (shotRoutine != null)
        {
            StopCoroutine(shotRoutine);
            shotRoutine = null;
        }
        RestoreOriginalCamera();
    }

    [ContextMenu("TEST - Overhead shot now (Play Mode)")]
    private void TestOverheadShot()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Bridge Camera] Enter Play Mode first.", this);
            return;
        }
        if (testBridge == null)
        {
            Debug.LogError("[Bridge Camera] TEST bridge is unassigned; run v6 reconnect.", this);
            return;
        }
        // For test purposes, visualize the left bridge even if button not pressed.
        testBridge.SetActive(true);
        ShowBridge(testBridge);
    }

    private void OnDisable() { CancelReveal(); }
    private void OnDestroy() { RestoreOriginalCamera(); }
}
