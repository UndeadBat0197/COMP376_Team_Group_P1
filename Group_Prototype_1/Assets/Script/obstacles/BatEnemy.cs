
using UnityEngine;
using UnityEngine.SceneManagement;

public class BatEnemy : MonoBehaviour
{
    [Header("References (found automatically)")]
    public PlayerHealth playerHealth;
    public Transform cylinderPivot;

    [Header("Cylinder Geometry")]
    public float outerRadius = 15f;
    public bool autoDetectRadius = true;
    public bool autoDetectLength = true;
    public float patrolHalfLength = 12f;
    public float endMargin = 2f;

    [Tooltip("Distance from the cylinder surface")]
    public float spawnHeight = 0.8f;

    public Vector3 centerOffset = Vector3.zero;

    [Header("Helical Patrol")]
    public float oneWayTravelTime = 6f;
    public float loopsPerTraversal = 1.5f;
    public float initialAngleDegrees = 0f;
    public float hoverHeight = 0.05f;
    public float hoverSpeed = 3f;

    [Header("Attack")]
    public float detectionRange = 35f;
    public float warningTime = 1.0f;
    public float diveSpeed = 22f;
    public float returnSpeed = 17f;
    public float hitRadius = 1.0f;
    public float damage = 1f;
    public float minCooldown = 3f;
    public float maxCooldown = 5f;
    public float attackSurfaceClearance = 0.7f;

    [Header("Visual")]
    public float bodySize = 1.2f;
    public Transform visual;

    enum BatState
    {
        Patrol,
        Warning,
        Attack,
        Return
    }

    BatState state;

    Transform cylinderMesh;
    CharacterController playerController;
    DeluxeBatVisual deluxe;

    Vector3 cylinderCenter;
    Vector3 lockedTarget;

    float patrolTime;
    float nextAttackTime;
    float warningDeadline;
    float attackTime;
    float attackDuration;

    float startAxial;
    float targetAxial;
    float startAngle;
    float targetAngle;
    float startRadius;
    float targetRadius;

    bool didDamage;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= SceneReady;
        SceneManager.sceneLoaded += SceneReady;

