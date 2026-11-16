using UnityEngine;
using UnityEditor;

public class SaveMeshAsset
{
    [MenuItem("CONTEXT/MeshFilter/Save Mesh As Asset")]
    static void SaveMesh(MenuCommand command)
    {
        MeshFilter mf = (MeshFilter)command.context;
        if (mf == null || mf.sharedMesh == null) return;

        string path = EditorUtility.SaveFilePanelInProject(
            "Save Mesh", mf.sharedMesh.name, "asset", "Choose where to save the mesh");
        if (string.IsNullOrEmpty(path)) return;

        Mesh meshCopy = Object.Instantiate(mf.sharedMesh);
        AssetDatabase.CreateAsset(meshCopy, path);
        AssetDatabase.SaveAssets();
        mf.sharedMesh = meshCopy;
    }
}
