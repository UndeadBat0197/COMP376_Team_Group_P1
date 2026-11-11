using System;
using UnityEditor;
using UnityEngine;
using FoxTerrainPack;

[InitializeOnLoad]
public static class FoxTerrainBuilder
{
    const string Base = "Assets/FoxTerrainPack";
    const string Prefabs = Base + "/Prefabs";
    const string Materials = Base + "/Materials";
    const string Meshes = Base + "/Meshes";
    const float R = 18f;

    static FoxTerrainBuilder()
    {
        EditorApplication.delayCall += AutoBuildOnce;
    }

    static void AutoBuildOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoBuildOnce;
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/GrassLedge.prefab") == null)
        {
            Debug.Log("[FoxTerrainPack] First import: generating terrain prefabs.");
            try { BuildAllInternal(); }
            catch (Exception ex) { Debug.LogError("[FoxTerrainPack] Generate failed: " + ex); }
        }
    }

    [MenuItem("Tools/Fox Terrain Pack/Generate or Rebuild Terrain Prefabs")]
    public static void BuildAll()
    {
        try
        {
            BuildAllInternal();
            EditorUtility.DisplayDialog("Fox Terrain Pack", "Created 9 terrain prefabs in Assets/FoxTerrainPack/Prefabs", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Fox Terrain Pack", "Generation failed. See Console for details.", "OK");
        }
    }

    static void BuildAllInternal()
    {
        EnsureFolder(Base, "Assets", "FoxTerrainPack");
        EnsureFolder(Prefabs, Base, "Prefabs");
        EnsureFolder(Materials, Base, "Materials");
        EnsureFolder(Meshes, Base, "Meshes");

        Material grass = Mat("Grass", new Color(.29f, .62f, .32f));
        Material grassLight = Mat("GrassLight", new Color(.42f, .75f, .36f));
        Material soil = Mat("Soil", new Color(.39f, .27f, .17f));
        Material rock = Mat("Rock", new Color(.47f, .51f, .52f));
        Material rockLight = Mat("RockLight", new Color(.59f, .64f, .64f));
        Material wood = Mat("Wood", new Color(.49f, .31f, .17f));
        Material woodLight = Mat("WoodLight", new Color(.68f, .47f, .28f));
        Material metal = Mat("Metal", new Color(.31f, .39f, .48f));
        Material metalLight = Mat("MetalLight", new Color(.53f, .62f, .71f));
        Material dark = Mat("DarkMetal", new Color(.16f, .21f, .29f));
        Material cyan = Mat("Cyan", new Color(.08f, .85f, .94f), true);
        Material orange = Mat("Orange", new Color(1.0f, .52f, .12f), true);

        BuildCurved("GrassLedge", 9f, 5f, 1.15f, false, grass, soil, grassLight, false);
        BuildCurved("MossyCliff", 6f, 4f, 2f, false, grass, rock, rockLight, true);
        BuildCurved("RockStep", 3.5f, 3f, .85f, false, rockLight, rock, rock, false);
        BuildWoodBridge(wood, woodLight, dark);
        BuildRamp("ForestRamp", 7f, 4f, 2.5f, false, grass, soil, wood);
        BuildCurved("SciFiCatwalk", 9f, 3.5f, .65f, true, metalLight, dark, cyan, false);
        BuildCurved("MetalPlatform", 6f, 5f, 1.2f, true, metal, dark, metalLight, false);
        BuildCurved("EnergyPlatform", 5f, 4f, 1.1f, true, dark, metal, cyan, false);
        BuildRamp("TechRamp", 7f, 4f, 2.5f, true, metal, dark, orange);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FoxTerrainPack] SUCCESS: 9 prefabs generated in " + Prefabs);
    }

    static void EnsureFolder(string target, string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(target)) AssetDatabase.CreateFolder(parent, name);
    }

    static Material Mat(string name, Color c, bool emission = false)
    {
        string path = Materials + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Sprites/Default");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (emission)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 1.5f);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Material m, Vector3 pos, Vector3 scale, Vector3 rot)
    {
        GameObject o = GameObject.CreatePrimitive(type);
        o.name = name;
        o.transform.SetParent(parent, false);
        o.transform.localPosition = pos;
        o.transform.localRotation = Quaternion.Euler(rot);
        o.transform.localScale = scale;
        Renderer rend = o.GetComponent<Renderer>();
        if (rend) rend.sharedMaterial = m;
        Collider collider = o.GetComponent<Collider>();
        if (collider) UnityEngine.Object.DestroyImmediate(collider);
        return o;
    }

    static GameObject BaseObject(string name, bool inside)
    {
        var root = new GameObject(name);
        var placement = root.AddComponent<CylinderSurfacePlacement>();
        placement.insideSurface = inside;
        placement.cylinderRadius = R;
        return root;
    }

    // Curved terrace; x follows tangent to cylinder, y is outward normal, z is cylinder axis.
    // When inside=true the curvature turns inward. Every terrace has a real MeshCollider.
    static Mesh MakeCurvedMesh(float width, float depth, float thick, bool inside)
    {
        const int N = 24;
        float halfAngle = Mathf.Min(1.3f, width / (2f * R));
        Vector3[] verts = new Vector3[(N + 1) * 4];
        for (int i = 0; i <= N; i++)
        {
            float a = Mathf.Lerp(-halfAngle, halfAngle, i);
            float x = R * Mathf.Sin(a);
            float y = (inside ? -1f : 1f) * R * (Mathf.Cos(a) - 1f);
            int j = 4 * i;
            verts[j] = new Vector3(x, y, -depth * .5f);
            verts[j + 1] = new Vector3(x, y, depth * .5f);
            verts[j + 2] = new Vector3(x, y - thick, -depth * .5f);
            verts[j + 3] = new Vector3(x, y - thick, depth * .5f);
        }
        var top = new System.Collections.Generic.List<int>();
        var sides = new System.Collections.Generic.List<int>();
        for (int i = 0; i < N; i++)
        {
            int a = i * 4, b = (i + 1) * 4;
            AddQuad(top, a, b, b + 1, a + 1);
            AddQuad(sides, a + 2, a + 3, b + 3, b + 2);
            AddQuad(sides, a, a + 2, b + 2, b);
            AddQuad(sides, a + 1, b + 1, b + 3, a + 3);
        }
        AddQuad(sides, 0, 1, 3, 2);
        int last = N * 4;
        AddQuad(sides, last, last + 2, last + 3, last + 1);
        Mesh mesh = new Mesh();
        mesh.name = "CurvedTerrainMesh";
        mesh.vertices = verts;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(top, 0);
        mesh.SetTriangles(sides, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddQuad(System.Collections.Generic.List<int> tris, int a, int b, int c, int d)
    {
        tris.Add(a); tris.Add(b); tris.Add(c);
        tris.Add(a); tris.Add(c); tris.Add(d);
    }

    static void StoreMeshAndAttach(GameObject root, Mesh mesh, string name, Material m0, Material m1)
    {
        string path = Meshes + "/" + name + ".asset";
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved != null)
        {
            EditorUtility.CopySerialized(mesh, saved);
            EditorUtility.SetDirty(saved);
            UnityEngine.Object.DestroyImmediate(mesh);
            mesh = saved;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, path);
        }
        var filter = root.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var r = root.AddComponent<MeshRenderer>();
        r.sharedMaterials = new[] { m0, m1 };
        var c = root.AddComponent<MeshCollider>();
        c.sharedMesh = mesh;
    }

    static void Save(GameObject root)
    {
        PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + root.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }

    static void BuildCurved(string name, float width, float depth, float thick, bool inside, Material top, Material sides, Material accent, bool stones)
    {
        GameObject root = BaseObject(name, inside);
        StoreMeshAndAttach(root, MakeCurvedMesh(width, depth, thick, inside), name + "Mesh", top, sides);
        float edge = Mathf.Min(width * .38f, width * .5f - .1f);
        if (stones)
        {
            Prim(PrimitiveType.Cube, "Stone_01", root.transform, accent, new Vector3(-edge, .20f, -depth * .25f), new Vector3(.9f, .5f, 1.1f), new Vector3(0, 19, 9));
            Prim(PrimitiveType.Cube, "Stone_02", root.transform, accent, new Vector3(edge, .25f, depth * .24f), new Vector3(1.1f, .55f, .7f), new Vector3(0, -18, 6));
        }
        else if (name == "EnergyPlatform" || name == "SciFiCatwalk")
        {
            Prim(PrimitiveType.Cube, "EdgeGlow_Left", root.transform, accent, new Vector3(-edge, .10f, 0), new Vector3(.12f, .16f, depth * .95f), Vector3.zero);
            Prim(PrimitiveType.Cube, "EdgeGlow_Right", root.transform, accent, new Vector3(edge, .10f, 0), new Vector3(.12f, .16f, depth * .95f), Vector3.zero);
        }
        else if (name == "GrassLedge")
        {
            Prim(PrimitiveType.Cube, "GrassPatch", root.transform, accent, new Vector3(-1.5f, .07f, -.65f), new Vector3(1.25f, .09f, 1.2f), new Vector3(0, 15, 0));
        }
        Save(root);
    }

    static void BuildWoodBridge(Material wood, Material light, Material metal)
    {
        var root = BaseObject("WoodBridge", false);
        // One box collider on root keeps the traversable deck solid.
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, -.15f, 0f);
        collider.size = new Vector3(8f, .55f, 2.6f);
        for (int i = 0; i < 12; i++)
        {
            float x = -3.7f + i * (7.4f / 11f);
            Prim(PrimitiveType.Cube, "Plank_" + (i + 1), root.transform, i % 3 == 0 ? light : wood,
                new Vector3(x, 0, 0), new Vector3(.56f, .25f, 2.5f), Vector3.zero);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            Prim(PrimitiveType.Cube, "Rail_" + side, root.transform, metal,
                new Vector3(0, .55f, side * 1.28f), new Vector3(8, .12f, .12f), Vector3.zero);
        }
        Save(root);
    }

    static void BuildRamp(string name, float length, float width, float rise, bool inside, Material top, Material sides, Material stripe)
    {
        var root = BaseObject(name, inside);
        // Wedge prism with sloped top, descending to y=-0.65 at one end.
        float h0 = -.65f, h1 = rise;
        float z0 = -length * .5f, z1 = length * .5f;
        float x0 = -width * .5f, x1 = width * .5f;
        Vector3[] v = {
            new Vector3(x0,h0,z0),new Vector3(x1,h0,z0),new Vector3(x0,h1,z1),new Vector3(x1,h1,z1),
            new Vector3(x0,-.85f,z0),new Vector3(x1,-.85f,z0),new Vector3(x0,-.85f,z1),new Vector3(x1,-.85f,z1)
        };
        var mesh = new Mesh();
        mesh.name = name + "Mesh";
        mesh.vertices = v;
        mesh.subMeshCount = 2;
        var topTris = new System.Collections.Generic.List<int>();
        var sideTris = new System.Collections.Generic.List<int>();
        AddQuad(topTris, 0, 2, 3, 1);
        AddQuad(sideTris, 4, 5, 7, 6);
        AddQuad(sideTris, 0, 4, 6, 2);
        AddQuad(sideTris, 1, 3, 7, 5);
        AddQuad(sideTris, 0, 1, 5, 4);
        AddQuad(sideTris, 2, 6, 7, 3);
        mesh.SetTriangles(topTris, 0);
        mesh.SetTriangles(sideTris, 1);
        mesh.RecalculateNormals();
        StoreMeshAndAttach(root, mesh, name + "Mesh", top, sides);
        for (int i = 0; i < 3; i++)
        {
            float z = Mathf.Lerp(z0 + .8f, z1 - .8f, i / 2f);
            float t = (z - z0) / length;
            float y = Mathf.Lerp(h0, h1, t) + .03f;
            GameObject step = Prim(PrimitiveType.Cube, "SurfaceMark" + i, root.transform, stripe,
                new Vector3(0f, y, z), new Vector3(width * .75f, .07f, .09f), Vector3.zero);
            step.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(h1-h0, length) * Mathf.Rad2Deg, 0, 0);
        }
        Save(root);
    }

    [MenuItem("Tools/Fox Terrain Pack/Snap Selected Terrain to Cylinder Wall")]
    public static void SnapSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Fox Terrain Pack", "Select an instantiated terrain prefab in Hierarchy first.", "OK");
            return;
        }
        var hint = selected.GetComponent<CylinderSurfacePlacement>();
        if (hint == null)
        {
            EditorUtility.DisplayDialog("Fox Terrain Pack", "Selected object has no CylinderSurfacePlacement component.", "OK");
            return;
        }
        Transform pivot = null;
        foreach (Transform t in UnityEngine.Object.FindObjectsOfType<Transform>())
        {
            if (t.name == "CylinderPivot" || t.name == "CylinderRotator") { pivot = t; break; }
        }
        if (!pivot)
        {
            EditorUtility.DisplayDialog("Fox Terrain Pack", "Cannot find CylinderPivot. Rename the cylinder rotation root 'CylinderPivot' or place terrain manually.", "OK");
            return;
        }
        // Use original world position as angular/axial hint.
        Vector3 originalPos = selected.transform.position;
        Vector3 axis = pivot.forward.normalized;
        Vector3 radial = Vector3.ProjectOnPlane(originalPos - pivot.position, axis);
        if (radial.sqrMagnitude < .001f)
            radial = Vector3.ProjectOnPlane(pivot.up, axis);
        radial.Normalize();
        Vector3 outward = hint.insideSurface ? -radial : radial;
        float axial = Vector3.Dot(originalPos - pivot.position, axis);
        float radius = Mathf.Max(.2f, hint.cylinderRadius + (hint.insideSurface ? -hint.surfaceOffset : hint.surfaceOffset));
        Vector3 newPosition = pivot.position + axis * axial + radial * radius;
        Undo.RecordObject(selected.transform, "Snap Terrain To Cylinder");
        selected.transform.position = newPosition;
        selected.transform.rotation = Quaternion.LookRotation(axis, outward);
        Undo.SetTransformParent(selected.transform, pivot, "Parent Terrain To Cylinder Pivot");
        EditorUtility.SetDirty(selected);
        Debug.Log("[FoxTerrainPack] Snapped " + selected.name + " to " + pivot.name + " (radius " + radius + ").");
    }
}