        SceneReady(
            SceneManager.GetActiveScene(),
            LoadSceneMode.Single
        );
    }

    static void SceneReady(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "environment_test_scene")
            return;

        if (FindObjectOfType<BatEnemy>() != null)
            return;

        GameObject prefab =
            Resources.Load<GameObject>("BatEnemy");

        if (prefab != null)
            Instantiate(prefab);
    }

    void Start()
    {
        if (!playerHealth)
            playerHealth = FindObjectOfType<PlayerHealth>();

        if (!cylinderPivot)
        {
            var rotator =
                FindObjectOfType<CylinderRotator>();

            if (rotator)
                cylinderPivot = rotator.transform;
        }

        if (!playerHealth || !cylinderPivot)
        {
            Debug.LogError(
                "BatEnemy: PlayerHealth or CylinderRotator not found",
                this
            );

            enabled = false;
            return;
        }

        playerController =
            playerHealth.GetComponent<CharacterController>();

        FindCylinderGeometry();

        if (!visual)
        {
            visual =
                new GameObject("Deluxe Bat Visual").transform;

            visual.SetParent(transform, false);
            visual.localScale = Vector3.one * bodySize;
        }

        deluxe = visual.GetComponent<DeluxeBatVisual>();

        if (!deluxe)
            deluxe =
                visual.gameObject.AddComponent<DeluxeBatVisual>();

        deluxe.Build();
        deluxe.SetAlert(false);

        state = BatState.Patrol;

        transform.position = PatrolPoint();

        ScheduleAttack();
    }

    Vector3 Center()
    {
        return cylinderPivot.TransformPoint(
            cylinderCenter + centerOffset
        );
    }

    Vector3 Axis()
    {
        return cylinderPivot.forward.normalized;
    }

    Vector3 Up()
    {
        Vector3 v = Vector3.ProjectOnPlane(
            Vector3.up,
            Axis()
        );

        if (v.sqrMagnitude < 0.00001f)
        {
            v = Vector3.ProjectOnPlane(
                cylinderPivot.up,
                Axis()
            );
        }

        return v.normalized;
    }

    Vector3 Side()
    {
        return Vector3.Cross(Axis(), Up()).normalized;
    }

    float Axial(Vector3 p)
    {
        return Vector3.Dot(p - Center(), Axis());
    }

    Vector3 Radial(Vector3 p)
    {
        return Vector3.ProjectOnPlane(
            p - Center(),
            Axis()
        );
    }

    float Angle(Vector3 p)
    {
        Vector3 r = Radial(p);

        return Mathf.Atan2(
            Vector3.Dot(r, Side()),
            Vector3.Dot(r, Up())
        ) * Mathf.Rad2Deg;
    }

    Vector3 Surface(
        float axial,
        float degrees,
        float radius)
    {
        float a = degrees * Mathf.Deg2Rad;

        return Center()
            + Axis() * axial
            + (
                Up() * Mathf.Cos(a)
                + Side() * Mathf.Sin(a)
            ) * radius;
    }

    void FindCylinderGeometry()
    {
        MeshFilter chosen = null;
        float best = -1f;

        foreach (
            var f in cylinderPivot
                .GetComponentsInChildren<MeshFilter>(true))
        {
            if (!f || !f.sharedMesh ||
                !f.gameObject.activeInHierarchy)
                continue;

            string name = f.name.ToLowerInvariant();

            if (!name.Contains("cylind") &&
                !name.Contains("tube") &&
                !name.Contains("pipe") &&
                !name.Contains("ground"))
                continue;

            Bounds b = f.sharedMesh.bounds;

            Vector3 ws =
                f.transform.TransformVector(b.size);

            float score =
                Mathf.Abs(ws.x * ws.y * ws.z);

            if (score > best)
            {
                best = score;
                chosen = f;
            }
        }

        if (!chosen)
        {
            foreach (
                var f in cylinderPivot
                    .GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f || !f.sharedMesh ||
                    !f.gameObject.activeInHierarchy)
                    continue;

                Bounds b = f.sharedMesh.bounds;

                Vector3 s =
                    f.transform.TransformVector(b.size);

                float score =
                    Mathf.Abs(s.x * s.y * s.z);

                if (score > best)
                {
                    best = score;
                    chosen = f;
                }
            }
        }

        if (!chosen)
        {
            cylinderCenter = Vector3.zero;

            Debug.LogWarning(
                "BatEnemy: Cannot find cylinder mesh",
                this
            );

            return;
        }

        cylinderMesh = chosen.transform;

        Bounds meshBounds = chosen.sharedMesh.bounds;

        Vector3 meshCenter =
            chosen.transform.TransformPoint(
                meshBounds.center
            );

        cylinderCenter =
            cylinderPivot.InverseTransformPoint(
                meshCenter
            );

        float minA = float.PositiveInfinity;
        float maxA = float.NegativeInfinity;
        float maxR = 0f;

        Vector3 e = meshBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 p =
                chosen.transform.TransformPoint(
                    meshBounds.center
                    + Vector3.Scale(
                        e,
                        new Vector3(x, y, z)
                    )
                );

            float a =
                Vector3.Dot(p - meshCenter, Axis());

            minA = Mathf.Min(minA, a);
            maxA = Mathf.Max(maxA, a);

            maxR = Mathf.Max(
                maxR,
                Vector3.ProjectOnPlane(
                    p - meshCenter,
                    Axis()
                ).magnitude
            );
        }

        if (autoDetectLength && maxA - minA > 3f)
        {
            patrolHalfLength =
                (maxA - minA) * 0.5f;
        }

        if (autoDetectRadius && maxR > 3f)
        {
            outerRadius =
                maxR / Mathf.Sqrt(2f);
        }

        Debug.Log(
            $"BatEnemy: mesh={chosen.name}, " +
            $"radius={outerRadius:0.0}, " +
            $"halfLength={patrolHalfLength:0.0}",
            this
        );
    }

    Vector3 PatrolPoint()
    {
        float t = Mathf.PingPong(
            patrolTime /
                Mathf.Max(1f, oneWayTravelTime),
            1f
        );

        float half = Mathf.Max(
            1f,
            patrolHalfLength - Mathf.Max(0f, endMargin)
        );

        float axial =
            Mathf.Lerp(-half, half, t);

        float angle =
            initialAngleDegrees
            + t * 360f * loopsPerTraversal;

        // FIX: No bodySize-based minimum height.
        float clearance =
            Mathf.Max(0.05f, spawnHeight);

        float radius =
            outerRadius
            + clearance
            + Mathf.Sin(Time.time * hoverSpeed)
              * hoverHeight;

        return Surface(axial, angle, radius);
    }

    Vector3 PlayerPoint()
    {
        if (playerController &&
            playerController.enabled)
        {
            return playerController.bounds.center;
        }

        return playerHealth.transform.position;
    }

    bool PlayerIsOutside()
    {
        if (playerHealth.IsDead)
            return false;

        Vector3 p = PlayerPoint();

        return Mathf.Abs(Axial(p))
                   < patrolHalfLength + 4f
            && Radial(p).magnitude
                   > outerRadius - 2.5f;
    }

    bool CanAttack()
    {
        if (!PlayerIsOutside())
            return false;

        Vector3 p = PlayerPoint();

        return Vector3.Distance(
                   transform.position,
                   p
               ) <= detectionRange
            && Vector3.Dot(
                   Radial(transform.position).normalized,
                   Radial(p).normalized
               ) > -0.1f;
    }

    void StartAttack()
    {
        Vector3 p = PlayerPoint();

        float r = Mathf.Max(
            outerRadius + Mathf.Max(
                attackSurfaceClearance,
                bodySize * 0.8f
            ),
            Radial(p).magnitude
        );

        lockedTarget = Surface(
            Axial(p),
            Angle(p),
            r
        );

        warningDeadline =
            Time.time + Mathf.Max(0.25f, warningTime);

        state = BatState.Warning;

        deluxe.SetAlert(true);
    }

    void StartDive()
    {
        deluxe.SetAlert(false);

        state = BatState.Attack;
        didDamage = false;

        startAxial = Axial(transform.position);
        targetAxial = Axial(lockedTarget);

        startAngle = Angle(transform.position);

        targetAngle = startAngle
            + Mathf.DeltaAngle(
                startAngle,
                Angle(lockedTarget)
            );

        startRadius =
            Radial(transform.position).magnitude;

        targetRadius =
            Radial(lockedTarget).magnitude;

        float arcDistance =
            Mathf.Abs(targetAngle - startAngle)
            * Mathf.Deg2Rad
            * Mathf.Max(startRadius, targetRadius);

        float length = Mathf.Sqrt(
            arcDistance * arcDistance
            + Mathf.Pow(
                targetAxial - startAxial,
                2f
            )
        );

        attackDuration = Mathf.Max(
            0.25f,
            length / Mathf.Max(1f, diveSpeed)
        );

        attackTime = 0f;
    }

    bool HitPlayer(Vector3 a, Vector3 b)
    {
        Vector3 p = PlayerPoint();
        Vector3 d = b - a;

        float f = d.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(
                Vector3.Dot(p - a, d)
                / d.sqrMagnitude
            )
            : 0f;

        Vector3 closest = a + f * d;

        if (playerController &&
            playerController.enabled)
        {
            Vector3 onPlayer =
                playerController.ClosestPoint(closest);

            return Vector3.Distance(
                closest,
                onPlayer
            ) <= hitRadius;
        }

        return Vector3.Distance(
            closest,
            p
        ) <= hitRadius + 0.75f;
    }

    void FaceMotion(Vector3 move)
    {
        if (move.sqrMagnitude < 0.0001f || !visual)
            return;

        Vector3 up =
            Radial(transform.position).normalized;

        Vector3 forward = move.normalized;

        if (Mathf.Abs(
                Vector3.Dot(forward, up)
            ) > 0.97f)
        {
            up = Vector3.up;
        }

        visual.rotation =
            Quaternion.LookRotation(forward, up);
    }

    void ReturnToPatrol()
    {
        deluxe.SetAlert(false);

        state = BatState.Return;
    }

    void ScheduleAttack()
    {
        nextAttackTime =
            Time.time + Random.Range(
                Mathf.Max(0.1f, minCooldown),
                Mathf.Max(minCooldown, maxCooldown)
            );
    }

    void Update()
    {
        if (!playerHealth || !cylinderPivot)
            return;

        float dt = Time.deltaTime;

        patrolTime += dt;

        Vector3 patrol = PatrolPoint();

        switch (state)
        {
            case BatState.Patrol:
            {
                Vector3 last = transform.position;

                transform.position = patrol;

                FaceMotion(
                    transform.position - last
                );

                if (Time.time >= nextAttackTime &&
                    CanAttack())
                {
                    StartAttack();
                }

                break;
            }

            case BatState.Warning:
            {
                if (!PlayerIsOutside())
                {
                    ReturnToPatrol();
                    break;
                }

                FaceMotion(
                    lockedTarget - transform.position
                );

                if (Time.time >= warningDeadline)
                {
                    StartDive();
                }

                break;
            }

            case BatState.Attack:
            {
                if (!PlayerIsOutside())
                {
                    ReturnToPatrol();
                    break;
                }

                Vector3 old = transform.position;

                attackTime += dt;

                float t = Mathf.Clamp01(
                    attackTime / attackDuration
                );

                float radius = Mathf.Max(
                    outerRadius + Mathf.Max(
                        attackSurfaceClearance,
                        bodySize * 0.8f
                    ),
                    Mathf.Lerp(
                        startRadius,
                        targetRadius,
                        t
                    )
                );

                transform.position = Surface(
                    Mathf.Lerp(
                        startAxial,
                        targetAxial,
                        t
                    ),
                    Mathf.Lerp(
                        startAngle,
                        targetAngle,
                        t
                    ),
                    radius
                );

                FaceMotion(
                    transform.position - old
                );

                if (!didDamage &&
                    HitPlayer(
                        old,
                        transform.position
                    ))
                {
                    didDamage = true;

                    playerHealth.TakeDamage(damage);

                    ReturnToPatrol();
                }
                else if (t >= 1f)
                {
                    ReturnToPatrol();
                }

                break;
            }

            case BatState.Return:
            {
                Vector3 before = transform.position;

                // FIX: Use spawnHeight for return altitude.
                float safeRadius =
                    outerRadius
                    + Mathf.Max(0.05f, spawnHeight);

                if (Radial(transform.position).magnitude
                    < safeRadius - 0.05f)
                {
                    transform.position = Surface(
                        Axial(transform.position),
                        Angle(transform.position),
                        Mathf.MoveTowards(
                            Radial(transform.position).magnitude,
                            safeRadius,
                            returnSpeed * dt
                        )
                    );
                }
                else
                {
                    float a =
                        Angle(transform.position);

                    float destA = Angle(patrol);

                    float angularSpeed =
                        returnSpeed
                        / Mathf.Max(0.1f, safeRadius)
                        * Mathf.Rad2Deg;

                    float nextA = Mathf.MoveTowardsAngle(
                        a,
                        destA,
                        angularSpeed * dt
                    );

                    float nextAxis = Mathf.MoveTowards(
                        Axial(transform.position),
                        Axial(patrol),
                        returnSpeed * dt
                    );

                    transform.position = Surface(
                        nextAxis,
                        nextA,
                        safeRadius
                    );

                    if (Mathf.Abs(
                            Mathf.DeltaAngle(
                                nextA,
                                destA
                            )
                        ) < 3f
                        && Mathf.Abs(
                            nextAxis - Axial(patrol)
                        ) < 0.8f)
                    {
                        transform.position = patrol;

                        state = BatState.Patrol;

                        ScheduleAttack();
                    }
                }

                FaceMotion(
                    transform.position - before
                );

                break;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );

        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;

            Gizmos.DrawWireSphere(
                Center(),
                outerRadius
            );

            if (state == BatState.Warning)
            {
                Gizmos.color = Color.red;

                Gizmos.DrawLine(
                    transform.position,
                    lockedTarget
                );
            }
        }
    }
}
