using UnityEngine;

public class testmovementtest : MonoBehaviour
{
    [SerializeField] private float radius = 2f;
    [SerializeField] private float speed = 1f;

    private Vector3 center;

    private void Start()
    {
        center = transform.position;
    }

    private void Update()
    {
        float angle = Time.time * speed;
        transform.position = center + new Vector3(
            Mathf.Cos(angle) * radius,
            0f,
            Mathf.Sin(angle) * radius
        );
    }
}
