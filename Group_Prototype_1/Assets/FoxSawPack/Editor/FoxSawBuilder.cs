using UnityEditor;
using UnityEngine;
using FoxSawPack;

public static class FoxSawBuilder
{
    private const string R = "Assets/FoxSawPack";
    [MenuItem("Tools/Fox Saw Pack/Create or Rebuild Saw Prefabs")]
    public static void Generate()
    {
        Ensure("Materials"); Ensure("Meshes"); Ensure("Prefabs");
        var steel = Material("Steel", new Color(0.40f, 0.46f, 0.51f));
        var dark = Material("DarkMetal", new Color(0.14f, 0.19f, 0.24f));
        var edge = Material("SawBlade", new Color(0.72f, 0.79f, 0.82f));
        var yellow = Material("WarningYellow", new Color(1.0f, 0.63f, 0.1f));
        var red = Material("WarningRed", new Color(0.95f, 0.16f, 0.12f));

        CreatePrefab(false, steel, dark, edge, yellow, red);
        CreatePrefab(true, steel, dark, edge, yellow, red);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FoxSawPack] Created FixedSaw.prefab and MovingSaw.prefab in Assets/FoxSawPack/Prefabs");
        EditorUtility.DisplayDialog("Fox Saw Pack", "FixedSaw and MovingSaw generated in Assets/FoxSawPack/Prefabs", "OK");
    }

    private static void Ensure(string folder)
    {
        if (!AssetDatabase.IsValidFolder(R + "/" + folder))
            AssetDatabase.CreateFolder(R, folder);
    }

    private static Material Material(string name, Color color)
    {
        string path = R + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        mat = new Material(shader);
        mat.name = name;
        mat.color = color;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Mesh SawMesh()
    {
        string path = R + "/Meshes/SawBladeMesh.asset";
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved != null) return saved;

        // 20 toothed radial segments; two triangles per tooth, front and back.
        const int teeth = 20;
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        const float root = 0.72f, tip = 0.96f;
        for (int i = 0; i < teeth; i++)
        {
            float a0 = i * Mathf.PI * 2f / teeth;
            float a1 = (i + 0.36f) * Mathf.PI * 2f / teeth;
            float a2 = (i + 1f) * Mathf.PI * 2f / teeth;
            // A triangular sawtooth connected to the center forms a robust flat mesh.
            AddTriangle(verts, tris, Vector3.zero, Polar(root, a0), Polar(tip, a1));
            AddTriangle(verts, tris, Vector3.zero, Polar(tip, a1), Polar(root, a2));
        }
        Mesh mesh = new Mesh { name = "SawBladeMesh" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Vector3 Polar(float radius, float radians)
    {
        return new Vector3(radius * Mathf.Cos(radians), radius * Mathf.Sin(radians), 0f);
    }

    private static void AddTriangle(System.Collections.Generic.List<Vector3> vertices,
        System.Collections.Generic.List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        triangles.Add(start); triangles.Add(start+1); triangles.Add(start+2);
        // Back face for visibility from both sides.
        triangles.Add(start+2); triangles.Add(start+1); triangles.Add(start);
    }

    private static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool solid = false)
    {
        GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.name = name;
        o.transform.SetParent(parent, false);
        o.transform.localPosition = pos;
        o.transform.localScale = scale;
        o.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!solid) UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
        return o;
    }

    private static void CreatePrefab(bool moving, Material steel, Material dark, Material edge, Material yellow, Material red)
    {
        string name = moving ? "MovingSaw" : "FixedSaw";
        GameObject root = new GameObject(name);
        try
        {
            SawTrap controller = root.AddComponent<SawTrap>();
            controller.moving = moving;
            controller.travelDistance = 6f;
            controller.travelSpeed = 2.5f;
            controller.spinDegreesPerSecond = 420f;
            controller.damage = 1f;
            controller.hitCooldown = 1f;

            // Wide, slim track is only decorative; the moving carriage is the animated part.
            if (moving)
            {
                Cube("Rail_Upper", root.transform, new Vector3(3f, 1.85f, 0.1f), new Vector3(7.4f, 0.14f, 0.22f), dark);
                Cube("Rail_Lower", root.transform, new Vector3(3f, 0.14f, 0.1f), new Vector3(7.4f, 0.14f, 0.22f), dark);
                Cube("TrackLeft", root.transform, new Vector3(-0.65f, 1f, 0.1f), new Vector3(0.2f, 2.0f, 0.30f), steel);
                Cube("TrackRight", root.transform, new Vector3(6.65f, 1f, 0.1f), new Vector3(0.2f, 2.0f, 0.30f), steel);
            }

            var carriage = new GameObject("Carriage");
            carriage.transform.SetParent(root.transform, false);
            controller.movingCarriage = carriage.transform;

            Cube("Base", carriage.transform, new Vector3(0, 0.17f, 0.12f), new Vector3(1.45f, 0.34f, 0.80f), dark, true);
            Cube("BasePlate", carriage.transform, new Vector3(0, 0.34f, 0.12f), new Vector3(1.15f, 0.12f, 0.68f), steel);
            Cube("StrutLeft", carriage.transform, new Vector3(-0.70f, 0.73f, 0.15f), new Vector3(0.18f, 0.90f, 0.25f), steel);
            Cube("StrutRight", carriage.transform, new Vector3(0.70f, 0.73f, 0.15f), new Vector3(0.18f, 0.90f, 0.25f), steel);
            Cube("CautionLeft", carriage.transform, new Vector3(-0.70f, 0.58f, -0.02f), new Vector3(0.19f, 0.16f, 0.04f), yellow);
            Cube("CautionRight", carriage.transform, new Vector3(0.70f, 0.58f, -0.02f), new Vector3(0.19f, 0.16f, 0.04f), yellow);
            Cube("Indicator", carriage.transform, new Vector3(0, 0.43f, -0.31f), new Vector3(0.22f, 0.09f, 0.04f), red);

            GameObject blade = new GameObject("Blade");
            blade.transform.SetParent(carriage.transform, false);
            blade.transform.localPosition = new Vector3(0f, 1.30f, -0.20f);

            var spinVisual = new GameObject("SpinVisual");
            spinVisual.transform.SetParent(blade.transform, false);
            controller.spinningBlade = spinVisual.transform;

            var bladeFace = new GameObject("ToothedDisk");
            bladeFace.transform.SetParent(spinVisual.transform, false);
            bladeFace.AddComponent<MeshFilter>().sharedMesh = SawMesh();
            bladeFace.AddComponent<MeshRenderer>().sharedMaterial = edge;

            // Central pivot disc: cylinder primitive's axis is Y, turn it to point along Z.
            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            hub.transform.SetParent(spinVisual.transform, false);
            hub.transform.localPosition = new Vector3(0, 0, -0.06f);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hub.transform.localScale = new Vector3(0.20f, 0.08f, 0.20f);
            hub.GetComponent<MeshRenderer>().sharedMaterial = dark;
            UnityEngine.Object.DestroyImmediate(hub.GetComponent<Collider>());

            var trigger = blade.AddComponent<SphereCollider>();
            trigger.radius = 0.82f;
            trigger.isTrigger = true;
            var rb = blade.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            blade.AddComponent<SawDamageZone>();

            string path = R + "/Prefabs/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
