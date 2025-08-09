using UnityEngine;
using DispensarySimulator.Core;
using DispensarySimulator.Store;

namespace DispensarySimulator.Player {
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour {
        [Header("Movement Settings")]
        public float walkSpeed = 5f;
        public float runSpeed = 8f;
        public float jumpHeight = 1.2f;
        public float gravity = -9.81f;

        [Header("Mouse Look Settings")]
        public float mouseSensitivity = 2f;
        public bool invertY = false;
        public float maxLookAngle = 80f;

        [Header("Ground Detection")]
        public Transform groundCheck;
        public float groundDistance = 0.4f;
        public LayerMask groundMask = 1;

        [Header("Audio")]
        public AudioSource footstepAudio;
        public AudioClip[] footstepClips;
        public float footstepInterval = 0.5f;

        [Header("UI Control")]
        public KeyCode menuKey = KeyCode.Escape;
        public KeyCode inventoryKey = KeyCode.Tab;
        private bool isInMenu = true;  // Start in menu mode for multiplayer setup
        private bool isInInventory = false;  // Track inventory UI state

        // Components
        private CharacterController controller;
        private Camera playerCamera;

        // Movement variables
        private Vector3 velocity;
        private bool isGrounded;
        private bool isRunning;

        // Mouse look variables
        private float xRotation = 0f;
        private float yRotation = 0f;

        // Footstep timing
        private float footstepTimer = 0f;

        // Input
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool jumpInput;
        private bool runInput;

        void Start() {
            InitializeComponents();
            SetMenuMode(true); // Start in menu mode for multiplayer setup
        }

        void Update() {
            if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

            HandleMenuToggle();
            HandleInventoryToggle();
            HandleInput();

            // Only handle mouse look if not in menu or inventory
            if (!isInMenu && !isInInventory) {
                HandleMouseLook();
            }

            HandleMovement();
            HandleFootsteps();
        }

        private void InitializeComponents() {
            controller = GetComponent<CharacterController>();
            playerCamera = GetComponentInChildren<Camera>();

            if (playerCamera == null) {
                Debug.LogError("No camera found as child of player!");
            }

            // Create ground check if it doesn't exist
            if (groundCheck == null) {
                GameObject groundCheckObj = new GameObject("GroundCheck");
                groundCheckObj.transform.parent = transform;
                groundCheckObj.transform.localPosition = Vector3.down * (controller.height / 2f);
                groundCheck = groundCheckObj.transform;
            }
        }

        private void HandleMenuToggle() {
            // Toggle menu with Escape key
            if (Input.GetKeyDown(menuKey)) {
                ToggleMenu();
            }
        }

        private void HandleInventoryToggle() {
            // Toggle inventory with Tab key
            if (Input.GetKeyDown(inventoryKey)) {
                ToggleInventory();
            }
        }

        private void HandleInput() {
            // Movement input (works in both modes)
            moveInput.x = Input.GetAxis("Horizontal");
            moveInput.y = Input.GetAxis("Vertical");

            // Mouse look input (only processed if not in menu or inventory)
            if (!isInMenu && !isInInventory) {
                lookInput.x = Input.GetAxis("Mouse X") * mouseSensitivity;
                lookInput.y = Input.GetAxis("Mouse Y") * mouseSensitivity;
            }

            // Jump input
            jumpInput = Input.GetButtonDown("Jump");

            // Run input
            runInput = Input.GetKey(KeyCode.LeftShift);
        }

        private void HandleMouseLook() {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            // Horizontal rotation (Y-axis)
            yRotation += lookInput.x;
            transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

            // Vertical rotation (X-axis)
            xRotation -= lookInput.y * (invertY ? -1f : 1f);
            xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);
            playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        private void HandleMovement() {
            // Ground check
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

            if (isGrounded && velocity.y < 0) {
                velocity.y = -2f; // Small negative value to stay grounded
            }

            // Calculate move direction
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

            // Determine speed
            isRunning = runInput && moveInput.magnitude > 0.1f;
            float currentSpeed = isRunning ? runSpeed : walkSpeed;

            // Apply movement
            controller.Move(move * currentSpeed * Time.deltaTime);

            // Jumping
            if (jumpInput && isGrounded) {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }

        private void HandleFootsteps() {
            if (!isGrounded || moveInput.magnitude < 0.1f || footstepAudio == null) return;

            footstepTimer += Time.deltaTime;

            float currentFootstepInterval = isRunning ? footstepInterval * 0.7f : footstepInterval;

            if (footstepTimer >= currentFootstepInterval) {
                PlayFootstepSound();
                footstepTimer = 0f;
            }
        }

        private void PlayFootstepSound() {
            if (footstepClips == null || footstepClips.Length == 0) return;

            AudioClip clipToPlay = footstepClips[Random.Range(0, footstepClips.Length)];
            footstepAudio.PlayOneShot(clipToPlay);
        }

        // Menu Management Methods
        public void ToggleMenu() {
            SetMenuMode(!isInMenu);
        }

        public void SetMenuMode(bool inMenu) {
            isInMenu = inMenu;

            // If entering menu mode, close inventory
            if (inMenu && isInInventory) {
                isInInventory = false;
            }

            UpdateCursorState();
        }

        // Inventory Management Methods
        public void ToggleInventory() {
            SetInventoryMode(!isInInventory);
        }

        public void SetInventoryMode(bool inInventory) {
            isInInventory = inInventory;

            // If entering inventory mode, close menu
            if (inInventory && isInMenu) {
                isInMenu = false;
            }

            UpdateCursorState();
        }

        private void UpdateCursorState() {
            bool shouldUnlockCursor = isInMenu || isInInventory;

            if (shouldUnlockCursor) {
                // Unlock cursor for UI interaction
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (isInMenu) {
                    Debug.Log("Menu mode: ON - Cursor unlocked");
                }
                else if (isInInventory) {
                    Debug.Log("Inventory mode: ON - Cursor unlocked");
                }
            }
            else {
                // Lock cursor for first-person control
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Debug.Log("Game mode: ON - Cursor locked");
            }
        }

        public bool IsInMenu() {
            return isInMenu;
        }

        public bool IsInInventory() {
            return isInInventory;
        }

        public bool IsInUIMode() {
            return isInMenu || isInInventory;
        }

        // Legacy method (kept for compatibility)
        public void ToggleCursor() {
            ToggleMenu();
        }

        // Public methods for external access
        public bool IsMoving() {
            return moveInput.magnitude > 0.1f && isGrounded;
        }

        public bool IsRunning() {
            return isRunning;
        }

        public bool IsGrounded() {
            return isGrounded;
        }

        // Gizmos for debugging
        void OnDrawGizmosSelected() {
            if (groundCheck != null) {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
            }
        }
    }
}