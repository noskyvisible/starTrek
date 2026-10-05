using StarTrek.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarTrek.Player
{
    /// <summary>
    /// First-person walking for ship interiors and away missions. The transform origin sits at the
    /// player's feet; the camera pivot is a child placed at eye height.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] InputActionAsset actions;

        [Header("View")]
        [SerializeField] Transform cameraPivot;
        [SerializeField] float mouseSensitivity = 0.08f;   // degrees per mouse count
        [SerializeField] float stickLookSpeed = 160f;      // degrees per second at full deflection
        [SerializeField] float pitchLimit = 85f;

        [Header("Movement (m/s)")]
        [SerializeField] float walkSpeed = 3.2f;
        [SerializeField] float sprintSpeed = 5.5f;
        [SerializeField] float crouchSpeed = 1.6f;
        [SerializeField] float acceleration = 12f;
        [SerializeField] float gravity = 20f;

        [Header("Height (m)")]
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.2f;
        [SerializeField] float eyeBelowTop = 0.12f;
        [SerializeField] float heightChangeSpeed = 4f;

        CharacterController controller;
        InputActionMap map;
        InputAction move, look, lookStick, sprint, crouch, pause;
        Vector3 velocity;
        float yaw, pitch, height;
        bool inputEnabled = true;

        /// <summary>False while a menu, console screen or cutscene owns the input.</summary>
        public bool InputEnabled
        {
            get => inputEnabled;
            set { inputEnabled = value; ApplyCursor(); }
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            map = actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            lookStick = map.FindAction("LookStick", true);
            sprint = map.FindAction("Sprint", true);
            crouch = map.FindAction("Crouch", true);
            pause = map.FindAction("Pause", true);

            yaw = transform.eulerAngles.y;
            height = standHeight;
            ApplyHeight();
        }

        void OnEnable()
        {
            map.Enable();
            PlayerLocator.Register(transform);
            ApplyCursor();
        }

        void OnDisable()
        {
            map.Disable();
            PlayerLocator.Unregister(transform);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (pause.WasPressedThisFrame())
                InputEnabled = !InputEnabled;
            else if (!inputEnabled && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                InputEnabled = true;

            if (inputEnabled)
                Look();
            Move();
        }

        void Look()
        {
            Vector2 delta = look.ReadValue<Vector2>() * mouseSensitivity
                          + lookStick.ReadValue<Vector2>() * (stickLookSpeed * Time.deltaTime);
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void Move()
        {
            float dt = Time.deltaTime;
            Vector2 input = inputEnabled ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            bool wantsCrouch = inputEnabled && crouch.IsPressed();
            bool crouched = wantsCrouch || (height < standHeight - 0.01f && !HeadroomToStand());

            float speed = crouched ? crouchSpeed
                        : inputEnabled && sprint.IsPressed() && input.y > 0.1f ? sprintSpeed
                        : walkSpeed;

            Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
            Vector3 horizontal = Vector3.Lerp(new Vector3(velocity.x, 0f, velocity.z), target, 1f - Mathf.Exp(-acceleration * dt));
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;
            velocity.y = controller.isGrounded && velocity.y < 0f ? -2f : velocity.y - gravity * dt;

            controller.Move(velocity * dt);

            float targetHeight = crouched ? crouchHeight : standHeight;
            if (!Mathf.Approximately(height, targetHeight))
            {
                height = Mathf.MoveTowards(height, targetHeight, heightChangeSpeed * dt);
                ApplyHeight();
            }
        }

        bool HeadroomToStand()
        {
            float r = controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (height - r);
            return !Physics.SphereCast(origin, r, Vector3.up, out _, standHeight - height + 0.02f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        void ApplyHeight()
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            if (cameraPivot)
                cameraPivot.localPosition = new Vector3(0f, height - eyeBelowTop, 0f);
        }

        void ApplyCursor()
        {
            Cursor.lockState = inputEnabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !inputEnabled;
        }
    }
}
