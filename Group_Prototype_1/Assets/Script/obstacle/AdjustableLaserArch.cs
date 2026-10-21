
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class AdjustableLaserArch : MonoBehaviour
{
    [Header("Cylinder Settings")]
    public float cylinderRadius = 7.5f;
    public float surfaceClearance = 0.35f;

    [Header("Laser Shape")]
    [Range(20f, 180f)]
    public float arcDegrees = 140f;

    [Range(-180f, 180f)]
    public float centerAngle = 90f;

    [Range(4, 64)]
    public int segments = 24;

    [Min(0.02f)]
    public float beamWidth = 0.2f;

    public float zOffset = 0f;

    [Header("Appearance")]
    public Material redLaserMaterial;

    private const string SegmentPrefix = "LaserHitbox_";
    private bool needsRebuild = true;

    private void OnEnable()
    {
        needsRebuild = true;
    }

    private void OnValidate()
    {
        needsRebuild = true;
    }

    private void Update()
    {
        if (needsRebuild)
        {
            needsRebuild = false;
            BuildLaser();
        }
    }

    [ContextMenu("Rebuild Laser")]
    public void BuildLaser()
    {
        needsRebuild = false;

        int count = Mathf.Clamp(segments, 4, 64);

        float radius = Mathf.Max(
            0.1f,
            cylinderRadius + surfaceClearance
        );

        float thickness = Mathf.Max(
            0.02f,
            beamWidth
        );

        // Create visible curved laser
        LineRenderer line = GetComponent<LineRenderer>();

        line.useWorldSpace = false;
        line.positionCount = count + 1;

        line.startWidth = thickness;
        line.endWidth = thickness;

        line.numCornerVertices = 5;
        line.numCapVertices = 5;

        line.startColor = Color.red;
        line.endColor = Color.red;

        if (redLaserMaterial != null)
        {
            line.sharedMaterial = redLaserMaterial;
        }

        float startAngle =
            centerAngle - arcDegrees / 2f;

        float step = arcDegrees / count;

        // Calculate points along the arch
        for (int i = 0; i <= count; i++)
        {
            float angle =
                (startAngle + i * step) * Mathf.Deg2Rad;

            Vector3 point = new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                zOffset
            );

            line.SetPosition(i, point);
        }

        // Create invisible collision boxes
        for (int i = 0; i < count; i++)
        {
            string objectName = SegmentPrefix + i;

            Transform hitbox = transform.Find(objectName);

            if (hitbox == null)
            {
                GameObject obj = new GameObject(objectName);

                hitbox = obj.transform;
                hitbox.SetParent(transform, false);

                BoxCollider newCollider =
                    obj.AddComponent<BoxCollider>();

                newCollider.isTrigger = true;

                Rigidbody rb =
                    obj.AddComponent<Rigidbody>();

                rb.isKinematic = true;
                rb.useGravity = false;

                // Restart the level when player touches laser
                obj.AddComponent<LaserHazard>();
            }

            Vector3 start = line.GetPosition(i);
            Vector3 end = line.GetPosition(i + 1);

            Vector3 direction = end - start;

            hitbox.localPosition = (start + end) / 2f;

            hitbox.localRotation =
                Quaternion.FromToRotation(
                    Vector3.right,
                    direction.normalized
                );

            hitbox.localScale = Vector3.one;

            BoxCollider box =
                hitbox.GetComponent<BoxCollider>();

            box.isTrigger = true;
            box.center = Vector3.zero;

            box.size = new Vector3(
                direction.magnitude + 0.05f,
                thickness * 1.4f,
                thickness * 1.4f
            );
        }

        // Remove extra hitboxes if segment count decreases
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (!child.name.StartsWith(SegmentPrefix))
                continue;

            string suffix =
                child.name.Substring(SegmentPrefix.Length);

            if (int.TryParse(suffix, out int index)
                && index >= count)
            {
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
