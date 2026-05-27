using UnityEngine;

/// <summary>
/// Moves this object in a horizontal circle centered on world origin.
/// The cube is placed at (0, 0, 0) in edit mode, then orbits around that point on the X/Z plane during Play Mode.
/// </summary>
public class CircularMovementAroundOrigin : MonoBehaviour
{
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private float radius = 2f;
    [SerializeField] private float degreesPerSecond = 90f;

    private float angleDegrees;

    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
    }

    private void Update()
    {
        angleDegrees += degreesPerSecond * Time.deltaTime;
        float radians = angleDegrees * Mathf.Deg2Rad;

        transform.position = center + new Vector3(
            Mathf.Cos(radians) * radius,
            0f,
            Mathf.Sin(radians) * radius
        );
    }
}
