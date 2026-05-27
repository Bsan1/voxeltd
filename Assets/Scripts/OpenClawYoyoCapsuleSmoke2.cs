using UnityEngine;

public sealed class OpenClawYoyoCapsuleSmoke2 : MonoBehaviour
{
    private const float BottomY = 5f;
    private const float TopY = 10f;
    private const float Speed = 2f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "OpenClaw_Yoyo_Capsule_2";
        capsule.transform.localScale = Vector3.one;
        capsule.AddComponent<OpenClawYoyoCapsuleSmoke2>();
    }

    private void Update()
    {
        float offsetY = Mathf.PingPong(Time.time * Speed, TopY - BottomY);
        transform.position = new Vector3(0f, BottomY + offsetY, 0f);
    }
}
