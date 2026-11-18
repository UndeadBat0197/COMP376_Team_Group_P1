#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Edits ONLY the current generated thorn scene's bridge placement and
/// installs an INDEPENDENT overhead camera. No player/camera scripts, rigs,
/// movement settings or thorn objects are modified. Never rebuilds the scene.
/// </summary>
public static class ThornBridgeOverheadInstaller
{
    private const string ScenePath =
        "Assets/Scenes/narges/Thorn_FullWall_SliderBridge_v2.unity";
    private const string ShotName = "BRIDGE REVEAL CAMERA - temporary overhead shot";

    [MenuItem("Tools/Thorn Full Wall v2/Install Left Bridge + Overhead Shot (Keep Level)")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
        {
            EditorUtility.DisplayDialog("Open the thorn level first",
                "Open " + ScenePath + " and then run this command.", "OK");
            return;
        }

        Transform[] all = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();

        Transform world = all.FirstOrDefault(t => t.name == "World (does not turn)");
        Transform bridgeTransform = all.FirstOrDefault(t => t.name == "BRIDGE - appears on LEFT");
        ThornFullBridgeButton button = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<ThornFullBridgeButton>(true))
            .FirstOrDefault();
        Camera normal = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(c => c.CompareTag("MainCamera"));

        if (world == null || bridgeTransform == null || button == null || normal == null)
        {
            EditorUtility.DisplayDialog("Scene objects missing",
                "The generated scene must already contain World (does not turn), " +
                "BRIDGE - appears on LEFT, BUTTON - halfway, and Main Camera. " +
                "This tool deliberately won't rebuild or delete your thorns.", "OK");
            return;
        }

        // Same left-gap position as the designed layout, in coordinates of
        // World (does not turn). This is the ONLY existing scene object moved.
        Undo.RecordObject(bridgeTransform, "Place bridge on left of gap");
        bridgeTransform.SetParent(world, true);
        bridgeTransform.localPosition = new Vector3(-5.5f, -9.35f, 43.75f);

        Transform shotTransform = world.Find(ShotName);
        if (shotTransform == null)
        {
            GameObject go = new GameObject(ShotName);
            Undo.RegisterCreatedObjectUndo(go, "Create temporary overhead bridge camera");
            shotTransform = go.transform;
            shotTransform.SetParent(world, false);
        }

        Camera shotCamera = shotTransform.GetComponent<Camera>();
        if (shotCamera == null) shotCamera = shotTransform.gameObject.AddComponent<Camera>();
        // URP: a second Base camera with higher priority draws after Main Camera.
        // We leave the existing Main Camera and CinemachineBrain entirely alone.
        UniversalAdditionalCameraData data =
            shotTransform.GetComponent<UniversalAdditionalCameraData>();
        if (data == null)
            data = shotTransform.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderType = CameraRenderType.Base;
        shotCamera.depth = normal.depth + 50f;
        shotCamera.cullingMask = normal.cullingMask;
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        shotCamera.nearClipPlane = .1f;
        shotCamera.farClipPlane = 500f;
        shotCamera.orthographic = false;
        shotCamera.fieldOfView = 49f;
        shotCamera.enabled = false;  // Never steals the view before the button.

        ThornBridgeOverheadShot reveal =
            shotTransform.GetComponent<ThornBridgeOverheadShot>();
        if (reveal == null)
            reveal = shotTransform.gameObject.AddComponent<ThornBridgeOverheadShot>();
        reveal.normalCamera = normal;  // Reference only; script never writes to it.
        reveal.overheadHeight = 53f;
        reveal.overheadFieldOfView = 49f;
        reveal.travelSeconds = .75f;
        reveal.holdSeconds = 2.5f;
        reveal.lookTargetOffset = new Vector3(3f, 0f, 0f);

        // Scene gizmo makes overhead placement easy to inspect and edit.
        Vector3 focus = bridgeTransform.position + reveal.lookTargetOffset;
        shotTransform.SetPositionAndRotation(
            focus + Vector3.up * reveal.overheadHeight,
            Quaternion.LookRotation(Vector3.down, Vector3.forward));

        Undo.RecordObject(button, "Connect button to temporary overhead bridge shot");
        button.bridge = bridgeTransform.gameObject;
        button.overheadShot = reveal;
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(shotCamera);
        EditorUtility.SetDirty(reveal);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        bool oldOverridePresent = normal.GetComponents<MonoBehaviour>()
            .Any(b => b != null && b.GetType().Name == "ThornFullOverheadCamera");
        Debug.Log("Installed left bridge + overhead reveal. " +
                  "No changes to fox movement or original Main Camera/Cinemachine. " +
                  "Press the tunnel button to view the bridge from above.");
        if (oldOverridePresent)
            Debug.LogWarning("An old ThornFullOverheadCamera override is still on Main Camera. " +
                "Run Tools > Thorn Full Wall v2 > Restore Original Player + Camera (Keep Level) first.");
    }
}
#endif
