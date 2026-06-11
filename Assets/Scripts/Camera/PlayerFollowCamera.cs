using UnityEngine;

// Orthographic, fixed-angle camera that follows the player ball with smoothing.
// The angle is constant (default 35° downward tilt + a yaw for the isometric look);
// only the POSITION tracks the target, since a rolling ball has no meaningful facing.
// Position is eased with SmoothDamp so the camera glides instead of snapping.
[RequireComponent(typeof(Camera))]
public sealed class PlayerFollowCamera : MonoBehaviour
{
    [Header("Target (auto-finds PlayerBall if empty)")]
    [SerializeField] private Transform target;

    [Header("Angle")]
    [Tooltip("Downward tilt of the camera in degrees.")]
    [SerializeField, Range(0f, 89f)] private float pitch = 35f;
    [Tooltip("Rotation around Y — gives the isometric/diagonal look. 0 = straight-on.")]
    [SerializeField] private float yaw = 45f;

    [Header("Framing")]
    [Tooltip("How far back the camera sits along its view direction (orthographic, so this only sets placement/clipping, not zoom).")]
    [SerializeField] private float distance = 20f;
    [Tooltip("Orthographic size = half the vertical view height in world units. Smaller = more zoomed in.")]
    [SerializeField, Min(0.1f)] private float orthographicSize = 8f;
    [Tooltip("Offset of the aim point from the target (e.g. raise it slightly).")]
    [SerializeField] private Vector3 lookOffset = Vector3.zero;

    [Header("Smoothing")]
    [Tooltip("Approx. seconds for the camera to catch up to the target. 0 = instant.")]
    [SerializeField, Min(0f)] private float smoothTime = 0.18f;

    private Camera cam;
    private Vector3 velocity;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthographicSize;

        ResolveTarget();

        // Start framed on the target so there is no initial glide from wherever the
        // camera happened to sit.
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        if (target != null)
        {
            transform.position = DesiredPosition();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            ResolveTarget();
            if (target == null)
            {
                return;
            }
        }

        // Keep angle/size live so they can be tuned in the Inspector during Play.
        cam.orthographicSize = orthographicSize;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        transform.position = Vector3.SmoothDamp(
            transform.position, DesiredPosition(), ref velocity, smoothTime);
    }

    private void ResolveTarget()
    {
        if (target != null)
        {
            return;
        }

        PlayerBall ball = FindFirstObjectByType<PlayerBall>();
        if (ball != null)
        {
            target = ball.transform;
        }
    }

    // Where the camera should sit to keep the target centred at the current angle.
    private Vector3 DesiredPosition()
    {
        Vector3 viewDir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        Vector3 aim = (target != null ? target.position : Vector3.zero) + lookOffset;
        return aim - viewDir * distance;
    }
}
