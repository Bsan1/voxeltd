using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FPSController: MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    PlayerLookCaster lookCaster;
    IPickupable heldItem;
    [SerializeField] GameObject hand;

    [Header("Movement")]
    public float walkSpeed = 4.5f;
    public float sprintMultiplier = 1.6f;
    public float jumpHeight = 1.2f;
    [Tooltip("Use negative gravity, e.g. -9.81")]
    public float gravity = -9.81f;

    [Header("Mouse Look")]
    public float sensitivity = 1.5f;
    [Range(-89f, 0f)] public float minPitch = -85f;
    [Range(0f, 89f)] public float maxPitch = 85f;

    CharacterController controller;
    float pitch;        // camera pitch (x-rotation)
    float verticalVel;  // y velocity

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (!playerCamera) Debug.LogError($"There is no camera assigned on FPSController {this.name}");
    }

    private void Start()
    {
        lookCaster = GetComponent<PlayerLookCaster>();
    }

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
    }

    void Update()
    {
        Look();
        Move();
        HandleInteraction();
    }

    private void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E))
            lookCaster.hitInteractable?.Interact();
        if (Input.GetKeyDown(KeyCode.G))
        {
            heldItem?.Drop();
            heldItem = null;
        }
        if(Input.GetMouseButtonDown(0))
        {
            if(heldItem == null)
            {
                heldItem = lookCaster.hitInteractable?.ReturnGameobject().GetComponent<IPickupable>();
                heldItem?.Pickup(hand.transform);
            }
            else
            {
                heldItem.Throw(transform.forward + Vector3.up * .15f,10);
                heldItem = null;
            }    
        }
    }

    void Look()
    {
        // old input system mouse delta
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        // yaw (rotate body)
        transform.Rotate(Vector3.up * (mouseX * sensitivity));

        // pitch (rotate camera)
        pitch -= mouseY * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        if (playerCamera) playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        bool grounded = controller.isGrounded;

        // WASD via old input axes (set up by default)
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 move = (transform.right * x + transform.forward * z).normalized;

        bool sprinting = Input.GetKey(KeyCode.LeftShift);
        float speed = (sprinting ? walkSpeed * sprintMultiplier : walkSpeed);

        if (grounded && verticalVel < 0f)
            verticalVel = -2f; // small stick-to-ground

        // jump on Space when grounded
        if (grounded && Input.GetButtonDown("Jump"))
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // gravity
        verticalVel += gravity * Time.deltaTime;

        Vector3 velocity = move * speed + Vector3.up * verticalVel;
        controller.Move(velocity * Time.deltaTime);
    }
}
