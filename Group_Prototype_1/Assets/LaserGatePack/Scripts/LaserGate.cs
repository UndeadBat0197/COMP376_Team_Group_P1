using UnityEngine;

/// <summary>
/// Curved, rotating-cylinder laser gate. The beam length is an ARC LENGTH (Unity units),
/// not a straight chord. Its geometry and hit checks trace the barrel's outside surface.
/// Attach to CylinderPivot, or let it attach automatically at runtime.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LaserGate : MonoBehaviour
{
    [Header("Cylinder: automatically finds CylinderRotator")]
    public Transform cylinderPivot;
    [Tooltip("Use the barrel mesh bounding box to estimate its radius and centre. Can be switched off for custom barrels.")]
    public bool autoDetectCylinder = true;
    [Min(0.1f)] public float manualRadius = 7.5f;
    [Tooltip("If auto detection is disabled, the local centre relative to CylinderPivot.")]
    public Vector3 manualCenterOffset = Vector3.zero;

    [Header("Arc location (around local Z axis of CylinderPivot)")]
    [Tooltip("0 = right, 90 = top, 180 = left, 270 = bottom.")]
    public float centerAngle = 90f;
    [Tooltip("Total curved length ALONG the surface, in Unity units.")]
    [Min(0.3f)] public float arcLength = 8f;
    [Tooltip("Position along the cylinder axis (local Z, relative to detected centre).")]
    public float axialPosition = 0f;
    [Tooltip("How far OUTSIDE the barrel surface the laser floats.")]
    [Min(0f)] public float surfaceGap = 0.35f;
    [Range(8, 120)] public int segments = 48;
    [Range(0.04f, 0.8f)] public float beamThickness = 0.15f;
    [Tooltip("Distance between the three parallel curved laser beams along the barrel axis.")]
    [Min(0.05f)] public float beamSpacing = 0.65f;

    [Header("Timing")]
    public bool blinking = true;
    [Min(0.05f)] public float onDuration = 2f;
    [Min(0.05f)] public float offDuration = 1.7f;
    public float timeOffset = 0f;

    [Header("Damage: uses existing PlayerHealth")]
    [Min(0f)] public float damage = 1f;
    public LayerMask playerLayers = ~0;

    [Header("Materials (provided by prefab generator)")]
    public Material beamMaterial;
    public Material emitterMaterial;
    public Material standbyMaterial;

    private const string GeneratedName = "CURVED GATE (generated)";
    private Transform generated;
    private LineRenderer[] beams;
    private Renderer[] lamps;
    private readonly Collider[] hits = new Collider[32];
    private bool isOn = true;
    private float radius = 7.5f;
    private Vector3 center;
    private int lastGeometryHash;
    private float nextGeometryCheck;
    public bool IsOn { get { return isOn; } }

    private void OnEnable()
    {
        lastGeometryHash = int.MinValue;
        if (Application.isPlaying) EnsurePivot(true);
    }

    private void OnValidate()
    {
        arcLength = Mathf.Max(0.3f, arcLength);
        manualRadius = Mathf.Max(0.1f, manualRadius);
        segments = Mathf.Clamp(segments, 8, 120);
        beamThickness = Mathf.Clamp(beamThickness, 0.04f, 0.8f);
        onDuration = Mathf.Max(0.05f, onDuration);
        offDuration = Mathf.Max(0.05f, offDuration);
        lastGeometryHash = int.MinValue;
    }

    private void Update()
    {
        // Do not modify the prefab asset itself merely because it is imported.
        if (!gameObject.scene.IsValid()) return;

        if (Application.isPlaying) EnsurePivot(true);
        if (Application.isPlaying || Time.realtimeSinceStartup >= nextGeometryCheck)
        {
            ResolveGeometry();
            nextGeometryCheck = Time.realtimeSinceStartup + 0.25f;
        }

        int hash = GeometryHash();
        if (hash != lastGeometryHash || generated == null)
        {
            RebuildVisuals();
            lastGeometryHash = hash;
        }
        RefreshState();
        if (Application.isPlaying && isOn) CheckDamage();
    }

    private void EnsurePivot(bool parentAtRuntime)
    {
        if (!cylinderPivot)
        {
            CylinderRotator rotator = FindObjectOfType<CylinderRotator>();
            if (rotator) cylinderPivot = rotator.transform;
            else
            {
                GameObject found = GameObject.Find("CylinderPivot");
                if (found) cylinderPivot = found.transform;
            }
        }

        if (parentAtRuntime && cylinderPivot && transform.parent != cylinderPivot)
        {
            transform.SetParent(cylinderPivot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            lastGeometryHash = int.MinValue;
        }
    }

    private void ResolveGeometry()
    {
        radius = manualRadius;
        center = manualCenterOffset;
        if (!autoDetectCylinder || !cylinderPivot) return;

        MeshFilter chosen = null;
        int best = int.MinValue;
        foreach (MeshFilter mf in cylinderPivot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf || !mf.sharedMesh || mf.GetComponentInParent<LaserGate>()) continue;
            string n = mf.name.ToLowerInvariant();
            int score = n.Contains("cylinderground") ? 100 :
                        n.Contains("hollowcylinder") ? 90 :
                        n.Contains("cylinder") ? 75 :
                        n.Contains("barrel") || n.Contains("tube") ? 60 : -100;
            if (score > best) { best = score; chosen = mf; }
        }
        // Mesh.bounds works with Read/Write disabled; no mesh.vertices access needed.
        if (!chosen || best < 0) return;
        Bounds b = chosen.sharedMesh.bounds;
        Vector3 c = cylinderPivot.InverseTransformPoint(chosen.transform.TransformPoint(b.center));
        float radialMax = 0f;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
            Vector3 p = cylinderPivot.InverseTransformPoint(chosen.transform.TransformPoint(corner)) - c;
            radialMax = Mathf.Max(radialMax, new Vector2(p.x, p.y).magnitude);
        }
        // Full circular meshes have square XY bounds, so corner distance is sqrt(2)*radius.
        if (radialMax > 0.2f)
        {
            radius = radialMax / Mathf.Sqrt(2f);
            center = c + manualCenterOffset;
        }
    }

    private int GeometryHash()
    {
        unchecked
        {
            int h = 17;
            h = 31*h + radius.GetHashCode();
            h = 31*h + center.GetHashCode();
            h = 31*h + centerAngle.GetHashCode();
            h = 31*h + arcLength.GetHashCode();
            h = 31*h + axialPosition.GetHashCode();
            h = 31*h + surfaceGap.GetHashCode();
            h = 31*h + beamSpacing.GetHashCode();
            h = 31*h + beamThickness.GetHashCode();
            h = 31*h + segments;
            h = 31*h + (transform.parent ? transform.parent.GetHashCode() : 0);
            return h;
        }
    }

    // Curve points are expressed in CylinderPivot coordinates.
    private Vector3 Point(float t, int row)
    {
        float a = (centerAngle + (t - 0.5f) * arcLength / Mathf.Max(0.1f, radius + surfaceGap) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        return center + new Vector3(
            Mathf.Cos(a) * (radius + surfaceGap),
            Mathf.Sin(a) * (radius + surfaceGap),
            axialPosition + (row - 1) * beamSpacing);
    }

    // When not parented (Prefab preview), transform data is local to this object.
    private Vector3 ToRootLocal(Vector3 pivotPoint)
    {
        if (!cylinderPivot) return pivotPoint;
        return transform.InverseTransformPoint(cylinderPivot.TransformPoint(pivotPoint));
    }

    public void RebuildVisuals()
    {
        if (!gameObject.scene.IsValid()) return;
        // Old straight-gate assets from v1 may still be present in an existing scene instance.
        Transform oldFrame = transform.Find("FRAME - industrial sci-fi");
        if (oldFrame) oldFrame.gameObject.SetActive(false);
        for (int i = 1; i <= 3; i++)
        {
            Transform straight = transform.Find("ACTIVE LASER " + i);
            if (straight) straight.gameObject.SetActive(false);
        }
        Transform previous = transform.Find(GeneratedName);
        if (previous)
        {
            if (Application.isPlaying) Destroy(previous.gameObject);
            else DestroyImmediate(previous.gameObject);
        }
        GameObject group = new GameObject(GeneratedName);
        group.transform.SetParent(transform, false);
        generated = group.transform;
        beams = new LineRenderer[3];
        for (int row = 0; row < 3; row++)
        {
            GameObject obj = new GameObject("Curved Laser Beam " + (row+1));
            obj.transform.SetParent(generated, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthMultiplier = beamThickness;
            line.positionCount = segments + 1;
            line.startColor = new Color(1f, .15f, .19f);
            line.endColor = line.startColor;
            if (beamMaterial) line.sharedMaterial = beamMaterial;
            for (int i = 0; i <= segments; i++)
                line.SetPosition(i, ToRootLocal(Point(i / (float)segments, row)));
            beams[row] = line;
            for (int end = 0; end <= 1; end++)
            {
                GameObject pod = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pod.name = "Emitter Pod";
                pod.transform.SetParent(generated, false);
                pod.transform.localPosition = ToRootLocal(Point(end, row));
                pod.transform.localScale = Vector3.one * Mathf.Max(.20f, beamThickness * 2.4f);
                Collider col = pod.GetComponent<Collider>();
                if (col)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
                Renderer render = pod.GetComponent<Renderer>();
                if (render && emitterMaterial) render.sharedMaterial = emitterMaterial;
            }
        }
        lamps = generated.GetComponentsInChildren<Renderer>();
    }

    private void RefreshState()
    {
        float cycle = Mathf.Max(0.1f, onDuration + offDuration);
        bool shouldOn = !blinking || Mathf.Repeat((Application.isPlaying ? Time.time : 0f) + timeOffset, cycle) < onDuration;
        isOn = shouldOn;
        if (beams == null || beams.Length != 3)
        {
            // Reacquire generated references when entering play mode from a saved prefab.
            Transform g = transform.Find(GeneratedName);
            if (g)
            {
                beams = g.GetComponentsInChildren<LineRenderer>(true);
                generated = g;
            }
        }
        if (beams != null)
            foreach (LineRenderer line in beams)
                if (line) line.enabled = isOn;
    }

    private void CheckDamage()
    {
        if (beams == null) return;
        Transform reference = cylinderPivot ? cylinderPivot : transform;
        for (int row = 0; row < 3; row++)
        {
            Vector3 prev = reference.TransformPoint(Point(0f, row));
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = reference.TransformPoint(Point(i / (float)segments, row));
                int count = Physics.OverlapCapsuleNonAlloc(prev, next,
                    Mathf.Max(0.08f, beamThickness * 0.75f), hits, playerLayers,
                    QueryTriggerInteraction.Ignore);
                for (int j = 0; j < count; j++)
                {
                    if (!hits[j]) continue;
                    PlayerHealth hp = hits[j].GetComponentInParent<PlayerHealth>();
                    if (hp)
                    {
                        hp.TakeDamage(damage);
                        return;
                    }
                }
                prev = next;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!gameObject.scene.IsValid()) return;
        Transform reference = cylinderPivot ? cylinderPivot : transform;
        Gizmos.color = new Color(1f, .15f, .15f, .85f);
        for (int row = 0; row < 3; row++)
        {
            Vector3 prev = reference.TransformPoint(Point(0f, row));
            for (int i = 1; i <= Mathf.Clamp(segments, 8, 120); i++)
            {
                Vector3 next = reference.TransformPoint(Point(i / (float)segments, row));
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
