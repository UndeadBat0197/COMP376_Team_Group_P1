#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ThornTrapPrefabBuilder
{
    private const string Root = "Assets/ThornTrapPack";
    private const string PrefabPath = Root + "/Prefabs/CurvedThornTrap.prefab";

    static ThornTrapPrefabBuilder() { EditorApplication.delayCall += CreateIfMissing; }

    private static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) CreateOrRebuildPrefab();
    }

    [MenuItem("Tools/Thorn Trap/Create or Rebuild Prefab")]
    public static void CreateOrRebuildPrefab()
    {
        Directory.CreateDirectory(Root + "/Prefabs");
        Directory.CreateDirectory(Root + "/Materials");
        Material metal = GetMaterial("ThornSocketMetal", new Color(.16f, .23f, .27f), false);
        Material spike = GetMaterial("ThornSharpCopper", new Color(.57f, .27f, .19f), false);
        Material warning = GetMaterial("ThornWarningRed", new Color(1f, .11f, .09f), true);
        GameObject obj = new GameObject("CurvedThornTrap");
        try
        {
            CurvedThornTrap trap = obj.AddComponent<CurvedThornTrap>();
            trap.baseMaterial = metal;
            trap.spikeMaterial = spike;
            trap.warningMaterial = warning;
            // Visuals are generated automatically after placed into a scene.
            PrefabUtility.SaveAsPrefabAsset(obj, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Thorn prefab created: " + PrefabPath);
        }
        finally { Object.DestroyImmediate(obj); }
    }

    [MenuItem("Tools/Thorn Trap/Snap Selected Trap to Cylinder")]
    public static void SnapSelected()
    {
        CurvedThornTrap trap = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<CurvedThornTrap>() : null;
        CylinderRotator rotator = Object.FindObjectOfType<CylinderRotator>();
        if (!trap || !rotator)
        {
            EditorUtility.DisplayDialog("Thorn Trap", "Select a CurvedThornTrap in the scene and make sure CylinderRotator exists.", "OK");
            return;
        }
        Undo.SetTransformParent(trap.transform, rotator.transform, "Attach thorn trap");
        trap.cylinderPivot = rotator.transform;
        EditorUtility.SetDirty(trap);
        trap.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver);
        SceneView.RepaintAll();
        Debug.Log("Thorn trap attached. It will snap on the next editor update.");
    }

    private static Material GetMaterial(string name, Color color, bool emissive)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material found = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (found) return found;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        if (!shader) shader = Shader.Find("Unlit/Color");
        Material mat = new Material(shader);
        mat.name = name;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 2.5f);
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
#endif
