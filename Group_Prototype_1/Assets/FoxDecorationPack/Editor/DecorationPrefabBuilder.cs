using UnityEditor;
using UnityEngine;
using FoxDecorationPack;

[InitializeOnLoad]
public static class DecorationPrefabBuilder
{
    private static bool building;

    static DecorationPrefabBuilder()
    {
        // Prefabs are created after the editor finishes importing/compiling assets.
        EditorApplication.delayCall += GenerateIfMissing;
    }

    private static void GenerateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += GenerateIfMissing;
            return;
        }

        string[] paths = {
            "Assets/FoxDecorationPack/Prefabs/RoundTree.prefab",
            "Assets/FoxDecorationPack/Prefabs/PineTree.prefab",
            "Assets/FoxDecorationPack/Prefabs/PinkFlower.prefab",
            "Assets/FoxDecorationPack/Prefabs/YellowFlower.prefab",
            "Assets/FoxDecorationPack/Prefabs/DaisyClump.prefab"
        };
        foreach (string path in paths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.Log("[Fox Decoration Pack] Missing prefab(s), generating automatically...");
                BuildAllInternal(false);
                return;
            }
        }
    }

    private const string Root = "Assets/FoxDecorationPack";
    private const string PrefabPath = Root + "/Prefabs";
    private const string MaterialPath = Root + "/Materials";

    [MenuItem("Tools/Fox Decoration Pack/Create or Rebuild Prefabs")]
    public static void BuildAll()
    {
        BuildAllInternal(true);
    }

    private static void BuildAllInternal(bool showDialog)
    {
        if (building) return;
        building = true;
        try
        {
            EnsureFolders();

        var bark = MakeMaterial("M_Bark", new Color(0.43f, 0.28f, 0.16f));
        var leaf = MakeMaterial("M_Leaf", new Color(0.38f, 0.67f, 0.30f));
        var leafDark = MakeMaterial("M_LeafDark", new Color(0.22f, 0.48f, 0.20f));
        var flowerPink = MakeMaterial("M_FlowerPink", new Color(1.00f, 0.45f, 0.70f));
        var flowerYellow = MakeMaterial("M_FlowerYellow", new Color(0.98f, 0.82f, 0.20f));
        var flowerWhite = MakeMaterial("M_FlowerWhite", new Color(0.97f, 0.96f, 0.93f));
        var stem = MakeMaterial("M_Stem", new Color(0.23f, 0.60f, 0.27f));
        var center = MakeMaterial("M_FlowerCenter", new Color(0.95f, 0.65f, 0.16f));
        var soil = MakeMaterial("M_Soil", new Color(0.34f, 0.23f, 0.15f));

        SaveAsPrefab(BuildRoundTree(bark, leaf, leafDark), "RoundTree.prefab");
        SaveAsPrefab(BuildPineTree(bark, leafDark), "PineTree.prefab");
        SaveAsPrefab(BuildFlower("PinkFlower", stem, flowerPink, center, soil, false), "PinkFlower.prefab");
        SaveAsPrefab(BuildFlower("YellowFlower", stem, flowerYellow, center, soil, true), "YellowFlower.prefab");
        SaveAsPrefab(BuildDaisyClump(stem, flowerWhite, center, soil), "DaisyClump.prefab");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Fox Decoration Pack] SUCCESS: Five prefabs created in " + PrefabPath);
            if (showDialog)
                EditorUtility.DisplayDialog("Fox Decoration Pack", "5 prefabs generated. Look in Assets/FoxDecorationPack/Prefabs", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[Fox Decoration Pack] FAILED while generating prefabs: " + ex);
            if (showDialog)
                EditorUtility.DisplayDialog("Fox Decoration Pack", "Generation failed. See the first RED Console error for details.", "OK");
        }
        finally
        {
            building = false;
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/FoxDecorationPack"))
            AssetDatabase.CreateFolder("Assets", "FoxDecorationPack");
        if (!AssetDatabase.IsValidFolder(MaterialPath))
            AssetDatabase.CreateFolder(Root, "Materials");
        if (!AssetDatabase.IsValidFolder(PrefabPath))
            AssetDatabase.CreateFolder(Root, "Prefabs");
    }

    private static Material MakeMaterial(string name, Color color)
    {
        string path = MaterialPath + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = color;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
            throw new System.Exception("No compatible material shader found.");
        var mat = new Material(shader);
        mat.name = name;
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void SaveAsPrefab(GameObject go, string fileName)
    {
        string path = PrefabPath + "/" + fileName;
        PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
    }

    private static GameObject BuildRoundTree(Material bark, Material leaf, Material leafDark)
    {
        var root = new GameObject("RoundTree");

        var trunk = Primitive(PrimitiveType.Cylinder, "Trunk", root.transform, bark,
            new Vector3(0f, 1.5f, 0f), new Vector3(0.6f, 1.5f, 0.6f));

        var c1 = Primitive(PrimitiveType.Sphere, "CanopyA", root.transform, leaf,
            new Vector3(0f, 3.4f, 0f), new Vector3(2.2f, 1.8f, 2.2f));
        var c2 = Primitive(PrimitiveType.Sphere, "CanopyB", root.transform, leafDark,
            new Vector3(-0.75f, 3.1f, 0.35f), new Vector3(1.5f, 1.4f, 1.5f));
        var c3 = Primitive(PrimitiveType.Sphere, "CanopyC", root.transform, leafDark,
            new Vector3(0.9f, 3.0f, -0.2f), new Vector3(1.55f, 1.35f, 1.55f));
        var c4 = Primitive(PrimitiveType.Sphere, "CanopyTop", root.transform, leaf,
            new Vector3(0.1f, 4.0f, 0.15f), new Vector3(1.45f, 1.15f, 1.45f));

        UnityEngine.Object.DestroyImmediate(trunk.GetComponent<Collider>());
        UnityEngine.Object.DestroyImmediate(c1.GetComponent<Collider>());
        UnityEngine.Object.DestroyImmediate(c2.GetComponent<Collider>());
        UnityEngine.Object.DestroyImmediate(c3.GetComponent<Collider>());
        UnityEngine.Object.DestroyImmediate(c4.GetComponent<Collider>());

        var sway = c1.AddComponent<GentleSway>();
        SerializedObject so = new SerializedObject(sway);
        so.FindProperty("angle").floatValue = 1.5f;
        so.FindProperty("speed").floatValue = 0.7f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 2.1f, 0f);
        col.height = 4.2f;
        col.radius = 1.0f;

        return root;
    }

    private static GameObject BuildPineTree(Material bark, Material leaf)
    {
        var root = new GameObject("PineTree");

        var trunk = Primitive(PrimitiveType.Cylinder, "Trunk", root.transform, bark,
            new Vector3(0f, 1.8f, 0f), new Vector3(0.45f, 1.8f, 0.45f));
        UnityEngine.Object.DestroyImmediate(trunk.GetComponent<Collider>());

        var l1 = Primitive(PrimitiveType.Cylinder, "Leaves1", root.transform, leaf,
            new Vector3(0f, 2.3f, 0f), new Vector3(2.2f, 0.7f, 2.2f));
        var l2 = Primitive(PrimitiveType.Cylinder, "Leaves2", root.transform, leaf,
            new Vector3(0f, 3.2f, 0f), new Vector3(1.8f, 0.65f, 1.8f));
        var l3 = Primitive(PrimitiveType.Cylinder, "Leaves3", root.transform, leaf,
            new Vector3(0f, 4.0f, 0f), new Vector3(1.35f, 0.6f, 1.35f));
        var top = Primitive(PrimitiveType.Cylinder, "LeavesTop", root.transform, leaf,
            new Vector3(0f, 4.75f, 0f), new Vector3(0.9f, 0.5f, 0.9f));

        foreach (var g in new[] { l1, l2, l3, top })
        {
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            var s = g.AddComponent<GentleSway>();
            SerializedObject so = new SerializedObject(s);
            so.FindProperty("angle").floatValue = 1.3f;
            so.FindProperty("speed").floatValue = 0.8f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        l1.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        l2.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        l3.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        top.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 2.4f, 0f);
        col.height = 5.0f;
        col.radius = 1.1f;

        return root;
    }

    private static GameObject BuildFlower(string name, Material stem, Material petal, Material centerMat, Material soil, bool tulipLike)
    {
        var root = new GameObject(name);

        var patch = Primitive(PrimitiveType.Cylinder, "Patch", root.transform, soil,
            new Vector3(0f, 0.05f, 0f), new Vector3(0.8f, 0.05f, 0.8f));
        UnityEngine.Object.DestroyImmediate(patch.GetComponent<Collider>());

        var stemGo = Primitive(PrimitiveType.Cylinder, "Stem", root.transform, stem,
            new Vector3(0f, 0.65f, 0f), new Vector3(0.11f, 0.65f, 0.11f));
        UnityEngine.Object.DestroyImmediate(stemGo.GetComponent<Collider>());
        var stemSway = stemGo.AddComponent<GentleSway>();
        SerializedObject stemSO = new SerializedObject(stemSway);
        stemSO.FindProperty("angle").floatValue = 3f;
        stemSO.FindProperty("speed").floatValue = 1.6f;
        stemSO.ApplyModifiedPropertiesWithoutUndo();

        var leafA = Primitive(PrimitiveType.Cube, "LeafA", stemGo.transform, stem,
            new Vector3(0.17f, 0.28f, 0f), new Vector3(0.28f, 0.06f, 0.16f));
        leafA.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
        var leafB = Primitive(PrimitiveType.Cube, "LeafB", stemGo.transform, stem,
            new Vector3(-0.17f, 0.46f, 0f), new Vector3(0.28f, 0.06f, 0.16f));
        leafB.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
        UnityEngine.Object.DestroyImmediate(leafA.GetComponent<Collider>());
        UnityEngine.Object.DestroyImmediate(leafB.GetComponent<Collider>());

        if (tulipLike)
        {
            var bud = Primitive(PrimitiveType.Sphere, "Bud", stemGo.transform, petal,
                new Vector3(0f, 0.78f, 0f), new Vector3(0.38f, 0.5f, 0.38f));
            UnityEngine.Object.DestroyImmediate(bud.GetComponent<Collider>());

            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f;
                var pet = Primitive(PrimitiveType.Cube, "Petal" + i, bud.transform, petal,
                    Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.18f, 0.15f),
                    new Vector3(0.12f, 0.28f, 0.2f));
                pet.transform.localRotation *= Quaternion.Euler(-15f, 0f, 0f);
                UnityEngine.Object.DestroyImmediate(pet.GetComponent<Collider>());
            }
        }
        else
        {
            var center = Primitive(PrimitiveType.Sphere, "Center", stemGo.transform, centerMat,
                new Vector3(0f, 0.82f, 0f), new Vector3(0.16f, 0.16f, 0.16f));
            UnityEngine.Object.DestroyImmediate(center.GetComponent<Collider>());

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f;
                Vector3 pos = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.15f, 0.82f, 0f);
                var pet = Primitive(PrimitiveType.Sphere, "Petal" + i, stemGo.transform, petal,
                    pos, new Vector3(0.16f, 0.07f, 0.22f));
                pet.transform.LookAt(center.transform.position);
                pet.transform.Rotate(90f, 0f, 0f, Space.Self);
                UnityEngine.Object.DestroyImmediate(pet.GetComponent<Collider>());
            }
        }

        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.7f, 0f);
        col.height = 1.5f;
        col.radius = 0.35f;
        col.direction = 1;

        return root;
    }

    private static GameObject BuildDaisyClump(Material stem, Material petals, Material centerMat, Material soil)
    {
        var root = new GameObject("DaisyClump");
        var patch = Primitive(PrimitiveType.Cylinder, "Patch", root.transform, soil,
            new Vector3(0f, 0.04f, 0f), new Vector3(1.0f, 0.04f, 1.0f));
        UnityEngine.Object.DestroyImmediate(patch.GetComponent<Collider>());

        Vector3[] positions = {
            new Vector3(-0.25f, 0f, -0.15f), new Vector3(0.15f, 0f, 0.18f), new Vector3(0.3f, 0f, -0.2f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var stemRoot = new GameObject("Flower" + i);
            stemRoot.transform.SetParent(root.transform, false);
            stemRoot.transform.localPosition = positions[i];
            stemRoot.transform.localRotation = Quaternion.Euler(0f, i * 30f, 0f);

            var s = Primitive(PrimitiveType.Cylinder, "Stem", stemRoot.transform, stem,
                new Vector3(0f, 0.5f + i * 0.06f, 0f), new Vector3(0.06f, 0.5f + i * 0.06f, 0.06f));
            UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
            var sway = stemRoot.AddComponent<GentleSway>();
            SerializedObject so = new SerializedObject(sway);
            so.FindProperty("angle").floatValue = 3.5f;
            so.FindProperty("speed").floatValue = 1.4f + i * 0.15f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var ctr = Primitive(PrimitiveType.Sphere, "Center", stemRoot.transform, centerMat,
                new Vector3(0f, 1.08f + i * 0.06f, 0f), new Vector3(0.12f, 0.12f, 0.12f));
            UnityEngine.Object.DestroyImmediate(ctr.GetComponent<Collider>());

            for (int p = 0; p < 8; p++)
            {
                float ang = p * 45f;
                Vector3 pos = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.16f, 1.08f + i * 0.06f, 0f);
                var pet = Primitive(PrimitiveType.Sphere, "Petal" + p, stemRoot.transform, petals,
                    pos, new Vector3(0.14f, 0.05f, 0.18f));
                pet.transform.LookAt(ctr.transform.position);
                pet.transform.Rotate(90f, 0f, 0f, Space.Self);
                UnityEngine.Object.DestroyImmediate(pet.GetComponent<Collider>());
            }
        }

        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.65f, 0f);
        col.height = 1.5f;
        col.radius = 0.6f;
        return root;
    }

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat, Vector3 localPos, Vector3 localScale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
        return go;
    }
}
