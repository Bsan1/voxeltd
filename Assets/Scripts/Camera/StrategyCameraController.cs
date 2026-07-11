using UnityEngine;

[RequireComponent(typeof(Camera))]
public class StrategyCameraController : MonoBehaviour
{
    [Header("Pan")]
    public float panSpeed = 8f;
    public bool enableEdgePan = true;
    public float edgePanPixels = 24f;

    [Header("Zoom")]
    [SerializeField] private bool enableScrollZoom = false;
    public float zoomSpeed = 18f;
    public float minHeight = 4f;
    public float maxHeight = 18f;

    [Header("Rotate")]
    public float rotateSpeed = 90f;
    public bool enableMiddleMouseRotate = true;

    private Camera controlledCamera;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
    }

    private void Update()
    {
        HandlePan();
        HandleZoom();
        HandleRotation();
    }

    private void HandlePan()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (enableEdgePan)
        {
            Vector3 mouse = Input.mousePosition;
            if (mouse.x >= 0f && mouse.x <= Screen.width && mouse.y >= 0f && mouse.y <= Screen.height)
            {
                if (mouse.x <= edgePanPixels)
                {
                    input.x -= 1f;
                }
                else if (mouse.x >= Screen.width - edgePanPixels)
                {
                    input.x += 1f;
                }

                if (mouse.y <= edgePanPixels)
                {
                    input.y -= 1f;
                }
                else if (mouse.y >= Screen.height - edgePanPixels)
                {
                    input.y += 1f;
                }
            }
        }

        if (input.sqrMagnitude <= 0.001f)
        {
            return;
        }

        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 movement = (right * input.x + forward * input.y) * (panSpeed * Time.deltaTime);
        transform.position += movement;
    }

    private void HandleZoom()
    {
        if (!enableScrollZoom)
        {
            return;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) <= 0.001f)
        {
            return;
        }

        Vector3 nextPosition = transform.position + transform.forward * (scroll * zoomSpeed * Time.deltaTime);
        nextPosition.y = Mathf.Clamp(nextPosition.y, minHeight, maxHeight);
        transform.position = nextPosition;
    }

    private void HandleRotation()
    {
        float rotateInput = 0f;

        if (Input.GetKey(KeyCode.Q))
        {
            rotateInput -= 1f;
        }

        if (Input.GetKey(KeyCode.E))
        {
            rotateInput += 1f;
        }

        if (Mathf.Abs(rotateInput) > 0.001f)
        {
            transform.Rotate(Vector3.up, rotateInput * rotateSpeed * Time.deltaTime, Space.World);
        }

        if (enableMiddleMouseRotate && Input.GetMouseButton(2))
        {
            float mouseDelta = Input.GetAxis("Mouse X");
            transform.Rotate(Vector3.up, mouseDelta * rotateSpeed * Time.deltaTime, Space.World);
        }

        if (controlledCamera != null)
        {
            transform.eulerAngles = new Vector3(
                Mathf.Clamp(NormalizeAngle(transform.eulerAngles.x), 35f, 75f),
                transform.eulerAngles.y,
                0f);
        }
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
