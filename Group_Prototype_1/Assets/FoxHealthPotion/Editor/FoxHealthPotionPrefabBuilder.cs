#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>One-time asset builder. Builds a real, editor-visible model prefab and never touches scenes.</summary>
[InitializeOnLoad]
public static class FoxHealthPotionPrefabBuilder
{
    private const string Base = "Assets/FoxHealthPotion";
    private const string PrefabPath = Base + "/Prefabs/HealthPotion.prefab";

    static FoxHealthPotionPrefabBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    static void BuildIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) Build();
    }

    [MenuItem("Tools/Fox Health Potion/Create or Rebuild Prefab")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play mode before rebuilding the potion prefab.");
            return;
        }
        Directory.CreateDirectory(Base + "/Prefabs");
        Directory.CreateDirectory(Base + "/Materials");
        Directory.CreateDirectory(Base + "/Meshes");

        Material glass = MaterialAsset("Glass_Azure", new Color(.30f, .86f, .96f, .33f), true, false);
        Material liquid = MaterialAsset("Healing_Ruby", new Color(.88f, .055f, .14f), false, true);
        Material gold = MaterialAsset("Warm_Gold", new Color(.91f, .65f, .22f), false, false);
        Material cork = MaterialAsset("Cork", new Color(.58f, .34f, .18f), false, false);
        Material white = MaterialAsset("Heart_White", new Color(1f, .96f, .88f), false, true);
        Material dark = MaterialAsset("Dark_Trim", new Color(.20f, .21f, .30f), false, false);
        Material glow = MaterialAsset("Rose_Glow", new Color(1f, .21f, .29f), false, true);

        // Save meshes as standalone assets so the prefab never loses references.
        Mesh bottleMesh = MeshAsset("PotionBottle", new[] {
            new Vector2(0.00f, 0.00f), new Vector2(.27f, 0.00f),
            new Vector2(.39f, .08f), new Vector2(.48f, .24f),
            new Vector2(.50f, .55f), new Vector2(.47f, .81f),
            new Vector2(.32f, 1.03f), new Vector2(.23f, 1.15f),
            new Vector2(.23f, 1.40f), new Vector2(.19f, 1.45f)
        });
        Mesh liquidMesh = MeshAsset("RubyLiquid", new[] {
            new Vector2(0f, .06f), new Vector2(.26f, .06f),
            new Vector2(.37f, .16f), new Vector2(.40f, .30f),
            new Vector2(.40f, .57f), new Vector2(.38f, .73f),
            new Vector2(.22f, .92f), new Vector2(0f, .92f)
        });
        Mesh corkMesh = MeshAsset("CorkShape", new[] {
            new Vector2(0f, 1.33f), new Vector2(.19f, 1.33f),
            new Vector2(.22f, 1.52f), new Vector2(.21f, 1.58f),
            new Vector2(0f, 1.58f)
        });
        GameObject root = new GameObject("HealthPotion");
        try
        {
            FoxHealthPotion pickup = root.AddComponent<FoxHealthPotion>();
            SphereCollider trigger = root.GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, .75f, 0);
            trigger.radius = .90f;
            // Kinematic rigidbody guarantees trigger events even when pickup sits still.
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            GameObject vis = new GameObject("Visual");
            vis.transform.SetParent(root.transform, false);
            vis.transform.localPosition = new Vector3(0f, .12f, 0f);
            pickup.visualRoot = vis.transform;

            MakeMesh("Bottle_Glass", vis.transform, bottleMesh, glass);
            MakeMesh("Red_Liquid", vis.transform, liquidMesh, liquid);
            MakeMesh("Cork_Stopper", vis.transform, corkMesh, cork);
            // Distinctive gold lip, collar, foot, and diagonal gold grips.
            Disc("Gold_Neck_Ring", vis.transform, new Vector3(0,1.39f,0), .27f, .06f, gold);
            Disc("Gold_Base_Ring", vis.transform, new Vector3(0,.095f,0), .405f, .06f, gold);
            Disc("Gold_Cap_Top", vis.transform, new Vector3(0,1.61f,0), .24f, .065f, gold);
            Disc("Base_Dark", vis.transform, new Vector3(0,.012f,0), .31f, .027f, dark);

            // Heart icon on front of bottle: two rounded lobes and a triangular lower tip.
            GameObject heart = new GameObject("White_Heart_Badge");
            heart.transform.SetParent(vis.transform, false);
            heart.transform.localPosition = new Vector3(0,.57f,-.485f);
            Primitive("Heart_Lobe_Left", PrimitiveType.Sphere, heart.transform,
                new Vector3(-.11f,.085f,0), new Vector3(.26f,.25f,.07f), white);
            Primitive("Heart_Lobe_Right", PrimitiveType.Sphere, heart.transform,
                new Vector3(.11f,.085f,0), new Vector3(.26f,.25f,.07f), white);
            GameObject tip = Primitive("Heart_Point", PrimitiveType.Cube, heart.transform,
                new Vector3(0,-.07f,0), new Vector3(.255f,.255f,.065f), white);
            tip.transform.localRotation = Quaternion.Euler(0f,0f,45f);

            // Four evenly-spaced decorative studs emphasize the collectible silhouette.
            for (int i=0;i<4;i++)
            {
                float a = i * Mathf.PI * .5f;
                Primitive("Gold_Stud_" + i, PrimitiveType.Sphere, vis.transform,
                    new Vector3(Mathf.Sin(a)*.47f,.30f,Mathf.Cos(a)*.47f),
                    new Vector3(.10f,.10f,.10f), gold);
            }
            // A subtle light/emissive orb inside the liquid (does not use expensive scene lights).
            Primitive("Inner_Rose_Glow", PrimitiveType.Sphere, vis.transform,
                new Vector3(0,.49f,0), new Vector3(.23f,.23f,.23f), glow);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Fox Health Potion prefab ready: " + PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static Material MaterialAsset(string filename, Color color, bool transparent, bool emission)
    {
        string path = Base + "/Materials/" + filename + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            mat = new Material(shader);
            mat.name = filename;
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .78f);
        if (transparent)
        {
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
        }
        if (emission)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(color.r,color.g,color.b) * 1.75f);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mesh MeshAsset(string name, Vector2[] rings)
    {
        string path = Base + "/Meshes/" + name + ".asset";
        Mesh found = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (found != null) return found;
        const int sides = 24;
        Vector3[] vertices = new Vector3[rings.Length*sides];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] tris = new int[(rings.Length-1)*sides*6];
        for (int j=0;j<rings.Length;j++)
        for (int i=0;i<sides;i++)
        {
            float a = 2f*Mathf.PI*i/sides;
            int idx = j*sides+i;
            vertices[idx] = new Vector3(Mathf.Sin(a)*rings[j].x, rings[j].y, Mathf.Cos(a)*rings[j].x);
            uvs[idx] = new Vector2((float)i/sides, (float)j/(rings.Length-1));
        }
        int n=0;
        for (int j=0;j<rings.Length-1;j++)
        for (int i=0;i<sides;i++)
        {
            int next = (i+1)%sides;
            int a=j*sides+i, b=j*sides+next, c=(j+1)*sides+i, d=(j+1)*sides+next;
            tris[n++]=a; tris[n++]=c; tris[n++]=b;
            tris[n++]=b; tris[n++]=c; tris[n++]=d;
        }
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static void MakeMesh(string name, Transform parent, Mesh mesh, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
    static GameObject Primitive(string name, PrimitiveType kind, Transform parent, Vector3 pos, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(kind);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }
    static void Disc(string name, Transform parent, Vector3 pos, float radius, float thick, Material material)
    {
        Primitive(name, PrimitiveType.Cylinder, parent, pos, new Vector3(radius*2f,thick*.5f,radius*2f), material);
    }
}
#endif
