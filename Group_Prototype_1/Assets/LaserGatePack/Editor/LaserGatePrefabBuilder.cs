#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LaserGatePrefabBuilder
{
    private const string Root = "Assets/LaserGatePack";
    private const string PrefabPath = Root + "/Prefabs/LaserGate_Curved.prefab";

    static LaserGatePrefabBuilder()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    private static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) return;
        Build();
    }

    [MenuItem("Tools/Laser Gate/Create or Rebuild Curved Prefab")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Prefabs");
        Directory.CreateDirectory(Root + "/Materials");
        Material beam = GetMaterial("CurvedLaserRed", new Color(1f, .07f, .10f), 4f);
        Material emitter = GetMaterial("CurvedEmitterDark", new Color(.08f, .12f, .20f), 0f);
        Material standby = GetMaterial("CurvedStandbyBlue", new Color(.1f, .6f, 1f), 2f);
        GameObject obj = new GameObject("LaserGate - Curved Cylinder");
        try
        {
            LaserGate gate = obj.AddComponent<LaserGate>();
            gate.beamMaterial = beam;
            gate.emitterMaterial = emitter;
            gate.standbyMaterial = standby;
            gate.RebuildVisuals();
            PrefabUtility.SaveAsPrefabAsset(obj, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Curved laser gate ready: " + PrefabPath);
        }
        finally { Object.DestroyImmediate(obj); }
    }

    [MenuItem("Tools/Laser Gate/Attach Selected Gate to CylinderPivot")]
    public static void AttachSelected()
    {
        GameObject selected = Selection.activeGameObject;
        LaserGate gate = selected ? selected.GetComponent<LaserGate>() : null;
        CylinderRotator rotator = Object.FindObjectOfType<CylinderRotator>();
        if (!gate || !rotator)
        {
            EditorUtility.DisplayDialog("Laser Gate", "Select the LaserGate object and ensure CylinderRotator exists in the scene.", "OK");
            return;
        }
        Undo.SetTransformParent(gate.transform, rotator.transform, "Attach curved gate to cylinder");
        Undo.RecordObject(gate.transform, "Align gate to cylinder");
        gate.cylinderPivot = rotator.transform;
        gate.transform.localPosition = Vector3.zero;
        gate.transform.localRotation = Quaternion.identity;
        gate.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(gate);
        gate.RebuildVisuals();
    }

    private static Material GetMaterial(string name, Color color, float emission)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing) return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) shader = Shader.Find("Unlit/Color");
        if (!shader) shader = Shader.Find("Standard");
        Material m = new Material(shader);
        m.name = name;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * emission);
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
#endif
