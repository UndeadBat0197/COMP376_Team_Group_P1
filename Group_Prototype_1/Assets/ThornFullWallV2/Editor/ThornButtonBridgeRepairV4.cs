#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Repairs connections in the CURRENT OPEN SCENE only. Does not regenerate
/// geometry, delete thorn edits, or modify the fox or Main Camera.
/// </summary>
public static class ThornButtonBridgeRepairV4
{
    [MenuItem("Tools/Thorn Full Wall v2/Reconnect Button + LEFT Bridge (v4)")]
    public static void Reconnect()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("No scene open", "Open your thorn scene first.", "OK");
            return;
        }

        Transform[] transforms = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .ToArray();

        Transform world = transforms.FirstOrDefault(t => t.name == "World (does not turn)");
        Transform buttonT = transforms.FirstOrDefault(t => t.name == "BUTTON - halfway");
        Transform bridgeT = transforms.FirstOrDefault(t => t.name == "BRIDGE - appears on LEFT");
        Camera main = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(c => c.CompareTag("MainCamera"));

        if (world == null || buttonT == null || bridgeT == null || main == null)
        {
            string missing = (world == null ? "World (does not turn); " : "") +
                             (buttonT == null ? "BUTTON - halfway; " : "") +
                             (bridgeT == null ? "BRIDGE - appears on LEFT; " : "") +
                             (main == null ? "Main Camera; " : "");
            Debug.LogError("[Bridge Repair v4] Required objects missing: " + missing);
            EditorUtility.DisplayDialog("Objects not found", "No changes made. Missing: " + missing, "OK");
            return;
        }

        // The platform is kept on the LEFT of the gap, in stationary-world coordinates.
        Undo.RecordObject(bridgeT, "Position LEFT gap platform");
        bridgeT.SetParent(world, true);
        bridgeT.localPosition = new Vector3(-5.5f, -9.35f, 43.75f);
        Undo.RecordObject(bridgeT.gameObject, "Hide bridge before button press");
        bridgeT.gameObject.SetActive(false);

        // Expand the invisible button hitbox to cover its visible cap and
        // the fox capsule even when Unity doesn't deliver trigger callbacks.
        Undo.RecordObject(buttonT.gameObject, "Enable existing button");
        buttonT.gameObject.SetActive(true);
        BoxCollider box = buttonT.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(buttonT.gameObject);
        Undo.RecordObject(box, "Repair button trigger volume");
        box.enabled = true;
        box.isTrigger = true;
        box.center = new Vector3(0f, 0.65f, 0f);
        box.size = new Vector3(4.8f, 2.4f, 4.8f);

        Rigidbody body = buttonT.GetComponent<Rigidbody>();
        if (body == null) body = Undo.AddComponent<Rigidbody>(buttonT.gameObject);
        Undo.RecordObject(body, "Configure stationary button Rigidbody");
        body.isKinematic = true;
        body.useGravity = false;

        ThornFullBridgeButton button = buttonT.GetComponent<ThornFullBridgeButton>();
        if (button == null) button = Undo.AddComponent<ThornFullBridgeButton>(buttonT.gameObject);
        Undo.RecordObject(button, "Link fox to bridge and reveal");
        button.enabled = true;
        button.bridge = bridgeT.gameObject;
        button.useBoundsFallback = true;
        button.proximityRadius = 2.1f;
        button.feetHeightTolerance = 2.0f;

        CharacterController fox = scene.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<CharacterController>(true))
            .FirstOrDefault();
        button.playerController = fox;
        button.playerHealth = fox != null ? fox.GetComponent<PlayerHealth>() : null;

        Transform cap = buttonT.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "Step on to reveal LEFT bridge");
        if (cap != null)
        {
            button.buttonCap = cap;
            button.buttonCapRenderer = cap.GetComponent<Renderer>();
        }

        const string shotName = "BRIDGE REVEAL CAMERA - temporary overhead shot";
        Transform shotT = transforms.FirstOrDefault(t => t.name == shotName);
        if (shotT == null)
        {
            GameObject go = new GameObject(shotName);
            Undo.RegisterCreatedObjectUndo(go, "Create independent bridge reveal camera");
            shotT = go.transform;
            shotT.SetParent(world, false);
        }
        Undo.RecordObject(shotT.gameObject, "Enable bridge reveal object");
        shotT.gameObject.SetActive(true);
        Camera shotCam = shotT.GetComponent<Camera>();
        if (shotCam == null) shotCam = Undo.AddComponent<Camera>(shotT.gameObject);
        UniversalAdditionalCameraData urp = shotT.GetComponent<UniversalAdditionalCameraData>();
        if (urp == null) urp = Undo.AddComponent<UniversalAdditionalCameraData>(shotT.gameObject);
        urp.renderType = CameraRenderType.Base;
        shotCam.depth = main.depth + 100f;
        shotCam.cullingMask = main.cullingMask;
        shotCam.clearFlags = CameraClearFlags.Skybox;
        shotCam.nearClipPlane = 0.05f;
        shotCam.farClipPlane = 500f;
        shotCam.fieldOfView = 49f;
        shotCam.enabled = false;

        ThornBridgeOverheadShot shot = shotT.GetComponent<ThornBridgeOverheadShot>();
        if (shot == null) shot = Undo.AddComponent<ThornBridgeOverheadShot>(shotT.gameObject);
        Undo.RecordObject(shot, "Configure bridge shot");
        shot.enabled = true;
        shot.normalCamera = main;
        shot.overheadHeight = 45f;
        shot.overheadFieldOfView = 49f;
        shot.travelSeconds = 0.6f;
        shot.holdSeconds = 2.4f;
        shot.lookTargetOffset = new Vector3(3f, 0f, 0f);
        button.overheadShot = shot;

        Vector3 focus = bridgeT.position + shot.lookTargetOffset;
        shotT.SetPositionAndRotation(
            focus + Vector3.up * shot.overheadHeight,
            Quaternion.LookRotation(Vector3.down, Vector3.forward));

        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(box);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(shot);
        EditorUtility.SetDirty(shotCam);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string result = "[Bridge Repair v4] Connected: " +
            "button='" + buttonT.name + "', bridge='" + bridgeT.name +
            "', fox=" + (fox != null ? fox.name : "MISSING") +
            ", overhead='" + shotT.name + "'. " +
            "Left bridge location=" + bridgeT.position + ". " +
            "No modifications to fox movement, Main Camera, Cinemachine, or thorns.";
        Debug.Log(result, button);
        EditorUtility.DisplayDialog("Button and overhead bridge connected",
            "Saved current scene. The button uses both trigger events and fox-capsule overlap.\n\n" +
            "1. Enter Play mode and walk onto the button.\n" +
            "2. Check Console for '[Thorn Button] ACTIVATED'.\n" +
            "3. If still not working, right-click Thorn Full Bridge Button header in Inspector and run " +
            "'TEST - Reveal Bridge + Camera (Play Mode)'.", "OK");
    }
}
#endif
