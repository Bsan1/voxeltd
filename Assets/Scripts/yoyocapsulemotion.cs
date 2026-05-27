using UnityEngine;

/// <summary>
/// Runtime-only capsule that moves up and down between (0, 5, 0) and (0, 10, 0).
/// The bootstrap creates the capsule when Play Mode starts, avoiding scene edits/saves.
/// </summary>
public sealed class YoyoCapsuleMotion : MonoBehaviour
{
    private const string CapsuleName = "YoyoCapsule";

    [SerializeField] private Vector3 lowerPoint = new Vector3(0f, 5f, 0f);
    [SerializeField] private Vector3 upperPoint = new Vector3(0f, 10f, 0f);
    [SerializeField] private float cyclesPerSecond = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeCapsule()
    {
        if (GameObject.Find(CapsuleName) != null)
        {
            return;
        }

        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = CapsuleName;
        capsule.transform.position = new Vector3(0f, 5f, 0f);
        capsule.AddComponent<YoyoCapsuleMotion>();
    }

    private void OnValidate()
    {
        cyclesPerSecond = Mathf.Max(0.01f, cyclesPerSecond);
    }

    private void Update()
    {
        float t = Mathf.PingPong(Time.time * cyclesPerSecond, 1f);
        transform.position = Vector3.Lerp(lowerPoint, upperPoint, t);
    }
}
