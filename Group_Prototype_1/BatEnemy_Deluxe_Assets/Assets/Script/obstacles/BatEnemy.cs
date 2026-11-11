using UnityEngine;
using UnityEngine.SceneManagement;

// Spiral-patrol bat. Safe to replace the previous BatEnemy.cs without changing its GUID.
public class BatEnemy : MonoBehaviour
{
    [Header("References (automatically found if empty)")]
    public PlayerHealth playerHealth;
    public Transform cylinderPivot;

    [Header("Cylinder / outside-only")]
    public float outerRadius = 15f;
    public bool autoDetectRadius = true;
    public bool autoDetectLength = true;
    [Tooltip("Distance from cylinder center to either end along its axis.")]
    public float patrolHalfLength = 12f;
    [Tooltip("How far the bat stays above the outer surface.")]
    public float spawnHeight = 4f;
    public float endMargin = 2f;

    [Header("Helical patrol")]
    [Tooltip("Time to travel from one end to the other, then reverse.")]
    public float oneWayTravelTime = 12f;
    [Tooltip("Number of full loops around the drum during one one-way trip.")]
    public float loopsPerTraversal = 1.5f;
    public float initialAngleDegrees = 0f;
    public float hoverHeight = 0.25f;
    public float hoverSpeed = 3f;

    [Header("Attack")]
    public float detectionRange = 18f;
    public float warningTime = 0.9f;
    public float diveSpeed = 16f;
    public float returnSpeed = 10f;
    public float hitRadius = 0.7f;
    public float damage = 1f;
    public float minCooldown = 3f;
    public float maxCooldown = 5f;
    [Tooltip("Keep bat center this far above the outer shell when diving.")]
    public float attackSurfaceClearance = 0.9f;

    [Header("Visual")]
    public float bodySize = 1.7f;
    public Transform visual;

