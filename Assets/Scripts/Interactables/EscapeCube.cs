using UnityEngine;

[DisallowMultipleComponent]
public class EscapeCube : MonoBehaviour
{
    [SerializeField] float teleportRadius = 10f;
    [SerializeField] string playerTag = "Player";
    [SerializeField] Vector3 triggerColliderSize = Vector3.one * 1.2f;

    public float TeleportRadius
    {
        get => teleportRadius;
        set => teleportRadius = Mathf.Max(0f, value);
    }

    public string PlayerTag
    {
        get => playerTag;
        set => playerTag = value;
    }

    public Vector3 TriggerColliderSize => triggerColliderSize;

    public int PhysicsColliderCount => CountBoxColliders(false);

    public int TriggerColliderCount => CountBoxColliders(true);

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other)) return;

        TeleportWithinRadius();
    }

    void Reset()
    {
        ConfigureDefaultColliders();
    }

    [ContextMenu("Configure Default Colliders")]
    public void ConfigureDefaultColliders()
    {
        BoxCollider[] colliders = GetComponents<BoxCollider>();

        BoxCollider physicsCollider = colliders.Length > 0 ? colliders[0] : gameObject.AddComponent<BoxCollider>();
        physicsCollider.isTrigger = false;
        physicsCollider.center = Vector3.zero;
        physicsCollider.size = Vector3.one;

        BoxCollider triggerCollider = null;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != physicsCollider)
            {
                triggerCollider = colliders[i];
                break;
            }
        }

        if (!triggerCollider)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }

        triggerCollider.isTrigger = true;
        triggerCollider.center = Vector3.zero;
        triggerCollider.size = triggerColliderSize;
    }

    public void TeleportWithinRadius()
    {
        transform.position += CreateTeleportOffset();
    }

    bool IsPlayerCollider(Collider other)
    {
        if (!other || string.IsNullOrWhiteSpace(playerTag)) return false;

        return other.CompareTag(playerTag)
            || (other.attachedRigidbody && other.attachedRigidbody.CompareTag(playerTag))
            || (other.transform.root && other.transform.root.CompareTag(playerTag));
    }

    int CountBoxColliders(bool trigger)
    {
        int count = 0;
        BoxCollider[] colliders = GetComponents<BoxCollider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].isTrigger == trigger)
            {
                count++;
            }
        }

        return count;
    }

    Vector3 CreateTeleportOffset()
    {
        if (teleportRadius <= 0f) return Vector3.zero;

        Vector3 offset = Random.insideUnitSphere * teleportRadius;
        if (offset.sqrMagnitude > 0.0001f) return offset;

        return Vector3.right * Mathf.Min(teleportRadius, 0.1f);
    }
}
