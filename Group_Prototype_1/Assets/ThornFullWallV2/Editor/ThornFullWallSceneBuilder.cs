#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds an editable copy of the user's thorn scene using the actual Cube
/// and Thorn Patch authored in Tree_Level_test_narges. All new GameObjects
/// are created and saved by Unity (not by editing scene YAML IDs).
/// </summary>
[InitializeOnLoad]
public static class ThornFullWallSceneBuilder
{
    private const string TargetScene = "Assets/Scenes/narges/Thorn_FullWall_SliderBridge_v2.unity";
    private const string ReferenceScene = "Assets/Scenes/narges/Tree_Level_test_narges.unity";
    private const string Marker = "FULL WALL v2 - generated in Unity - editable";
    private const string PipeHole = "Assets/Prefabs/environment/woodenPipeWithHole.prefab";
    private const string PipeSolid = "Assets/Prefabs/environment/woodenPipe Variant.prefab";
    private const string TreeEnd = "Assets/Prefabs/environment/treeEnd.prefab";
    private const string Acorn = "Assets/Prefabs/Object/Goal Acorn.prefab";

    // Edit these two constants, then Rebuild if you want a different density.
    private const int PatchesPerLayer = 40;
    private const float ThornAxialScale = .85f;

    static ThornFullWallSceneBuilder()
    {
        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            if (scene.path != TargetScene) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SceneManager.GetActiveScene().path != TargetScene) return;
                if (GameObject.Find(Marker) == null) Build(false);
            };
        };
    }

    [MenuItem("Tools/Thorn Full Wall v2/Rebuild Current Scene")]
    public static void Rebuild() { Build(true); }

    [MenuItem("Tools/Thorn Full Wall v2/Rebuild Current Scene", true)]
    static bool CanRebuild()
    {
        return !EditorApplication.isPlaying && SceneManager.GetActiveScene().path == TargetScene;
    }

    static Transform DeepFind(Transform parent, string name)
    {
        return parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }

    static GameObject NewObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Remove(Transform parent, string name)
    {
        if (parent == null) return;
        Transform existing = parent.Find(name);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
    }

    static GameObject Prefab(string assetPath, string name, Transform parent, Vector3 localPos,
        Quaternion localRot, Vector3 localScale)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null) throw new Exception("Prefab not found: " + assetPath);
        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
        if (go == null) throw new Exception("Could not instantiate: " + assetPath);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;
        go.SetActive(true);
        return go;
    }

    static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale,
        Material material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (material != null) mr.sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void OpenRim(Transform parent, string name, float z, Material material)
    {
        Transform rim = NewObject(name + " (open centre, doesn't block travel)", parent).transform;
        const int segments = 32;
        const float radius = 11.0f;
        for (int i = 0; i < segments; i++)
        {
            float a = 360f * i / segments;
            float r = a * Mathf.Deg2Rad;
            GameObject piece = Cube("Ring edge " + (i + 1).ToString("00"), rim,
                new Vector3(radius * Mathf.Cos(r), radius * Mathf.Sin(r), z),
                new Vector3(.8f, 2.25f, .35f), material, false);
            piece.transform.localRotation = Quaternion.Euler(0, 0, a);
        }
    }

    static void Build(bool confirmation)
    {
        Scene target = SceneManager.GetActiveScene();
        if (target.path != TargetScene)
        {
            EditorUtility.DisplayDialog("Open the correct scene", "Open " + TargetScene + " first.", "OK");
            return;
        }
        if (!confirmation && GameObject.Find(Marker) != null) return;
        if (confirmation && !EditorUtility.DisplayDialog("Rebuild full-wall thorn level?",
            "Recreates this generated scene and loses manual edits here. Other scenes remain untouched.",
            "Rebuild", "Cancel")) return;

        Scene reference = default;
        try
        {
            Transform level = target.GetRootGameObjects().SelectMany(go =>
                go.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Tree Level (1)");
            if (level == null) throw new Exception("Root Tree Level (1) missing from copied scene.");
            Transform tree = level.Find("Tree (turns with the buttons)");
            Transform world = level.Find("World (does not turn)");
            if (tree == null || world == null) throw new Exception("Tree/World parent not found.");
            Transform trunk = tree.Find("Trunk");
            Transform pipes = trunk != null ? trunk.Find("modularpipes") : null;
            if (pipes == null) throw new Exception("Trunk/modularpipes missing.");
            TreeRotator rotator = tree.GetComponent<TreeRotator>();
            if (rotator == null) throw new Exception("TreeRotator missing.");

            // Load the reference scene additively only to COPY the exact authored
            // sliding cube and thorn patches. Do not alter or save that scene.
            reference = EditorSceneManager.OpenScene(ReferenceScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(target);
            GameObject[] referenceRoots = reference.GetRootGameObjects();
            Transform sourceCube = referenceRoots.SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Cube" && t.GetComponent<SlidingHoleCover>() != null);
            Transform sourcePatch = referenceRoots.SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Thorn Patch" && t.childCount >= 12);
            if (sourceCube == null || sourcePatch == null)
                throw new Exception("Sliding Cube or authored Thorn Patch missing in " + ReferenceScene);

            // Remove old objects *in this new scene only*.
            foreach (string old in new[] { "Section 0 - outer obstacle", "Section 1 - Landing",
                "Section 2 - Door On The Ceiling", "Section 3 - Timed Door",
                "Section 4 - Acorn", "Branches And Leaves", "THORNS - REMOVE PATCHES TO CARVE YOUR PATH" })
                Remove(tree, old);
            foreach (string old in new[] { "Checkpoints", "Invisible Walls", "THORN WALKWAY",
                "THORN ENTRY / EXIT TRIGGERS", "BUTTON - halfway", "BRIDGE - appears on LEFT",
                "GOAL ACORN - no interior landing" })
                Remove(world, old);
            foreach (string old in new[] { "Trunk End (start)", "Trunk End (goal)",
                "TREE END CAPS - keep travel ends open" })
                Remove(trunk, old);
            Remove(level, Marker);
            for (int i = pipes.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(pipes.GetChild(i).gameObject);
            foreach (string old in new[] { "Trunk Inside", "Trunk Bark" })
            {
                Transform oldMesh = trunk.Find(old);
                if (oldMesh != null) oldMesh.gameObject.SetActive(false);
            }

            // A pipe with the entrance hole, joined to a plain pipe; a third
            // separate pipe across a large 20-unit gap.
            Prefab(PipeHole, "01 - woodenPipeWithHole", pipes, Vector3.zero,
                Quaternion.identity, Vector3.one);
            Prefab(PipeSolid, "02 - woodenPipe plain JOINED", pipes,
                new Vector3(0, 0, 22.5f), Quaternion.Euler(90, 0, 0), Vector3.one);
            Prefab(PipeSolid, "03 - remote goal pipe (20-unit gap)", pipes,
                new Vector3(0, 0, 65f), Quaternion.Euler(90, 0, 0), Vector3.one);

            rotator.rotationMode = TreeRotator.RotationMode.Interruptible180;
            rotator.rotationAxis = TreeRotator.RotationAxis.Z;
            rotator.useRotationLimit = false;
            rotator.halfTurnDuration = 3f;

            // Copy your working cover from Tree_Level_test_narges. All its
            // materials, BoxCollider and SlidingHoleCover values are preserved.
            GameObject slider = UnityEngine.Object.Instantiate(sourceCube.gameObject, tree, false);
            slider.name = "ORIGINAL SLIDING CUBE - covers top hole";
            slider.transform.localPosition = new Vector3(.41f, 13.02f, 6.5f);
            slider.transform.localRotation = Quaternion.identity;
            slider.transform.localScale = new Vector3(3.5f, 3.5f, 3.5f);
            SlidingHoleCover cover = slider.GetComponent<SlidingHoleCover>();
            if (cover != null) cover.treeRotator = rotator;

            // Caps use the exact 'treeEnd' prefab from the exterior scene.
            // CLOSE only the two unused far ends. Caps on either side of the
            // gap would block entering/exiting, so put open decorative rims there.
            Transform caps = NewObject("TREE END CAPS - keep travel ends open", trunk).transform;
            Prefab(TreeEnd, "treeEnd - starting closed end", caps,
                new Vector3(0f, 0f, -11.33f), Quaternion.Euler(90, 0, 0),
                new Vector3(23f, .11f, 23f));
            Prefab(TreeEnd, "treeEnd - distant closed far end", caps,
                new Vector3(0f, 0f, 76.33f), Quaternion.Euler(90, 0, 0),
                new Vector3(23f, .11f, 23f));
            Material ringMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/TreeLevel/Resources/TreeLevel/Tree_Rings.mat");
            OpenRim(caps, "Joined trunk -> bridge rim", 33.75f, ringMat);
            OpenRim(caps, "Bridge -> remote trunk rim", 53.75f, ringMat);

            // DENSE wall thorns: 8 axial layers x 40 around the full inner
            // circumference. Every patch is a separate editable object.
            // Remove individual patches/groups in the Hierarchy to carve routes.
            Transform thornParent = NewObject("THORNS - REMOVE PATCHES TO CARVE YOUR PATH", tree).transform;
            float[] ringZ = { -7.4f, -1.9f, 3.6f, 9.1f, 14.6f, 20.1f, 25.6f, 31.1f };
            int count = 0;
            for (int layer = 0; layer < ringZ.Length; layer++)
            {
                Transform layerRoot = NewObject(string.Format("LAYER {0:00} - Z {1:0.0} - DELETE PATCHES HERE",
                    layer + 1, ringZ[layer]), thornParent).transform;
                for (int j = 0; j < PatchesPerLayer; j++)
                {
                    float deg = j * 360f / PatchesPerLayer;
                    float rad = deg * Mathf.Deg2Rad;
                    // Keep only a small opening under the existing slider hole
                    // so the entrance itself is not blocked by thorn geometry.
                    if (Mathf.Abs(ringZ[layer] - 6.5f) < 3.1f &&
                        Mathf.Abs(Mathf.DeltaAngle(deg, 90f)) < 17f)
                        continue;
                    GameObject patch = UnityEngine.Object.Instantiate(sourcePatch.gameObject, layerRoot, false);
                    patch.name = string.Format("Thorn Patch {0:00} @ {1:000} deg", j + 1, deg);
                    patch.transform.localPosition = new Vector3(
                        10f * Mathf.Cos(rad), 10f * Mathf.Sin(rad), ringZ[layer]);
                    patch.transform.localRotation = Quaternion.Euler(0, 0, deg + 90f);
                    patch.transform.localScale = new Vector3(1f, 1f, ThornAxialScale);
                    patch.SetActive(true);
                    count++;
                }
            }

            // Narrow inside-floor support through the connected trunks only.
            // Intentionally NO 'Landing inside remote goal pipe' is created.
            Material wood = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/TreeLevel/Resources/TreeLevel/Tree_Wood.mat");
            Transform walkway = NewObject("THORN WALKWAY", world).transform;
            Cube("Stationary inside path - connected pair only", walkway,
                new Vector3(0f, -9.34f, 12.25f), new Vector3(3.8f, .40f, 42.2f), wood, true);

            // Preserve the original player, camera, Cinemachine, and movement
            // setup exactly as copied from the source scene. Do not add lane
            // scripts, move the player, or add a camera controller.

            // A LEFT-OFFSET round bridge in the OTHERWISE EMPTY 20-unit gap.
            // The original exterior 'treeEnd' becomes horizontal (Y-up) here.
            GameObject bridge = Prefab(TreeEnd, "BRIDGE - appears on LEFT", world,
                new Vector3(-5.5f, -9.35f, 43.75f), Quaternion.identity,
                new Vector3(12f, .32f, 12f));
            bridge.SetActive(false);

            // Goal is on the curved inner floor of the distant pipe. No
            // artificial landing platform is added inside the remote pipe.
            GameObject acorn = Prefab(Acorn, "GOAL ACORN - no interior landing", world,
                new Vector3(0f, -9.25f, 65f), Quaternion.identity, Vector3.one);

            // Stationary step-on button located halfway along the tunnel.
            GameObject button = NewObject("BUTTON - halfway", world);
            button.transform.localPosition = new Vector3(0f, -9.13f, 21.5f);
            BoxCollider buttonTrigger = button.AddComponent<BoxCollider>();
            buttonTrigger.isTrigger = true;
            buttonTrigger.center = new Vector3(0f, .65f, 0f);
            buttonTrigger.size = new Vector3(2.6f, 1.3f, 2.6f);
            Rigidbody buttonBody = button.AddComponent<Rigidbody>();
            buttonBody.isKinematic = true;
            buttonBody.useGravity = false;
            Material baseMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/TreeLevel/Resources/TreeLevel/Tree_ButtonBase.mat");
            Material capMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/TreeLevel/Resources/TreeLevel/Tree_Button.mat");
            Material pressedMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/TreeLevel/Resources/TreeLevel/Tree_ButtonPressed.mat");
            Cube("Wooden button base", button.transform, Vector3.zero,
                new Vector3(2.8f, .20f, 2.8f), baseMat, true);
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Step on to reveal LEFT bridge";
            cap.transform.SetParent(button.transform, false);
            cap.transform.localPosition = new Vector3(0f, .22f, 0f);
            cap.transform.localScale = new Vector3(1.2f, .12f, 1.2f);
            Renderer capRenderer = cap.GetComponent<Renderer>();
            if (capMat != null) capRenderer.sharedMaterial = capMat;
            ThornFullBridgeButton press = button.AddComponent<ThornFullBridgeButton>();
            press.bridge = bridge;
            // Existing health is only read to restore the bridge after death.
            press.playerHealth = target.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<PlayerHealth>(true))
                .FirstOrDefault();
            press.buttonCap = cap.transform;
            press.buttonCapRenderer = capRenderer;
            press.pressedMaterial = pressedMat;

            NewObject(Marker, level);
            EditorSceneManager.CloseScene(reference, true);
            reference = default;
            SceneManager.SetActiveScene(target);
            EditorSceneManager.MarkSceneDirty(target);
            EditorSceneManager.SaveScene(target);
            Debug.Log("Thorn Full Wall v2 generated: " + count +
                " editable wall patches, original slider cube, left bridge, original player and camera preserved.");
        }
        catch (Exception e)
        {
            Debug.LogError("Thorn Full Wall v2: build failed: " + e);
            EditorUtility.DisplayDialog("Thorn full-wall scene build failed", e.Message, "OK");
        }
        finally
        {
            if (reference.IsValid() && reference.isLoaded)
                EditorSceneManager.CloseScene(reference, true);
        }
    }
}
#endif
