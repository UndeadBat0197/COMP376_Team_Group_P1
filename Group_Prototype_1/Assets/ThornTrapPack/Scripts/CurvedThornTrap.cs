using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Curved retractable spikes on the OUTSIDE of a Z-axis cylinder.
/// Works with the project's existing CylinderRotator + PlayerHealth.
/// Drop the prefab near the cylinder; it attaches to CylinderPivot and snaps to the surface.
/// No mesh.vertices / Read-Write requirement.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CurvedThornTrap : MonoBehaviour
{
    [Header("Cylinder / auto snap")]
    public Transform cylinderPivot;
    public bool autoDetectCylinder = true;
    [Min(0.1f)] public float manualRadius = 15f;
    public Vector3 manualCenterOffset = Vector3.zero;
    public bool autoSnap = true;
    [Min(0f)] public float surfaceOffset = 0.06f;

    [Header("Curved thorn strip")]
    [Tooltip("Length measured ALONG the barrel circumference, not a straight line.")]
    [Min(0.5f)] public float arcLength = 9f;
    [Min(0.2f)] public float stripWidth = 2.1f;
    [Range(5, 48)] public int spikeCount = 17;
    [Range(1, 3)] public int spikeRows = 2;
    [Min(0.15f)] public float spikeHeight = 1.35f;
    [Min(0.06f)] public float spikeBaseRadius = 0.25f;
    [Range(4, 10)] public int spikeSides = 6;

    [Header("Extend / retract animation")]
    [Min(0.1f)] public float extendedSeconds = 2.5f;
    [Min(0.1f)] public float retractedSeconds = 2.0f;
    [Min(0.05f)] public float extendSeconds = 0.35f;
    [Min(0.05f)] public float retractSeconds = 0.4f;
    public float timeOffset = 0f;
    public bool startExtended = false;
    [Tooltip("Allow hits only when the spikes have visibly emerged.")]
    [Range(0f, 1f)] public float damagingExtension = 0.55f;

    [Header("Damage")]
    [Min(0)] public float damage = 1f;
    [Min(0.05f)] public float collisionRadius = 0.30f;
    public LayerMask playerLayers = ~0;

    [Header("Visual materials (assigned automatically by prefab builder)")]
    public Material baseMaterial;
    public Material spikeMaterial;
    public Material warningMaterial;

    private const string VisualName = "THORN_VISUALS_GENERATED";
    private Transform visuals;
    private readonly List<Transform> spikes = new List<Transform>();
    private readonly List<Renderer> warningLights = new List<Renderer>();
    private readonly Collider[] hitBuffer = new Collider[32];
    private float radius;
    private Vector3 center;
    private float extension;
    private bool damaging;
    private int lastHash = int.MinValue;
    private float nextEditorRefresh;
    private float fixedAngle = 90f;
    private float fixedAxial;
    private bool anchorReady;
    private Transform previousPivot;

    public float Extension { get { return extension; } }
    public bool IsDangerous { get { return damaging; } }

    private void OnEnable() { lastHash = int.MinValue; anchorReady = false; }

    private void OnValidate()
    {
        manualRadius = Mathf.Max(.1f, manualRadius);
        arcLength = Mathf.Max(.5f, arcLength);
        stripWidth = Mathf.Max(.2f, stripWidth);
        spikeHeight = Mathf.Max(.15f, spikeHeight);
        spikeBaseRadius = Mathf.Max(.06f, spikeBaseRadius);
        lastHash = int.MinValue;
    }

    private void Update()
    {
        if (!gameObject.scene.IsValid()) return;
        // Avoid touching prefab assets / prefab editing stage.
#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif
        if (!Application.isPlaying && Time.realtimeSinceStartup < nextEditorRefresh) return;
        if (!Application.isPlaying) nextEditorRefresh = Time.realtimeSinceStartup + .15f;

        FindPivot();
        if (!cylinderPivot) return;
        ResolveCylinder();
        if (autoSnap) SnapToCylinder();
        else if (!anchorReady) CaptureAnchor();

        int h = HashGeometry();
        if (visuals == null || h != lastHash)
        {
            BuildVisuals();
            lastHash = h;
        }

        extension = Application.isPlaying ? CalculateExtension(Time.time + timeOffset) : 0f;
        damaging = Application.isPlaying && extension >= damagingExtension;
        AnimateSpikes();
        if (damaging) CheckDamage();
    }

    private void FindPivot()
    {
        if (!cylinderPivot)
        {
            CylinderRotator rotator = FindObjectOfType<CylinderRotator>();
            if (rotator) cylinderPivot = rotator.transform;
            else
            {
                GameObject obj = GameObject.Find("CylinderPivot");
                if (obj) cylinderPivot = obj.transform;
            }
        }
        if (!cylinderPivot) return;
        if (previousPivot != cylinderPivot) { anchorReady = false; previousPivot = cylinderPivot; }
        // Preserve WORLD position while parenting, so dropping anywhere near the barrel works.
        if (transform.parent != cylinderPivot)
        {
            transform.SetParent(cylinderPivot, true);
            anchorReady = false;
            lastHash = int.MinValue;
        }
    }

    private void ResolveCylinder()
    {
        radius = manualRadius;
        center = manualCenterOffset;
        if (!autoDetectCylinder) return;
        MeshFilter best = null;
        int bestScore = -1000;
        foreach (MeshFilter f in cylinderPivot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!f || !f.sharedMesh || f.GetComponentInParent<CurvedThornTrap>()) continue;
            string n = f.name.ToLowerInvariant();
            int score = n.Contains("cylinderground") ? 100 :
                n.Contains("hollowcylinder") ? 90 :
                n.Contains("cylinder") ? 70 :
                n.Contains("barrel") || n.Contains("tube") ? 50 : -100;
            if (score > bestScore) { bestScore = score; best = f; }
        }
        if (!best || bestScore < 0) return;
        Bounds b = best.sharedMesh.bounds;
        Vector3 c = cylinderPivot.InverseTransformPoint(best.transform.TransformPoint(b.center));
        // Bounds of a circular tube in XY give a square; use half its XY span,
        // not the diagonal, so spikes do not hover away from the barrel.
        Vector3 x = cylinderPivot.InverseTransformVector(best.transform.TransformVector(new Vector3(b.extents.x, 0f, 0f)));
        Vector3 y = cylinderPivot.InverseTransformVector(best.transform.TransformVector(new Vector3(0f, b.extents.y, 0f)));
        float rx = new Vector2(x.x, x.y).magnitude;
        float ry = new Vector2(y.x, y.y).magnitude;
        float detected = Mathf.Max(rx, ry);
        if (detected > .2f) { radius = detected; center = c + manualCenterOffset; }
    }

    private void CaptureAnchor()
    {
        Vector3 relative = cylinderPivot.InverseTransformPoint(transform.position) - center;
        // A new prefab at pivot origin has no meaningful angle: default to top.
        if (new Vector2(relative.x, relative.y).sqrMagnitude > .05f)
            fixedAngle = Mathf.Atan2(relative.y, relative.x) * Mathf.Rad2Deg;
        fixedAxial = relative.z;
        anchorReady = true;
    }

    private void SnapToCylinder()
    {
        if (!anchorReady) CaptureAnchor();
        // Moving the prefab in Scene view changes its angle / axial offset.
        Vector3 relative = cylinderPivot.InverseTransformPoint(transform.position) - center;
        float currentRadius = new Vector2(relative.x, relative.y).magnitude;
        float targetRadius = radius + surfaceOffset;
        Vector3 expected = new Vector3(Mathf.Cos(fixedAngle * Mathf.Deg2Rad), Mathf.Sin(fixedAngle * Mathf.Deg2Rad), 0f) * targetRadius + Vector3.forward * fixedAxial;
        if ((relative - expected).sqrMagnitude > 0.0025f)
        {
            if (currentRadius > 0.1f)
                fixedAngle = Mathf.Atan2(relative.y, relative.x) * Mathf.Rad2Deg;
            fixedAxial = relative.z;
        }
        float a = fixedAngle * Mathf.Deg2Rad;
        Vector3 normal = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
        transform.localPosition = center + normal * targetRadius + Vector3.forward * fixedAxial;
        transform.localRotation = Quaternion.LookRotation(Vector3.forward, normal);
        // Position and rotation are kept in cylinder-local coordinates.
    }

    // Positions along the curved strip, in pivot local space.
    private Vector3 StripPoint(float t, float zOffset, float radialHeight)
    {
        float a = (fixedAngle + (t - .5f) * arcLength / Mathf.Max(.1f, radius) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        float r = radius + surfaceOffset + radialHeight;
        return center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, fixedAxial + zOffset);
    }
    private Vector3 NormalAt(float t)
    {
        float a = (fixedAngle + (t - .5f) * arcLength / Mathf.Max(.1f, radius) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
    }
    private Vector3 LocalPoint(Vector3 pivotPoint) { return transform.InverseTransformPoint(cylinderPivot.TransformPoint(pivotPoint)); }
    private Quaternion LocalOrientation(Vector3 normal)
    {
        Quaternion world = Quaternion.LookRotation(cylinderPivot.forward, cylinderPivot.TransformDirection(normal));
        return Quaternion.Inverse(transform.rotation) * world;
    }

    private int HashGeometry()
    {
        unchecked
        {
            int h = 17;
            h = 31*h + fixedAngle.GetHashCode(); h = 31*h + fixedAxial.GetHashCode();
            h = 31*h + radius.GetHashCode(); h = 31*h + center.GetHashCode();
            h = 31*h + arcLength.GetHashCode(); h = 31*h + stripWidth.GetHashCode();
            h = 31*h + spikeCount; h = 31*h + spikeRows; h = 31*h + spikeHeight.GetHashCode();
            h = 31*h + spikeBaseRadius.GetHashCode(); h = 31*h + spikeSides;
            h = 31*h + surfaceOffset.GetHashCode();
            return h;
        }
    }

    private GameObject Primitive(string name, PrimitiveType type, Transform parent, Material mat)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        Collider c = obj.GetComponent<Collider>();
        if (c) { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
        Renderer r = obj.GetComponent<Renderer>();
        if (r && mat) r.sharedMaterial = mat;
        return obj;
    }

    private Mesh ConeMesh()
    {
        int sides = Mathf.Clamp(spikeSides, 4, 10);
        Vector3[] vertices = new Vector3[sides * 2 + 2];
        int[] triangles = new int[sides * 6];
        vertices[0] = new Vector3(0, 1, 0);  // pointed tip
        vertices[1] = Vector3.zero;          // bottom center
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2 / sides;
            vertices[i + 2] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            vertices[i + 2 + sides] = vertices[i + 2];
        }
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            int k = i * 6;
            triangles[k] = 0; triangles[k+1] = i + 2; triangles[k+2] = j + 2;
            triangles[k+3] = 1; triangles[k+4] = j + 2 + sides; triangles[k+5] = i + 2 + sides;
        }
        Mesh mesh = new Mesh(); mesh.name = "Procedural Thorn Cone";
        mesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        mesh.vertices = vertices; mesh.triangles = triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    public void BuildVisuals()
    {
        if (!gameObject.scene.IsValid()) return;
        Transform previous = transform.Find(VisualName);
        if (previous)
        {
            if (Application.isPlaying) Destroy(previous.gameObject);
            else DestroyImmediate(previous.gameObject);
        }
        spikes.Clear(); warningLights.Clear();
        GameObject group = new GameObject(VisualName);
        group.transform.SetParent(transform, false); visuals = group.transform;
        Mesh cone = ConeMesh();
        for (int i = 0; i < spikeCount; i++)
        {
            float t = spikeCount == 1 ? .5f : (i + .5f) / spikeCount;
            Vector3 normal = NormalAt(t);
            for (int j = 0; j < spikeRows; j++)
            {
                float offset = spikeRows == 1 ? 0f : ((j + .5f) / spikeRows - .5f) * stripWidth;
                // dark metallic socket visible even when spikes retract
                GameObject socket = Primitive("Socket " + i + "-" + j,
                    PrimitiveType.Cylinder, visuals, baseMaterial);
                socket.transform.localPosition = LocalPoint(StripPoint(t, offset, .05f));
                socket.transform.localRotation = LocalOrientation(normal);
                socket.transform.localScale = new Vector3(spikeBaseRadius * 2.8f, .045f, spikeBaseRadius * 2.8f);

                GameObject thorn = new GameObject("Animated Thorn " + i + "-" + j);
                thorn.transform.SetParent(visuals, false);
                thorn.transform.localPosition = LocalPoint(StripPoint(t, offset, .065f));
                thorn.transform.localRotation = LocalOrientation(normal);
                MeshFilter mf = thorn.AddComponent<MeshFilter>(); mf.sharedMesh = cone;
                MeshRenderer mr = thorn.AddComponent<MeshRenderer>();
                if (spikeMaterial) mr.sharedMaterial = spikeMaterial;
                spikes.Add(thorn.transform);
                // A tiny red light warns that spikes are about to emerge.
                if (i % 3 == 0 && j == 0)
                {
                    GameObject light = Primitive("Warning lamp " + i,
                        PrimitiveType.Sphere, visuals, warningMaterial);
                    light.transform.localPosition = LocalPoint(StripPoint(t, -stripWidth * .65f, .10f));
                    light.transform.localScale = Vector3.one * .16f;
                    warningLights.Add(light.GetComponent<Renderer>());
                }
            }
        }
        AnimateSpikes();
    }

    private float CalculateExtension(float clock)
    {
        // Stowed -> rises -> extended hold -> falls -> stowed hold.
        float cycle = extendSeconds + extendedSeconds + retractSeconds + retractedSeconds;
        float t = Mathf.Repeat(clock, Mathf.Max(.1f, cycle));
        if (startExtended)
            t = Mathf.Repeat(t + extendSeconds, cycle);
        if (t < extendSeconds) return Smooth(t / extendSeconds);
        t -= extendSeconds;
        if (t < extendedSeconds) return 1f;
        t -= extendedSeconds;
        if (t < retractSeconds) return 1f - Smooth(t / retractSeconds);
        return 0f;
    }
    private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    private void AnimateSpikes()
    {
        float height = Mathf.Lerp(.025f, spikeHeight, extension);
        foreach (Transform t in spikes)
            if (t) t.localScale = new Vector3(spikeBaseRadius, height, spikeBaseRadius);
    }

    private void CheckDamage()
    {
        for (int i = 0; i < spikeCount; i++)
        {
            float t = (i + .5f) / spikeCount;
            for (int row = 0; row < spikeRows; row++)
            {
                float z = spikeRows == 1 ? 0f : ((row + .5f) / spikeRows - .5f) * stripWidth;
                Vector3 start = cylinderPivot.TransformPoint(StripPoint(t, z, .1f));
                Vector3 end = cylinderPivot.TransformPoint(StripPoint(t, z, spikeHeight * extension));
                int count = Physics.OverlapCapsuleNonAlloc(start, end,
                    collisionRadius, hitBuffer, playerLayers, QueryTriggerInteraction.Collide);
                for (int k = 0; k < count; k++)
                {
                    Collider c = hitBuffer[k];
                    if (!c) continue;
                    PlayerHealth hp = c.GetComponentInParent<PlayerHealth>();
                    if (hp) { hp.TakeDamage(damage); return; }
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!cylinderPivot) return;
        Gizmos.color = Color.red;
        Vector3 last = cylinderPivot.TransformPoint(StripPoint(0f, 0f, 0f));
        for (int i = 1; i <= 40; i++)
        {
            Vector3 next = cylinderPivot.TransformPoint(StripPoint(i / 40f, 0f, 0f));
            Gizmos.DrawLine(last, next); last = next;
        }
    }
}
