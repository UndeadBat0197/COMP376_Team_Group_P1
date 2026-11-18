#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Repairs the existing button/bridge/cutaway links IN PLACE.
/// Does not rebuild the scene, touch thorn objects, player movement,
/// Main Camera, or Cinemachine.
/// </summary>
public static class ThornButtonConnectionRepair
{
    const string ScenePath = "Assets/Scenes/narges/Thorn_FullWall_SliderBridge_v2.unity";
    const string ButtonName = "BUTTON - halfway";
    const string BridgeName = "BRIDGE - appears on LEFT";
    const string CameraName = "BRIDGE REVEAL CAMERA - temporary overhead shot";

    [MenuItem("Tools/Thorn Full Wall v2/Fix Button + Bridge Reveal (Keep Level)")]
    public static void Repair()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Open the existing thorn scene",
                "Open " + ScenePath + " first. This fix will not change any other scene.", "OK");
            return;
        }

        Transform[] all = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .ToArray();
        Transform world = all.FirstOrDefault(t => t.name == "World (does not turn)");
        Transform buttonTransform = all.FirstOrDefault(t => t.name == ButtonName);
        Transform bridge = all.FirstOrDefault(t => t.name == BridgeName);
        Camera originalCamera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(c => c.CompareTag("MainCamera"));

        if (world == null || buttonTransform == null || bridge == null || originalCamera == null)
        {
            EditorUtility.DisplayDialog("Missing objects",
                "Could not find World, BUTTON - halfway, BRIDGE - appears on LEFT, or Main Camera. " +
                "No changes were made. If you renamed one, rename it temporarily to its generated name.", "OK");
            Debug.LogError("Button repair could not locate the required scene objects.");
            return;
        }

        // Keep existing object; no replacement and no rebuilding other geometry.
        Undo.RecordObject(buttonTransform.gameObject, "Activate existing button");
        buttonTransform.gameObject.SetActive(true);
        BoxCollider trigger = buttonTransform.GetComponent<BoxCollider>();
        if (trigger == null) trigger = Undo.AddComponent<BoxCollider>(buttonTransform.gameObject);
        Undo.RecordObject(trigger, "Make button trigger reachable");
        trigger.enabled = true;
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.75f, 0f);
        trigger.size = new Vector3(3.0f, 2.2f, 3.0f);

        Rigidbody rb = buttonTransform.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(buttonTransform.gameObject);
        Undo.RecordObject(rb, "Configure trigger Rigidbody");
        rb.isKinematic = true;
        rb.useGravity = false;

        // Ensure bridge is on the LEFT of the gap and belongs to stationary World.
        Undo.RecordObject(bridge, "Place bridge on the left");
        bridge.SetParent(world, true);
        bridge.localPosition = new Vector3(-5.5f, -9.35f, 43.75f);
        bridge.gameObject.SetActive(false);

        ThornFullBridgeButton button = buttonTransform.GetComponent<ThornFullBridgeButton>();
        if (button == null) button = Undo.AddComponent<ThornFullBridgeButton>(buttonTransform.gameObject);
        Undo.RecordObject(button, "Reconnect bridge button");
        button.enabled = true;
        button.bridge = bridge.gameObject;
        button.activationRadius = 1.45f;
        button.feetBelow = .65f;
        button.feetAbove = 1.15f;

        CharacterController player = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<CharacterController>(true))
            .FirstOrDefault();
        button.playerController = player;
        if (player != null) button.playerHealth = player.GetComponent<PlayerHealth>();
        else Debug.LogWarning("Button repair: CharacterController not found; use the trigger or assign the fox controller manually.");

        Transform cap = buttonTransform.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "Step on to reveal LEFT bridge");
        if (cap != null)
        {
            button.buttonCap = cap;
            button.buttonCapRenderer = cap.GetComponent<Renderer>();
        }

        // An independent temporary camera - NEVER alter the existing Main Camera.
        Transform shotT = world.Find(CameraName);
        if (shotT == null)
        {
            var go = new GameObject(CameraName);
            Undo.RegisterCreatedObjectUndo(go, "Add independent overhead camera");
            shotT = go.transform;
            shotT.SetParent(world, false);
        }
        shotT.gameObject.SetActive(true);
        Camera temp = shotT.GetComponent<Camera>();
        if (temp == null) temp = Undo.AddComponent<Camera>(shotT.gameObject);
        UniversalAdditionalCameraData urpData =
            shotT.GetComponent<UniversalAdditionalCameraData>();
        if (urpData == null) urpData = Undo.AddComponent<UniversalAdditionalCameraData>(shotT.gameObject);
        urpData.renderType = CameraRenderType.Base;
        temp.depth = originalCamera.depth + 50f;
        temp.cullingMask = originalCamera.cullingMask;
        temp.clearFlags = CameraClearFlags.Skybox;
        temp.nearClipPlane = 0.1f;
        temp.farClipPlane = 500f;
        temp.orthographic = false;
        temp.fieldOfView = 49f;
        temp.enabled = false;

        ThornBridgeOverheadShot shot = shotT.GetComponent<ThornBridgeOverheadShot>();
        if (shot == null) shot = Undo.AddComponent<ThornBridgeOverheadShot>(shotT.gameObject);
        Undo.RecordObject(shot, "Connect independent camera shot");
        shot.enabled = true;
        shot.normalCamera = originalCamera;
        shot.overheadHeight = 53f;
        shot.overheadFieldOfView = 49f;
        shot.travelSeconds = .75f;
        shot.holdSeconds = 2.5f;
        shot.lookTargetOffset = new Vector3(3f, 0f, 0f);
        Vector3 focus = bridge.position + shot.lookTargetOffset;
        shotT.SetPositionAndRotation(focus + Vector3.up * shot.overheadHeight,
            Quaternion.LookRotation(Vector3.down, Vector3.forward));
        button.overheadShot = shot;

        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(rb);
        EditorUtility.SetDirty(shot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("THORN BUTTON REPAIRED: refs assigned, trigger widened, " +
            "fox proximity fallback enabled, bridge positioned at left gap, " +
            "independent overhead camera connected. Fox and Main Camera unchanged.", button);
        EditorUtility.DisplayDialog("Button repair complete",
            "Reconnected existing button + bridge + temporary overhead camera. " +
            "The fox and Main Camera were not changed. Enter Play mode, step onto the button, " +
            "and check the Console for 'THORN BUTTON PRESSED'.", "OK");
    }
}
#endif
