using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using FoxWorldPack;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FoxWorldPrefabBuilder
{
    const string Root = "Assets/FoxWorldPack";
    const string Prefabs = Root + "/Prefabs";
    const string Materials = Root + "/Materials";
    const string Meshes = Root + "/Meshes";

    static FoxWorldPrefabBuilder()
    {
        EditorApplication.delayCall += AutoBuildIfMissing;
    }

    static void AutoBuildIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Nature_RoundTree.prefab") != null) return;
        try { Build(); }
        catch (Exception e) { Debug.LogError("FoxWorldPack failed to generate prefabs: " + e); }
    }

    [MenuItem("Tools/Fox World Pack/Generate or Rebuild All Prefabs")]
    public static void Build()
    {
        EnsureDirectory(Prefabs);
        EnsureDirectory(Materials);
        EnsureDirectory(Meshes);

        var wood = Material("Wood_Bark", "#79503A");
        var woodDark = Material("Wood_Dark", "#4F3528");
        var woodLight = Material("Wood_Light", "#B8875A");
        var leaf = Material("Leaves_Green", "#72AC5B");
        var leaf2 = Material("Leaves_Dark", "#3E7A53");
        var needle = Material("Needle_Green", "#316F65");
        var rock = Material("Rock_Slate", "#8C9A99");
        var rock2 = Material("Rock_Dark", "#5C7076");
        var moss = Material("Moss", "#61915B");
        var metal = Material("Metal_Graphite", "#2C394B");
        var steel = Material("Metal_Silver", "#9AAFBF");
        var rim = Material("Metal_Blue", "#496C86");
        var bolt = Material("Metal_Bolt", "#1C2938");
        var cyan = Material("Glow_Cyan", "#56E5FF", true);
        var amber = Material("Glow_Amber", "#FFC566", true);
        var red = Material("Glow_Red", "#FF526B", true);

        Save(CreateRoundTree(wood, leaf, leaf2), "Nature_RoundTree");
        Save(CreatePineTree(wood, needle, leaf2), "Nature_PineTree");
        Save(CreateRock("SmallRock", rock, moss, .72f), "Nature_SmallRock");
        Save(CreateRock("MediumRock", rock2, moss, 1.2f), "Nature_MediumRock");
        Save(CreateRock("LargeRock", rock, moss, 1.75f), "Nature_LargeRock");
        Save(CreateStump(wood, woodLight, woodDark), "Nature_TreeStump");
        Save(CreateMetalCrate(metal, steel, bolt, cyan, false, false), "SciFi_MetalBlock");
        Save(CreateMetalCrate(metal, steel, bolt, cyan, true, false), "SciFi_GlowBlock");
        Save(CreateMetalCrate(metal, steel, bolt, amber, true, true), "SciFi_PushCrate");
        Save(CreatePillar(metal, steel, cyan), "SciFi_TechPillar");
        Save(CreateDoor(metal, steel, cyan, amber), "SciFi_AutoDoor");
        Save(CreateEnergyBarrier(metal, steel, cyan, red), "SciFi_EnergyBarrier");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FoxWorldPack] Successfully generated 12 prefabs in " + Prefabs + ".");
    }

    static void EnsureDirectory(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureDirectory(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Material Material(string name, string colorCode, bool emission = false)
    {
        string path = Materials + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        ColorUtility.TryParseHtmlString(colorCode, out Color color);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("HDRP/Lit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (emission)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 1.8f);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject Create(string name) { return new GameObject(name); }

    static GameObject Primitive(Transform parent, PrimitiveType type, string name, Material mat,
        Vector3 pos, Vector3 scale, Vector3 rotation = default(Vector3))
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localRotation = Quaternion.Euler(rotation);
        obj.transform.localScale = scale;
        var render = obj.GetComponent<Renderer>();
        if (render) render.sharedMaterial = mat;
        var collider = obj.GetComponent<Collider>();
        if (collider) Object.DestroyImmediate(collider);
        return obj;
    }

    static Mesh ConeMesh()
    {
        string path = Meshes + "/Cone_LowPoly.asset";
        Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m) return m;
        const int n = 9;
        Vector3[] verts = new Vector3[n + 2];
        verts[0] = new Vector3(0f, 1f, 0f);
        verts[n + 1] = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            float a = 2f * Mathf.PI * i / n;
            verts[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
        int[] tris = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            tris[i*6] = 0; tris[i*6+1] = 1+j; tris[i*6+2] = 1+i;
            tris[i*6+3] = n+1; tris[i*6+4] = 1+i; tris[i*6+5] = 1+j;
        }
        m = new Mesh { name="Cone_LowPoly", vertices=verts, triangles=tris };
        m.RecalculateNormals();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Cone(Transform parent, string name, Material mat, Vector3 pos, Vector3 scale)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    static void AddCollider(GameObject root, Vector3 center, Vector3 size)
    {
        var c = root.AddComponent<BoxCollider>(); c.center = center; c.size = size;
    }

    static GameObject CreateRoundTree(Material wood, Material leaf, Material leaf2)
    {
        var root=Create("Nature_RoundTree");
        Primitive(root.transform, PrimitiveType.Cylinder, "Trunk", wood, new Vector3(0,1.2f,0), new Vector3(.56f,1.2f,.56f));
        Primitive(root.transform, PrimitiveType.Sphere, "Crown_Core", leaf, new Vector3(0,3.05f,0), new Vector3(2.0f,1.75f,1.9f));
        Primitive(root.transform, PrimitiveType.Sphere, "Crown_Left", leaf2, new Vector3(-.95f,2.9f,.18f), new Vector3(1.35f,1.25f,1.35f));
        Primitive(root.transform, PrimitiveType.Sphere, "Crown_Right", leaf, new Vector3(.9f,3.0f,-.2f), new Vector3(1.4f,1.3f,1.4f));
        Primitive(root.transform, PrimitiveType.Sphere, "Crown_Top", leaf, new Vector3(0,3.75f,0), new Vector3(1.45f,1.0f,1.45f));
        var c = root.AddComponent<CapsuleCollider>(); c.center = new Vector3(0,1.9f,0); c.height = 4f; c.radius=.85f;
        return root;
    }

    static GameObject CreatePineTree(Material bark, Material needle, Material secondary)
    {
        var root=Create("Nature_PineTree");
        Primitive(root.transform, PrimitiveType.Cylinder,"Trunk",bark,new Vector3(0,1.65f,0),new Vector3(.38f,1.65f,.38f));
        Cone(root.transform,"Needles_Lower",secondary,new Vector3(0,1.55f,0),new Vector3(1.65f,2.25f,1.65f));
        Cone(root.transform,"Needles_Middle",needle,new Vector3(0,2.45f,0),new Vector3(1.3f,2.3f,1.3f));
        Cone(root.transform,"Needles_Top",secondary,new Vector3(0,3.4f,0),new Vector3(.93f,1.9f,.93f));
        var c=root.AddComponent<CapsuleCollider>(); c.center=new Vector3(0,2.2f,0);c.height=4.2f;c.radius=.7f;
        return root;
    }

    static GameObject CreateRock(string name, Material stone, Material moss, float scale)
    {
        var root=Create("Nature_"+name);
        Primitive(root.transform,PrimitiveType.Cube,"Rock_Core",stone,new Vector3(0,.42f*scale,0),new Vector3(1.25f*scale,.8f*scale,1.0f*scale),new Vector3(8,20,8));
        Primitive(root.transform,PrimitiveType.Cube,"Rock_Ridge",stone,new Vector3(-.25f*scale,.67f*scale,.04f*scale),new Vector3(.8f*scale,.58f*scale,.9f*scale),new Vector3(0,-15,25));
        Primitive(root.transform,PrimitiveType.Sphere,"Moss_Patch",moss,new Vector3(-.2f*scale,.86f*scale,.15f*scale),new Vector3(.68f*scale,.11f*scale,.56f*scale));
        AddCollider(root,new Vector3(0,.5f*scale,0),new Vector3(1.35f*scale,1.15f*scale,1.2f*scale));
        return root;
    }

    static GameObject CreateStump(Material bark, Material top, Material dark)
    {
        var root=Create("Nature_TreeStump");
        Primitive(root.transform,PrimitiveType.Cylinder,"Stump_Bark",bark,new Vector3(0,.55f,0),new Vector3(.95f,.55f,.95f));
        Primitive(root.transform,PrimitiveType.Cylinder,"Cut_Surface",top,new Vector3(0,1.11f,0),new Vector3(1.0f,.06f,1.0f));
        Primitive(root.transform,PrimitiveType.Cylinder,"Cut_Ring",dark,new Vector3(0,1.173f,0),new Vector3(.63f,.01f,.63f));
        Primitive(root.transform,PrimitiveType.Cylinder,"Cut_Center",top,new Vector3(0,1.189f,0),new Vector3(.38f,.01f,.38f));
        AddCollider(root,new Vector3(0,.58f,0),new Vector3(1.05f,1.18f,1.05f));
        return root;
    }

    static GameObject CreateMetalCrate(Material metal, Material silver, Material dark, Material light, bool glowing, bool pushable)
    {
        var root=Create(pushable?"SciFi_PushCrate":glowing?"SciFi_GlowBlock":"SciFi_MetalBlock");
        Primitive(root.transform,PrimitiveType.Cube,"Core",metal,new Vector3(0,.75f,0),new Vector3(1.5f,1.5f,1.5f));
        for (int ax=0;ax<3;ax++) for(int sy=-1;sy<=1;sy+=2)
        {
            Vector3 pos = new Vector3(0,.75f,0), size=Vector3.one;
            pos[ax] += sy*.755f;
            size[ax] = .07f; size[(ax+1)%3] = 1.4f; size[(ax+2)%3] = 1.4f;
            Primitive(root.transform,PrimitiveType.Cube,"Panel_"+ax+"_"+sy,silver,pos,size);
        }
        // Narrow corner struts
        for (int x=-1;x<=1;x+=2) for (int z=-1;z<=1;z+=2)
        {
            Primitive(root.transform,PrimitiveType.Cube,"CornerStrut",dark,new Vector3(x*.69f,.75f,z*.69f),new Vector3(.16f,1.58f,.16f));
        }
        if(glowing)
        {
            Primitive(root.transform,PrimitiveType.Cube,"GlowBar_Front",light,new Vector3(0,.75f,-.795f),new Vector3(.95f,.10f,.05f));
            Primitive(root.transform,PrimitiveType.Cube,"GlowBar_Back",light,new Vector3(0,.75f,.795f),new Vector3(.95f,.10f,.05f));
            Primitive(root.transform,PrimitiveType.Cube,"GlowBar_Left",light,new Vector3(-.795f,.75f,0),new Vector3(.05f,.10f,.95f));
            Primitive(root.transform,PrimitiveType.Cube,"GlowBar_Right",light,new Vector3(.795f,.75f,0),new Vector3(.05f,.10f,.95f));
            Primitive(root.transform,PrimitiveType.Cube,"GlowPanel_Top",light,new Vector3(0,1.52f,0),new Vector3(.7f,.05f,.7f));
        }
        AddCollider(root,new Vector3(0,.75f,0),new Vector3(1.5f,1.5f,1.5f));
        if(pushable)
        {
            var rb=root.AddComponent<Rigidbody>(); rb.mass=15f; rb.interpolation=RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        }
        return root;
    }

    static GameObject CreatePillar(Material dark,Material steel,Material blue)
    {
        var root=Create("SciFi_TechPillar");
        Primitive(root.transform,PrimitiveType.Cylinder,"Foot",dark,new Vector3(0,.14f,0),new Vector3(1.25f,.14f,1.25f));
        Primitive(root.transform,PrimitiveType.Cylinder,"Main",steel,new Vector3(0,2f,0),new Vector3(.85f,1.8f,.85f));
        for(int i=0;i<4;i++)
        {
            Primitive(root.transform,PrimitiveType.Cylinder,"Band_"+i,dark,new Vector3(0,.8f+i*.8f,0),new Vector3(1.0f,.1f,1.0f));
            Primitive(root.transform,PrimitiveType.Cube,"Glow_"+i,blue,new Vector3(0,.8f+i*.8f,-.9f),new Vector3(.6f,.08f,.08f));
        }
        Primitive(root.transform,PrimitiveType.Cylinder,"Top",dark,new Vector3(0,3.9f,0),new Vector3(1.2f,.18f,1.2f));
        AddCollider(root,new Vector3(0,2f,0),new Vector3(1.1f,4.0f,1.1f));
        return root;
    }

    static GameObject CreateDoor(Material dark,Material silver,Material blue,Material amber)
    {
        var root=Create("SciFi_AutoDoor");
        Primitive(root.transform,PrimitiveType.Cube,"LeftSupport",dark,new Vector3(-2f,2f,0),new Vector3(.5f,4.2f,.65f));
        Primitive(root.transform,PrimitiveType.Cube,"RightSupport",dark,new Vector3(2f,2f,0),new Vector3(.5f,4.2f,.65f));
        Primitive(root.transform,PrimitiveType.Cube,"Header",silver,new Vector3(0,4.13f,0),new Vector3(4.6f,.42f,.7f));
        Primitive(root.transform,PrimitiveType.Cube,"NeonLeft",blue,new Vector3(-1.65f,2f,-.37f),new Vector3(.09f,3.7f,.07f));
        Primitive(root.transform,PrimitiveType.Cube,"NeonRight",blue,new Vector3(1.65f,2f,-.37f),new Vector3(.09f,3.7f,.07f));
        Primitive(root.transform,PrimitiveType.Cube,"WarningLight",amber,new Vector3(0,4.2f,-.37f),new Vector3(.5f,.12f,.07f));
        var leaf = Primitive(root.transform,PrimitiveType.Cube,"SlidingDoor",silver,new Vector3(0,1.85f,0),new Vector3(3.3f,3.7f,.34f));
        Primitive(leaf.transform,PrimitiveType.Cube,"DoorAccent",dark,new Vector3(0,0,-.55f),new Vector3(.65f,.92f,.1f));
        var doorCol=leaf.AddComponent<BoxCollider>(); doorCol.size=Vector3.one;
        AddCollider(root,new Vector3(-2f,2f,0),new Vector3(.5f,4.2f,.7f));
        var rightCol=root.AddComponent<BoxCollider>();rightCol.center=new Vector3(2f,2f,0);rightCol.size=new Vector3(.5f,4.2f,.7f);
        var controller=root.AddComponent<ProximityDoor>();controller.doorLeaf=leaf.transform;
        controller.liftHeight=4.0f;
        return root;
    }

    static GameObject CreateEnergyBarrier(Material metal, Material steel, Material blue, Material red)
    {
        var root=Create("SciFi_EnergyBarrier");
        Primitive(root.transform,PrimitiveType.Cube,"LeftPost",metal,new Vector3(-2f,1.55f,0),new Vector3(.55f,3.1f,.65f));
        Primitive(root.transform,PrimitiveType.Cube,"RightPost",metal,new Vector3(2f,1.55f,0),new Vector3(.55f,3.1f,.65f));
        Primitive(root.transform,PrimitiveType.Cube,"LeftLamp",red,new Vector3(-2f,2.6f,-.35f),new Vector3(.35f,.16f,.08f));
        Primitive(root.transform,PrimitiveType.Cube,"RightLamp",red,new Vector3(2f,2.6f,-.35f),new Vector3(.35f,.16f,.08f));
        var field=Create("EnergyField_VISUAL"); field.transform.SetParent(root.transform,false);
        for (int i=0;i<6;i++)
        {
            Primitive(field.transform,PrimitiveType.Cube,"EnergyLine_"+i,blue,new Vector3(0,.35f+i*.5f,0),new Vector3(3.75f,.08f,.1f));
        }
        var trigger=root.AddComponent<BoxCollider>();trigger.center=new Vector3(0,1.55f,0);trigger.size=new Vector3(3.5f,3.05f,.5f);trigger.isTrigger=true;
        var script=root.AddComponent<EnergyBarrier>(); script.fieldVisual=field;
        return root;
    }

    static void Save(GameObject instance, string name)
    {
        string path=Prefabs+"/"+name+".prefab";
        var saved = PrefabUtility.SaveAsPrefabAsset(instance,path);
        if (saved == null) Debug.LogError("FoxWorldPack prefab failed: "+path);
        Object.DestroyImmediate(instance);
    }
}
