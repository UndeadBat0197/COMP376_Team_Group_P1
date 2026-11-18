#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Reconnect ONLY the reveal camera and button. Does not rebuild the scene,
/// relocate any platforms, or touch the fox, Main Camera or Cinemachine.
/// </summary>
public static class ThornBridgeCameraOnlyRepairV6
{
    private const string RevealObjectName = "BRIDGE REVEAL CAMERA - temporary overhead shot";

    [MenuItem("Tools/Thorn Full Wall v2/Fix Camera Reveal v6 (Keep Level)")]
    public static void Reconnect()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[Bridge Camera v6] Open the thorn level scene first.");
            return;
        }

        Transform[] all = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .ToArray();
        ThornFullBridgeButton button = all
            .Select(t => t.GetComponent<ThornFullBridgeButton>())
            .FirstOrDefault(b => b != null);
        Transform bridge = all.FirstOrDefault(t => t.name == "BRIDGE - appears on LEFT");
        Camera main = all.Select(t => t.GetComponent<Camera>())
            .FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
        Transform world = all.FirstOrDefault(t => t.name == "World (does not turn)");

        if (button == null || bridge == null || main == null)
        {
            EditorUtility.DisplayDialog("Reveal camera not linked",
                "Missing: " + (button == null ? "button " : "") +
                (bridge == null ? "LEFT bridge " : "") +
                (main == null ? "Main Camera" : "") +
                ". No objects were changed.", "OK");
            return;
        }

        Transform revealTransform = all.FirstOrDefault(t => t.name == RevealObjectName);
        if (revealTransform == null)
        {
            GameObject created = new GameObject(RevealObjectName);
            Undo.RegisterCreatedObjectUndo(created, "Create bridge reveal camera");
            revealTransform = created.transform;
            if (world != null) revealTransform.SetParent(world, false);
        }

        Undo.RecordObject(revealTransform.gameObject, "Enable bridge camera game object");
        revealTransform.gameObject.SetActive(true);

        Camera shotCamera = revealTransform.GetComponent<Camera>();
        if (shotCamera == null)
            shotCamera = Undo.AddComponent<Camera>(revealTransform.gameObject);

        UniversalAdditionalCameraData urp =
            revealTransform.GetComponent<UniversalAdditionalCameraData>();
        if (urp == null)
            urp = Undo.AddComponent<UniversalAdditionalCameraData>(revealTransform.gameObject);

        ThornBridgeOverheadShot shot = revealTransform.GetComponent<ThornBridgeOverheadShot>();
        if (shot == null)
            shot = Undo.AddComponent<ThornBridgeOverheadShot>(revealTransform.gameObject);

        Undo.RecordObject(shotCamera, "Configure temporary overhead camera");
        Undo.RecordObject(urp, "Set reveal camera as URP Base camera");
        Undo.RecordObject(shot, "Link reveal camera target");
        Undo.RecordObject(button, "Link button to overhead camera");

        urp.renderType = CameraRenderType.Base;
        shotCamera.enabled = false;
        shotCamera.targetTexture = null;
        shotCamera.targetDisplay = main.targetDisplay;
        shotCamera.cullingMask = main.cullingMask;
        shotCamera.depth = Mathf.Max(1000f, main.depth + 100f);
        shotCamera.clearFlags = CameraClearFlags.Skybox;
        shotCamera.nearClipPlane = 0.05f;
        shotCamera.farClipPlane = Mathf.Max(500f, main.farClipPlane);
        shotCamera.orthographic = false;
        shotCamera.fieldOfView = 49f;

        shot.enabled = true;
        shot.normalCamera = main;
        shot.testBridge = bridge.gameObject;
        shot.overheadHeight = 45f;
        shot.overheadFieldOfView = 49f;
        shot.travelSeconds = 0.65f;
        shot.holdSeconds = 2.5f;
        shot.lookTargetOffset = new Vector3(3f, 0f, 0f);

        // Do not move the LEFT bridge, fox, Main Camera, or any thorns.
        // Merely preview where the overhead camera will go when activated.
        Vector3 focus = bridge.position + shot.lookTargetOffset;
        Undo.RecordObject(revealTransform, "Place overhead camera preview");
        revealTransform.SetPositionAndRotation(
            focus + Vector3.up * shot.overheadHeight,
            Quaternion.LookRotation(Vector3.down, Vector3.forward));
        button.overheadShot = shot;
        button.bridge = bridge.gameObject;
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(shot);
        EditorUtility.SetDirty(shotCamera);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[Bridge Camera v6] Connected button and LEFT bridge to dedicated overhead camera: " +
                  revealTransform.name + ". The Main Camera and fox were not edited.", button);
        EditorUtility.DisplayDialog("Bridge camera connected",
            "Saved the active scene without rebuilding or moving the thorns.\n\n" +
            "Press Play and step on the button. Watch for [Bridge Camera] STARTED / RETURNED logs.\n" +
            "For a direct test, select the reveal camera object and run its " +
            "'TEST - Overhead shot now (Play Mode)' context menu.", "OK");
    }
}
#endif
