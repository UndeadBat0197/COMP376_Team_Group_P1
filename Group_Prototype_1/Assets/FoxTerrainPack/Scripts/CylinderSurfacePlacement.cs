using UnityEngine;

namespace FoxTerrainPack
{
    // Marker component: holds a placement hint. No automatic scene modifications.
    public class CylinderSurfacePlacement : MonoBehaviour
    {
        [Tooltip("Check when placing the prefab on the inside of the cylinder.")]
        public bool insideSurface = false;
        [Tooltip("Approximate cylinder radius, used by the editor snap tool.")]
        public float cylinderRadius = 18f;
        [Tooltip("Additional distance outwards from the cylinder wall.")]
        public float surfaceOffset = 0f;
    }
}
