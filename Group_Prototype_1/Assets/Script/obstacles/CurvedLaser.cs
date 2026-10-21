using System.Collections.Generic;
using UnityEngine;

// A laser that can bend.
//
// Create an empty GameObject (GameObject > Create Empty), add this component.
//
// ARC mode:   the laser curves around this object's local Z axis, like a ring segment.
//             Put the object on the cylinder's centre line (child of CylinderPivot,
//             position 0,0,z, rotation 0,0,0) and it wraps around the cylinder.
//
// POINTS mode: the laser passes through this object's child objects, in Hierarchy order.
//             Drag the children around with the Move tool (W) to bend it however you like.
//             Right-click the component header > "Add Point" to add another one.
//
// Parent it to anything that moves/rotates and it follows along, still dealing damage.
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class CurvedLaser : MonoBehaviour
{
    public enum Shape { Arc, Points }

    [Header("Shape")]
    public Shape shape = Shape.Arc;

    [Tooltip("How many little straight pieces the curve is made of. Higher = smoother.")]
    [Range(2, 200)] public int resolution = 48;

    [Header("Arc settings")]
    [Tooltip("Distance from this object's centre to the beam. Cylinder radius is 7.5, so 8.5 floats just above it.")]
    public float radius = 8.5f;

    [Tooltip("Where the arc starts. 0 = right, 90 = top, 180 = left, 270 = bottom. " +
             "For an arc over the top, this is the RIGHT end: raise it to shorten from the right.")]
    public float startAngle = 45f;

    [Tooltip("Where the arc ends (going counter-clockwise from Start). " +
             "For an arc over the top, this is the LEFT end: lower it to shorten from the left.")]
    public float endAngle = 135f;

    [Header("Points settings")]
    [Tooltip("Smoothly curve through the points instead of sharp corners.")]
    public bool smooth = true;

    [Header("Trim (works in both modes)")]
    [Tooltip("Cut this fraction off the start of the beam. 0.25 = remove the first quarter.")]
    [Range(0f, 1f)] public float trimStart = 0f;
    [Tooltip("Cut this fraction off the end of the beam. 0.25 = remove the last quarter.")]
    [Range(0f, 1f)] public float trimEnd = 0f;

    [Header("Beam look")]
    public float thickness = 0.25f;
    public Color laserColor = new Color(1f, 0.1f, 0.1f, 1f);
    [Tooltip("Optional. Leave empty to use a simple default material.")]
    public Material laserMaterial;

    [Header("Damage")]
    [Range(0f, 1f)] public float damageFraction = 1f / 3f;
    public LayerMask hitLayers = ~0;

    [Header("Blinking (optional)")]
    public bool blink = false;
    public float onDuration = 2f;
    public float offDuration = 1f;
    public float timeOffset = 0f;

    public bool IsOn { get; private set; } = true;

    private LineRenderer line;
    private readonly List<Vector3> path = new List<Vector3>();
    private static Material defaultMaterial;

    void OnEnable()
    {
        line = GetComponent<LineRenderer>();
        SetupLine();
        RebuildPath();
    }

    void OnValidate()
    {
        thickness = Mathf.Max(0.01f, thickness);
        radius = Mathf.Max(0.01f, radius);
        onDuration = Mathf.Max(0f, onDuration);
        offDuration = Mathf.Max(0f, offDuration);

        // Keep the arc between 1 and 360 degrees long.
        endAngle = Mathf.Clamp(endAngle, startAngle + 1f, startAngle + 360f);

        // Don't let the two trims overlap.
        if (trimStart + trimEnd > 0.99f)
            trimEnd = Mathf.Max(0f, 0.99f - trimStart);

        if (line != null) SetupLine();
    }

    // Used by the Scene-view handles.
    public Vector3 ArcPoint(float angleDeg)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        Vector3 local = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
        return transform.position + transform.rotation * local;
    }

    public IReadOnlyList<Vector3> Path => path;

    // Runs after all movement/rotation this frame, so the beam is never one frame behind.
    void LateUpdate()
    {
        RebuildPath();

        if (!Application.isPlaying)
            return;

        UpdateBlink();

        if (IsOn)
            CheckForPlayer();
    }

    // ---------------- Building the curve ----------------

    void RebuildPath()
    {
        path.Clear();

        if (shape == Shape.Arc)
            BuildArc();
        else
            BuildFromPoints();

        ApplyTrim();

        if (line == null) return;
        line.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
            line.SetPosition(i, path[i]);
    }

    void BuildArc()
    {
        // ArcPoint uses position + rotation only (ignores scale), so a stretched
        // parent like the cylinder doesn't squash the arc into an oval.
        for (int i = 0; i <= resolution; i++)
            path.Add(ArcPoint(Mathf.Lerp(startAngle, endAngle, (float)i / resolution)));
    }

    // Cuts trimStart / trimEnd off the beam, measured along its real length.
    void ApplyTrim()
    {
        if (path.Count < 2 || (trimStart <= 0f && trimEnd <= 0f))
            return;

        float total = 0f;
        for (int i = 0; i < path.Count - 1; i++)
            total += Vector3.Distance(path[i], path[i + 1]);
        if (total <= 0f) return;

        float from = total * trimStart;
        float to = total * (1f - trimEnd);

        var trimmed = new List<Vector3>();
        float walked = 0f;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 a = path[i], b = path[i + 1];
            float len = Vector3.Distance(a, b);
            float segStart = walked, segEnd = walked + len;

            if (segEnd >= from && segStart <= to && len > 0f)
            {
                if (trimmed.Count == 0)
                    trimmed.Add(Vector3.Lerp(a, b, Mathf.Clamp01((from - segStart) / len)));

                if (segEnd <= to)
                    trimmed.Add(b);
                else
                {
                    trimmed.Add(Vector3.Lerp(a, b, Mathf.Clamp01((to - segStart) / len)));
                    break;
                }
            }
            walked = segEnd;
        }

        path.Clear();
        path.AddRange(trimmed);
    }

    void BuildFromPoints()
    {
        int n = transform.childCount;
        if (n < 2) return;

        var pts = new List<Vector3>(n);
        for (int i = 0; i < n; i++)
            pts.Add(transform.GetChild(i).position);

        if (!smooth)
        {
            path.AddRange(pts);
            return;
        }

        int perSpan = Mathf.Max(1, resolution / (n - 1));
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(i - 1, 0)];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = pts[Mathf.Min(i + 2, n - 1)];

            for (int s = 0; s < perSpan; s++)
                path.Add(CatmullRom(p0, p1, p2, p3, (float)s / perSpan));
        }
        path.Add(pts[n - 1]);
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 +
                       (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // ---------------- Look ----------------

    void SetupLine()
    {
        line.useWorldSpace = true;
        line.loop = false;
        line.widthMultiplier = thickness;
        line.startColor = laserColor;
        line.endColor = laserColor;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = laserMaterial != null ? laserMaterial : GetDefaultMaterial();
    }

    static Material GetDefaultMaterial()
    {
        if (defaultMaterial == null)
        {
            // Unlit shader that uses the LineRenderer's colour. Works in URP and built-in.
            defaultMaterial = new Material(Shader.Find("Sprites/Default"));
            defaultMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        return defaultMaterial;
    }

    void UpdateBlink()
    {
        bool shouldBeOn = true;
        if (blink && onDuration + offDuration > 0f)
        {
            float t = Mathf.Repeat(Time.time + timeOffset, onDuration + offDuration);
            shouldBeOn = t < onDuration;
        }

        if (shouldBeOn != IsOn)
        {
            IsOn = shouldBeOn;
            line.enabled = IsOn;
        }
    }

    // ---------------- Damage ----------------

    void CheckForPlayer()
    {
        float r = thickness * 0.5f;

        // Check a capsule along every little piece of the curve.
        for (int i = 0; i < path.Count - 1; i++)
        {
            Collider[] hits = Physics.OverlapCapsule(
                path[i], path[i + 1], r, hitLayers, QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
                if (health != null)
                {
                    health.TakeDamageFraction(damageFraction);
                    return;
                }
            }
        }
    }

    // ---------------- Editor helpers ----------------

    // Right-click the component header in the Inspector to use this.
    [ContextMenu("Add Point")]
    void AddPoint()
    {
        shape = Shape.Points;

        var p = new GameObject("Point " + transform.childCount);
        p.transform.SetParent(transform, false);

        int n = transform.childCount;
        if (n >= 3)
        {
            // Continue on from the last two points.
            Vector3 last = transform.GetChild(n - 2).localPosition;
            Vector3 prev = transform.GetChild(n - 3).localPosition;
            p.transform.localPosition = last + (last - prev);
        }
        else
        {
            p.transform.localPosition = new Vector3(0f, 0f, (n - 1) * 3f);
        }

#if UNITY_EDITOR
        // Lets Ctrl+Z undo it and makes sure the scene saves the new point.
        UnityEditor.Undo.RegisterCreatedObjectUndo(p, "Add Laser Point");
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.Selection.activeGameObject = p;
#endif
    }

    void OnDrawGizmos()
    {
        if (shape == Shape.Points)
        {
            // Little spheres on each point so they're easy to click and drag.
            Gizmos.color = Color.yellow;
            for (int i = 0; i < transform.childCount; i++)
                Gizmos.DrawWireSphere(transform.GetChild(i).position, Mathf.Max(0.2f, thickness));
        }
        else
        {
            // Shows the axis the arc wraps around, so you can line it up with the cylinder.
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Vector3 axis = transform.forward * 2f;
            Gizmos.DrawLine(transform.position - axis, transform.position + axis);
        }
    }
}