    enum State { Patrol, Warning, Dive, Retreat }
    State state = State.Patrol;
    float patrolClock, warningEnds, nextAttack;
    Vector3 attackTarget;
    bool alreadyHit;
    CharacterController playerController;
    Renderer[] visualRenderers;
    MaterialPropertyBlock propertyBlock;
    Transform leftWing, rightWing;
    DeluxeBatVisual deluxeVisual;
    readonly Color idleColor = new Color(0.26f, 0.12f, 0.45f);
    readonly Color warningColor = new Color(1f, 0.1f, 0.08f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "environment_test_scene" || FindObjectOfType<BatEnemy>()) return;
        GameObject prefab = Resources.Load<GameObject>("BatEnemy");
        if (prefab) Instantiate(prefab);
    }

    void Start()
    {
        if (!playerHealth) playerHealth = FindObjectOfType<PlayerHealth>();
        if (!cylinderPivot)
        {
            CylinderRotator rotation = FindObjectOfType<CylinderRotator>();
            if (rotation) cylinderPivot = rotation.transform;
        }
        if (!playerHealth || !cylinderPivot)
        {
            Debug.LogError("BatEnemy requires PlayerHealth and CylinderRotator.", this);
            enabled = false;
            return;
        }
        playerController = playerHealth.GetComponent<CharacterController>();
        DetectCylinderDimensions();
        if (!visual) BuildVisual();
        deluxeVisual = visual.GetComponent<DeluxeBatVisual>();
        visualRenderers = visual.GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();
        SetWarning(false);
        transform.position = HelixPosition(0f);
        ScheduleAttack();
    }

    void Update()
    {
        if (!playerHealth || !cylinderPivot) return;
        float dt = Time.deltaTime;
        FlapWings();
        // Continue the patrol timer while attacking, so the bat can rejoin a moving route.
        patrolClock += dt;
        Vector3 patrolPoint = HelixPosition(patrolClock);
        Vector3 playerPoint = PlayerCenter();

        switch (state)
        {
            case State.Patrol:
                Face(patrolPoint - transform.position);
                transform.position = patrolPoint;
                if (Time.time >= nextAttack && CanAttack(playerPoint))
                {
                    // Lock the player's current position at warning START, allowing a dodge.
                    attackTarget = SafeAttackPoint(playerPoint);
                    warningEnds = Time.time + Mathf.Max(0.05f, warningTime);
                    state = State.Warning;
                    SetWarning(true);
                }
                break;

            case State.Warning:
                Face(attackTarget - transform.position);
                if (!IsPlayerOutside() || !SafeSegment(transform.position, attackTarget))
                {
                    BeginRetreat();
                    break;
                }
                if (Time.time >= warningEnds)
                {
                    SetWarning(false);
                    alreadyHit = false;
                    state = State.Dive;
                }
                break;

            case State.Dive:
                if (!IsPlayerOutside() || !SafeSegment(transform.position, attackTarget))
                {
                    BeginRetreat();
                    break;
                }
                Vector3 before = transform.position;
                Vector3 after = Vector3.MoveTowards(before, attackTarget, Mathf.Max(1f, diveSpeed) * dt);
                Face(after - before);
                transform.position = after;
                if (!alreadyHit && TouchesPlayer(before, after))
                {
                    alreadyHit = true;
                    playerHealth.TakeDamage(damage); // Existing health bar handles the update.
                    BeginRetreat();
                }
                else if ((after - attackTarget).sqrMagnitude < 0.0025f) BeginRetreat();
                break;

            case State.Retreat:
                // First rise away from the shell, then slide around its rim to meet the helix.
                float safeR = outerRadius + Mathf.Max(spawnHeight, attackSurfaceClearance + hitRadius + .5f);
                Vector3 axisPoint = AxisPoint(transform.position);
                Vector3 radial = Radial(transform.position);
                Vector3 currentDir = radial.sqrMagnitude > .0001f ? radial.normalized : WorldUpOnCrossSection();
                Vector3 orbitDir = Radial(patrolPoint).normalized;
                float currentRadius = radial.magnitude;
                if (currentRadius < safeR - .15f)
                {
                    Vector3 climb = axisPoint + currentDir * safeR;
                    transform.position = Vector3.MoveTowards(transform.position, climb, Mathf.Max(1f, returnSpeed) * dt);
                }
                else
                {
                    float radians = Mathf.Max(0.01f, returnSpeed / safeR) * dt;
                    Vector3 newDir = Vector3.RotateTowards(currentDir, orbitDir, radians, 0f).normalized;
                    float axial = Mathf.MoveTowards(Vector3.Dot(transform.position - cylinderPivot.position, Axis()),
                        Vector3.Dot(patrolPoint - cylinderPivot.position, Axis()), returnSpeed * dt);
                    Vector3 orbit = cylinderPivot.position + Axis() * axial + newDir * safeR;
                    Face(orbit - transform.position);
                    transform.position = orbit;
                    if (Vector3.Angle(newDir, orbitDir) < 4f && Vector3.Distance(transform.position, patrolPoint) < 1f)
                    {
                        state = State.Patrol;
                        transform.position = patrolPoint;
                        ScheduleAttack();
                    }
                }
                break;
        }
    }

    Vector3 Axis() => cylinderPivot.forward.normalized; // Rotator turns around local Z.
    Vector3 WorldUpOnCrossSection()
    {
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, Axis());
        if (up.sqrMagnitude < .0001f) up = Vector3.ProjectOnPlane(Vector3.right, Axis());
        return up.normalized;
    }
    Vector3 AxisPoint(Vector3 p) => cylinderPivot.position + Axis() * Vector3.Dot(p - cylinderPivot.position, Axis());
    Vector3 Radial(Vector3 p) => p - AxisPoint(p);

    Vector3 HelixPosition(float seconds)
    {
        float half = Mathf.Max(.5f, patrolHalfLength - endMargin);
        float period = Mathf.Max(1f, oneWayTravelTime);
        float travel = Mathf.PingPong(seconds / period, 1f); // 0 -> 1 -> 0
        float axial = Mathf.Lerp(-half, half, travel);
        float angle = (initialAngleDegrees + travel * 360f * loopsPerTraversal) * Mathf.Deg2Rad;
        Vector3 up = WorldUpOnCrossSection();
        Vector3 side = Vector3.Cross(Axis(), up).normalized;
        Vector3 radial = Mathf.Cos(angle) * up + Mathf.Sin(angle) * side;
        float radius = outerRadius + Mathf.Max(1f, spawnHeight) + Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;
        return cylinderPivot.position + Axis() * axial + radial * radius;
    }

    void DetectCylinderDimensions()
    {
        // Prefer the large cylinder mesh, but allow manual overrides for unusual imported meshes.
        if (!autoDetectRadius && !autoDetectLength) return;
        MeshFilter[] filters = cylinderPivot.GetComponentsInChildren<MeshFilter>(true);
        float bestSize = -1f;
        float foundRadius = outerRadius, foundHalf = patrolHalfLength;
        foreach (MeshFilter filter in filters)
        {
            if (!filter.gameObject.activeInHierarchy || !filter.sharedMesh) continue;
            Vector3[] vertices = filter.sharedMesh.vertices;
            if (vertices == null || vertices.Length == 0) continue;
            float minA = float.PositiveInfinity, maxA = float.NegativeInfinity, maxR = 0f;
            // Inspect actual mesh vertices: bounds corners overestimate round cylinders.
            int stride = Mathf.Max(1, vertices.Length / 15000);
            for (int i = 0; i < vertices.Length; i += stride)
            {
                Vector3 world = filter.transform.TransformPoint(vertices[i]);
                float axial = Vector3.Dot(world - cylinderPivot.position, Axis());
                minA = Mathf.Min(minA, axial); maxA = Mathf.Max(maxA, axial);
                maxR = Mathf.Max(maxR, Radial(world).magnitude);
            }
            float span = maxA - minA;
            float score = span * maxR;
            if (span > 5f && maxR > 3f && score > bestSize)
            {
                bestSize = score; foundRadius = maxR; foundHalf = Mathf.Max(Mathf.Abs(minA), Mathf.Abs(maxA));
            }
        }
        if (bestSize > 0f)
        {
            if (autoDetectRadius) outerRadius = foundRadius;
            if (autoDetectLength) patrolHalfLength = foundHalf;
        }
        else Debug.LogWarning("BatEnemy: cylinder dimensions not found; using prefab defaults. Verify radius and half length.", this);
    }

    Vector3 PlayerCenter() => playerController ? playerController.bounds.center : playerHealth.transform.position;
    bool IsPlayerOutside()
    {
        if (playerHealth.IsDead) return false;
        Vector3 p = PlayerCenter();
        float a = Vector3.Dot(p - cylinderPivot.position, Axis());
        return Mathf.Abs(a) <= patrolHalfLength + 1f && Radial(p).magnitude >= outerRadius - 0.3f;
    }
    bool CanAttack(Vector3 p) => IsPlayerOutside() && Vector3.Distance(transform.position, p) <= detectionRange
        && SafeSegment(transform.position, SafeAttackPoint(p));
    Vector3 SafeAttackPoint(Vector3 p)
    {
        Vector3 r = Radial(p);
        float targetRadius = outerRadius + Mathf.Max(attackSurfaceClearance, hitRadius + .2f);
        if (r.magnitude >= targetRadius) return p;
        Vector3 outward = r.sqrMagnitude > .0001f ? r.normalized : WorldUpOnCrossSection();
        return AxisPoint(p) + outward * targetRadius;
    }
    bool SafeSegment(Vector3 from, Vector3 to)
    {
        // Check the full segment, not just the endpoints. Prevent flight through the drum.
        Vector3 a = Radial(from), d = Radial(to) - a;
        float t = d.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector3.Dot(a,d)/d.sqrMagnitude) : 0f;
        return (a + d*t).magnitude >= outerRadius + .05f;
    }
    bool TouchesPlayer(Vector3 from, Vector3 to)
    {
        if (!playerController) return Vector3.Distance(to, playerHealth.transform.position) < hitRadius + 1f;
        // Closest point from the bat's swept segment to the player's controller capsule center.
        Vector3 center = playerController.bounds.center;
        Vector3 segment = to - from;
        float t = segment.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector3.Dot(center - from, segment) / segment.sqrMagnitude) : 0f;
        Vector3 nearest = from + segment * t;
        Vector3 pointOnPlayer = playerController.ClosestPoint(nearest);
        return Vector3.Distance(nearest, pointOnPlayer) <= hitRadius;
    }
    void Face(Vector3 delta)
    {
        if (!visual || delta.sqrMagnitude < .0001f) return;
        Vector3 forward = delta.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > .98f ? Axis() : Vector3.up;
        visual.rotation = Quaternion.LookRotation(forward, up);
    }
    void BeginRetreat() { SetWarning(false); state = State.Retreat; }
    void ScheduleAttack() { nextAttack = Time.time + Random.Range(minCooldown, Mathf.Max(minCooldown, maxCooldown)); }
    void SetWarning(bool warning)
    {
        if (deluxeVisual) { deluxeVisual.SetAlert(warning); return; }
        if (visualRenderers == null) return;
        foreach (Renderer r in visualRenderers)
        {
            r.GetPropertyBlock(propertyBlock);
            Color c = warning ? warningColor : idleColor;
            propertyBlock.SetColor("_BaseColor", c);
            propertyBlock.SetColor("_Color", c);
            r.SetPropertyBlock(propertyBlock);
        }
    }
    void FlapWings()
    {
        if (deluxeVisual) return; // Detailed wing animation is handled by DeluxeBatVisual.
        if (!leftWing || !rightWing) return;
        float flap = Mathf.Sin(Time.time * 14f) * 35f;
        leftWing.localRotation = Quaternion.Euler(0f, 0f, flap);
        rightWing.localRotation = Quaternion.Euler(0f, 0f, -flap);
    }
    void BuildVisual()
    {
        // Creates a fully procedural deluxe mesh. No imported models or materials are required.
        visual = new GameObject("Deluxe Bat Visual").transform;
        visual.SetParent(transform, false);
        visual.localPosition = Vector3.zero;
        visual.localScale = Vector3.one * bodySize;
        deluxeVisual = visual.gameObject.AddComponent<DeluxeBatVisual>();
        deluxeVisual.Build();
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        if (Application.isPlaying && state == State.Warning)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, attackTarget);
        }
    }
}
