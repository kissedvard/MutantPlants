using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// First-person movement (WASD), mouse look, sprint, jump, footsteps.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Camera playerCamera;
        public float walkSpeed = 6f;
        public float sprintSpeed = 9f;
        public float acceleration = 60f;
        public float airControl = 0.35f;
        public float jumpHeight = 1.2f;
        public float gravity = -22f;
        [Tooltip("Base look speed; multiplied by the sensitivity from the settings menu.")]
        public float lookSpeed = 1.5f;

        CharacterController controller;
        CameraFX cameraFX;
        float pitch;
        float verticalVelocity;
        Vector3 planarVelocity;
        bool wasGrounded = true;
        float lastStepPhase;

        /// <summary>Mouse movement this frame, used by the weapon sway.</summary>
        public Vector2 LookDelta { get; private set; }
        public float Speed => planarVelocity.magnitude;
        public bool IsGrounded => controller.isGrounded;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            cameraFX = playerCamera.GetComponent<CameraFX>();
        }

        void Start()
        {
            playerCamera.fieldOfView = GameSettings.FieldOfView;
        }

        void Update()
        {
            if (Time.timeScale == 0f) return;
            Look();
            Move();
        }

        void Look()
        {
            float sens = lookSpeed * GameSettings.MouseSensitivity;
            float mx = Input.GetAxis("Mouse X") * sens;
            float my = Input.GetAxis("Mouse Y") * sens * (GameSettings.InvertY ? -1f : 1f);
            LookDelta = new Vector2(mx, my);

            transform.Rotate(0f, mx, 0f);
            pitch = Mathf.Clamp(pitch - my, -85f, 85f);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f) * CameraFX.ShakeRotation;
        }

        void Move()
        {
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            input = Vector3.ClampMagnitude(input, 1f);
            bool sprinting = Input.GetKey(KeyCode.LeftShift) && input.z > 0.1f;
            float speed = sprinting ? sprintSpeed : walkSpeed;
            Vector3 target = transform.TransformDirection(input) * speed;

            bool grounded = controller.isGrounded;
            float accel = acceleration * (grounded ? 1f : airControl);
            planarVelocity = Vector3.MoveTowards(planarVelocity, target, accel * Time.deltaTime);

            if (grounded)
            {
                if (!wasGrounded && verticalVelocity < -6f)
                {
                    CameraFX.Land(Mathf.Clamp01(-verticalVelocity / 15f));
                    SoundFX.PlayVaried(SoundFX.Sfx.Land, 0.7f);
                }
                verticalVelocity = -2f;
                if (Input.GetButtonDown("Jump"))
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            wasGrounded = grounded;
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 move = planarVelocity;
            move.y = verticalVelocity;
            controller.Move(move * Time.deltaTime);

            if (cameraFX != null)
            {
                cameraFX.SetMovement(planarVelocity.magnitude, grounded, sprinting);
                // Footstep at every half bob cycle.
                float stepPhase = Mathf.Floor(cameraFX.BobPhase / Mathf.PI);
                if (grounded && planarVelocity.magnitude > 1f && stepPhase != lastStepPhase)
                    SoundFX.PlayVaried(SoundFX.Sfx.Footstep, 0.6f, 0.15f);
                lastStepPhase = stepPhase;
            }
        }
    }
}
