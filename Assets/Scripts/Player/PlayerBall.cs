using UnityEngine;

// A physics-driven player ball. Rolls under CAMERA-RELATIVE WASD / arrow input by
// applying force to a Rigidbody with a SphereCollider, so motion (rolling, momentum,
// bumping the maze walls, settling on the floor) emerges from the physics engine.
// Tuned to feel heavy: high mass resists the agent crowd, and a fall multiplier
// makes it drop faster after the jump apex WITHOUT changing jump height (the ascent
// uses normal gravity; only the descent is heavier). Jump is variable — release
// early for a short hop. Uses the legacy Input axes (project Active Input = Both).
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public sealed class PlayerBall : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Roll acceleration (units/s^2). Mass-independent (applied as Acceleration).")]
    [SerializeField] private float acceleration = 30f;
    [Tooltip("Top horizontal rolling speed (units/s).")]
    [SerializeField] private float maxSpeed = 8f;

    [Header("Friction / Stopping")]
    [Tooltip("Horizontal deceleration when grounded with no input. Medium values create a short roll-out before stopping.")]
    [SerializeField, Min(0f)] private float stoppingFriction = 8f;

    [Header("Air Control")]
    [Tooltip("0..1 multiplier on movement acceleration while airborne, so mid-air steering is weaker than ground steering.")]
    [SerializeField, Range(0f, 1f)] private float airControl = 0.5f;

    [Header("Weight")]
    [Tooltip("Rigidbody mass. Higher = the agent crowd shoves the ball around less.")]
    [SerializeField, Min(0.1f)] private float mass = 8f;

    [Header("Max Fall Speed")]
    [Tooltip("Terminal velocity cap for downward Y speed only. Upward motion and horizontal speed are unaffected.")]
    [SerializeField, Min(0f)] private float maxFallSpeed = 25f;

    [Header("Bounciness")]
    [Tooltip("0..1 bounciness applied through a runtime PhysicsMaterial on the SphereCollider.")]
    [SerializeField, Range(0f, 1f)] private float bounciness = 0.1f;

    [Header("Turn Responsiveness")]
    [Tooltip("Extra acceleration multiplier when input opposes the current horizontal velocity.")]
    [SerializeField, Min(0f)] private float turnResponsiveness = 2f;

    [Header("Jump")]
    [SerializeField] private bool allowJump = true;
    [Tooltip("Upward launch speed (m/s). Applied as VelocityChange so jump height is independent of mass.")]
    [SerializeField] private float jumpForce = 5f;
    [Tooltip("Extra gravity while FALLING -> the ball meets the ground faster. 1 = normal.")]
    [SerializeField, Min(1f)] private float fallMultiplier = 2.5f;
    [Tooltip("Extra gravity while RISING with jump released -> short hop (variable jump). 1 = no cut.")]
    [SerializeField, Min(1f)] private float lowJumpMultiplier = 2.5f;

    private Rigidbody body;
    private SphereCollider sphereCollider;
    private Transform cam;
    private bool jumpHeld;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>();
        body.isKinematic = false;
        body.useGravity = true;
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        sphereCollider.material = new PhysicsMaterial
        {
            bounciness = bounciness,
            bounceCombine = PhysicsMaterialCombine.Maximum
        };
        ResolveCamera();
    }

    private void Update()
    {
        jumpHeld = Input.GetButton("Jump");
        if (allowJump && Input.GetButtonDown("Jump") && IsGrounded())
        {
            // VelocityChange -> launch speed (and therefore jump height) does not
            // depend on mass, so making the ball heavy doesn't shrink the jump.
            Vector3 v = body.linearVelocity;
            v.y = 0f;
            body.linearVelocity = v;
            body.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }
    }

    private void FixedUpdate()
    {
        Vector3 moveDirection = ReadMoveDirection();
        bool grounded = IsGrounded();
        float controlMultiplier = grounded ? 1f : airControl;

        body.AddForce(moveDirection * acceleration * controlMultiplier, ForceMode.Acceleration);
        ApplyCounterSteer(moveDirection, controlMultiplier);
        ApplyStoppingFriction(moveDirection, grounded);
        ApplyWeight();
        ClampFallSpeed();
        ClampHorizontalSpeed();
    }

    // WASD relative to the camera's facing (projected onto the ground), so "up" is
    // always "away from the camera" regardless of the fixed isometric angle. Falls
    // back to world axes only if no camera can be found.
    private Vector3 ReadMoveDirection()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (cam == null)
        {
            ResolveCamera();
        }

        Vector3 dir;
        if (cam != null)
        {
            Vector3 forward = cam.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = cam.right;
            right.y = 0f;
            right.Normalize();
            dir = right * h + forward * v;
        }
        else
        {
            dir = new Vector3(h, 0f, v);
        }

        if (dir.sqrMagnitude > 1f)
        {
            dir.Normalize();
        }

        return dir;
    }

    // Heavier feel: pile extra gravity on while airborne so the ball drops fast
    // after the apex (fallMultiplier); if jump was released mid-rise, cut the ascent
    // (lowJumpMultiplier) for a tap-able short hop. Held jumps keep full height
    // because the rise otherwise uses normal gravity.
    private void ApplyWeight()
    {
        if (IsGrounded())
        {
            return;
        }

        float vy = body.linearVelocity.y;
        if (vy < 0f)
        {
            body.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (vy > 0f && !jumpHeld)
        {
            body.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    private void ApplyStoppingFriction(Vector3 moveDirection, bool grounded)
    {
        if (!grounded || moveDirection.sqrMagnitude > 0.0001f)
        {
            return;
        }

        Vector3 vel = body.linearVelocity;
        Vector3 flat = new Vector3(vel.x, 0f, vel.z);
        flat = Vector3.MoveTowards(flat, Vector3.zero, stoppingFriction * Time.fixedDeltaTime);
        body.linearVelocity = new Vector3(flat.x, vel.y, flat.z);
    }

    private void ApplyCounterSteer(Vector3 moveDirection, float controlMultiplier)
    {
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 vel = body.linearVelocity;
        Vector3 flat = new Vector3(vel.x, 0f, vel.z);
        if (flat.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 inputDir = moveDirection.normalized;
        Vector3 velocityDir = flat.normalized;
        if (Vector3.Dot(inputDir, velocityDir) < 0f)
        {
            body.AddForce(inputDir * acceleration * turnResponsiveness * controlMultiplier, ForceMode.Acceleration);
        }
    }

    private void ClampFallSpeed()
    {
        Vector3 vel = body.linearVelocity;
        if (vel.y < -maxFallSpeed)
        {
            body.linearVelocity = new Vector3(vel.x, -maxFallSpeed, vel.z);
        }
    }

    private void ClampHorizontalSpeed()
    {
        Vector3 vel = body.linearVelocity;
        Vector3 flat = new Vector3(vel.x, 0f, vel.z);
        if (flat.magnitude > maxSpeed)
        {
            flat = flat.normalized * maxSpeed;
            body.linearVelocity = new Vector3(flat.x, vel.y, flat.z);
        }
    }

    private void ResolveCamera()
    {
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            return;
        }

        Camera any = FindFirstObjectByType<Camera>();
        if (any != null)
        {
            cam = any.transform;
        }
    }

    private bool IsGrounded()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        float reach = (col != null ? col.bounds.extents.y : 0.5f) + 0.1f;
        return Physics.Raycast(transform.position, Vector3.down, reach);
    }
}
