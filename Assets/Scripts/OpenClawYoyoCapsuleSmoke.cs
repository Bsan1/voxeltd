using UnityEngine;

[AddComponentMenu("")]
public sealed class OpenClawYoyoCapsuleSmoke : MonoBehaviour
{
    private const float MinY = 5f;
    private const float MaxY = 10f;
    private const float Speed = 2f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "OpenClaw_Yoyo_Capsule";
        go.transform.localScale = Vector3.one;
        go.AddComponent<OpenClawYoyoCapsuleSmoke>();
    }

    private void Update()
    {
        float y = Mathf.PingPong(Time.time * Speed, MaxY - MinY) + MinY;
        transform.position = new Vector3(0f, y, 0f);
    }
}
