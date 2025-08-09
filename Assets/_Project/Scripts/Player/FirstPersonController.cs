using UnityEngine;
using DispensarySimulator.Core;
using DispensarySimulator.Store;

namespace DispensarySimulator.Player {
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour {
        [Header("Movement Settings")]
        public float walkSpeed = 5f;
        public float runSpeed = 8f;
        public float crouchSpeed = 2f;
        public float jumpHeight = 1.2f;
        public float gravity = -9.81f;

        [Header("Crouching")]
        public float standingHeight = 2f;
        public float crouchingHeight = 1f;
        public float crouchTransitionSpeed = 8f;
        public KeyCode crouchKey = KeyCode.LeftControl;

        [Header("Mouse Look Settings")]
        public float mouseSensitivity = 2f;
        public bool invertY = false;
        public float maxLookAngle = 80f;

        [Header("Ground Detection")]
        public Transform groundCheck;
        public float groundDistance = 0.4f;
        public LayerMask groundMask = 1;

        [Header("Carrying Integration")]
        public float baseWalkSpeed = 5f;  // Store original walk speed
        public float baseRunSpeed = 8f;


        [Header("Audio")]
        public AudioSource footstepAudio;
        public AudioClip[] footstepClips;
        public float footstepInterval = 0.5f;

        [Header("UI Control")]
        public KeyCode menuKey = KeyCode.Escape;
        public KeyCode inventoryKey = KeyCode.Tab;
        private bool isInMenu = true;  // Start in menu mode for multiplayer setup
        private bool isInInventory = false;  // Track inventory UI state

        // References to other systems
        private DispensarySimulator.Store.InventoryManager inventoryManager;

        // Components
        private CharacterController controller;
        private Camera playerCamera;

        // Movement variables
        private Vector3 velocity;
        private bool isGrounded;
        private bool isRunning;
        private bool isCrouching = false;
        private float currentHeight;
        private float targetHeight;


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
        private bool crouchInput;

        private PlayerInventory playerInventory;

        void Start() {
            InitializeComponents();
            SetMenuMode(true); // Start in menu mode for multiplayer setup

            // Debug check for InventoryManager
            if (inventoryManager != null) {
                Debug.Log("✅ InventoryManager found successfully");
            }
            else {
                Debug.LogError("❌ InventoryManager NOT found!");

                // Try to find it manually for debugging
                var allInventoryManagers = FindObjectsOfType<DispensarySimulator.Store.InventoryManager>();
                Debug.Log($"🔍 Found {allInventoryManagers.Length} InventoryManager(s) in scene");

                for (int i = 0; i < allInventoryManagers.Length; i++) {
                    Debug.Log($"🔍 InventoryManager {i}: {allInventoryManagers[i].gameObject.name} (Active: {allInventoryManagers[i].gameObject.activeInHierarchy})");
                }
            }

            playerInventory = GetComponent<PlayerInventory>();

            // Store base speeds
            baseWalkSpeed = walkSpeed;
            baseRunSpeed = runSpeed;
        }

        public void UpdateMovementSpeed() {
            if (playerInventory != null && playerInventory.IsHoldingItem()) {
                // Reduce speed when carrying items
                float multiplier = playerInventory.carryingMoveSpeedMultiplier;
                walkSpeed = baseWalkSpeed * multiplier;
                runSpeed = baseRunSpeed * multiplier;
                Debug.Log($"🏃 Speed reduced to {multiplier * 100:F0}% while carrying");
            }
            else {
                // Restore normal speed
                walkSpeed = baseWalkSpeed;
                runSpeed = baseRunSpeed;
                Debug.Log("🏃 Speed restored to normal");
            }
        }

        void Update() {
            if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

            HandleMenuToggle();
            HandleInventoryToggle();
            HandleInput();
            HandleCrouching();

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

            // Initialize height values
            standingHeight = controller.height;
            currentHeight = standingHeight;
            targetHeight = standingHeight;

            // Find the inventory manager
            inventoryManager = FindObjectOfType<DispensarySimulator.Store.InventoryManager>();
            if (inventoryManager == null) {
                Debug.LogWarning("No InventoryManager found in scene!");
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
            if (Input.GetKeyDown(menuKey)) {
                var gm = GameManager.Instance;
                if (gm != null) {
                    // Use GameManager for pause/resume functionality
                    if (!gm.isPaused) {
                        gm.PauseGame();
                    }
                    else {
                        gm.ResumeGame();
                    }
                    return;
                }

                // Fallback if GameManager is missing - just toggle our own menu
                ToggleMenu();
            }
        }

        private void HandleInventoryToggle() {
            // Toggle inventory with Tab key
            if (Input.GetKeyDown(inventoryKey)) {
                Debug.Log($"🔄 Tab pressed. Current inventory state: {isInInventory}");
                ToggleInventory();
            }
        }

        private void HandleCrouching() {
            // Set target height based on crouch input
            targetHeight = crouchInput ? crouchingHeight : standingHeight;
            isCrouching = crouchInput;

            // Smooth height transition
            if (Mathf.Abs(currentHeight - targetHeight) > 0.01f) {
                currentHeight = Mathf.Lerp(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);

                // Update CharacterController height
                Vector3 center = controller.center;
                controller.height = currentHeight;
                center.y = currentHeight / 2f;
                controller.center = center;

                // Update camera position
                if (playerCamera != null) {
                    Vector3 cameraPos = playerCamera.transform.localPosition;
                    cameraPos.y = (currentHeight / 2f) + (currentHeight * 0.3f); // Position camera in upper portion
                    playerCamera.transform.localPosition = cameraPos;
                }

                // Update ground check position
                if (groundCheck != null) {
                    Vector3 groundPos = groundCheck.localPosition;
                    groundPos.y = -currentHeight / 2f;
                    groundCheck.localPosition = groundPos;
                }
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
            if (jumpInput) Debug.Log("🦘 Jump input detected!");

            // Run input
            runInput = Input.GetKey(KeyCode.LeftShift);

            // Crouch input (hold Ctrl)
            crouchInput = Input.GetKey(crouchKey);
            if (crouchInput != isCrouching) Debug.Log($"🐒 Crouch state: {crouchInput}");
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
            // More reliable ground check
            Vector3 rayStart = transform.position;
            rayStart.y += 0.1f; // Start slightly above player center

            RaycastHit hit;
            isGrounded = Physics.Raycast(rayStart, Vector3.down, out hit, (controller.height / 2f) + 0.2f);

            // Debug the ground check
            if (jumpInput) {
                Debug.Log($"🔍 Ground raycast from {rayStart} distance {(controller.height / 2f) + 0.2f}");
                Debug.Log($"🔍 Hit: {(hit.collider != null ? hit.collider.name : "nothing")}");
                Debug.Log($"🔍 isGrounded: {isGrounded}");
            }

            if (isGrounded && velocity.y < 0) {
                velocity.y = -2f; // Small negative value to stay grounded
            }

            // Calculate move direction
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

            // Determine speed based on current state
            float currentSpeed;
            if (isCrouching) {
                currentSpeed = crouchSpeed;
                isRunning = false; // Can't run while crouching
            }
            else {
                isRunning = runInput && moveInput.magnitude > 0.1f;
                currentSpeed = isRunning ? runSpeed : walkSpeed;
            }

            // Apply movement
            controller.Move(move * currentSpeed * Time.deltaTime);

            // Jumping (can't jump while crouching)
            if (jumpInput) {
                Debug.Log($"🦘 Jump conditions - isGrounded: {isGrounded}, isCrouching: {isCrouching}, velocity.y: {velocity.y:F2}");

                if (isGrounded && !isCrouching) {
                    float jumpForce = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    velocity.y = jumpForce;
                    Debug.Log($"🦘 JUMPING! Jump force: {jumpForce:F2}");
                }
                else {
                    if (!isGrounded) Debug.Log("❌ Can't jump - not grounded");
                    if (isCrouching) Debug.Log("❌ Can't jump - crouching");
                }
            }

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }

        private void HandleFootsteps() {
            if (!isGrounded || moveInput.magnitude < 0.1f || footstepAudio == null) return;

            footstepTimer += Time.deltaTime;

            // Adjust footstep interval based on movement state
            float currentFootstepInterval = footstepInterval;

            if (isCrouching) {
                currentFootstepInterval = footstepInterval * 1.5f; // Slower when crouching
            }
            else if (isRunning) {
                currentFootstepInterval = footstepInterval * 0.7f; // Faster when running
            }

            if (footstepTimer >= currentFootstepInterval) {
                PlayFootstepSound();
                footstepTimer = 0f;
            }
        }

        private void PlayFootstepSound() {
            if (footstepClips == null || footstepClips.Length == 0) return;

            AudioClip clipToPlay = footstepClips[Random.Range(0, footstepClips.Length)];

            // Adjust volume based on movement state
            float volume = 1f;
            if (isCrouching) {
                volume = 0.5f; // Quieter when crouching
            }
            else if (isRunning) {
                volume = 1.2f; // Louder when running
            }

            footstepAudio.PlayOneShot(clipToPlay, volume);
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
            Debug.Log($"🎒 SetInventoryMode called: {isInInventory} → {inInventory}");

            isInInventory = inInventory;

            // If entering inventory mode, close menu
            if (inInventory && isInMenu) {
                Debug.Log("🎒 Closing menu because inventory opened");
                isInMenu = false;
            }

            // Find InventoryManager each time (failsafe approach)
            var invManager = FindObjectOfType<DispensarySimulator.Store.InventoryManager>();
            if (invManager != null) {
                Debug.Log($"🎒 Found InventoryManager! IsOpen: {invManager.IsInventoryOpen()}");

                if (inInventory && !invManager.IsInventoryOpen()) {
                    Debug.Log("🎒 Calling OpenInventory()");
                    invManager.OpenInventory();
                }
                else if (!inInventory && invManager.IsInventoryOpen()) {
                    Debug.Log("🎒 Calling CloseInventory()");
                    invManager.CloseInventory();
                }
            }
            else {
                Debug.LogError("❌ InventoryManager not found in scene!");

                // Additional debugging
                var allComponents = FindObjectsOfType<MonoBehaviour>();
                var inventoryComponents = new System.Collections.Generic.List<string>();
                foreach (var comp in allComponents) {
                    if (comp.GetType().Name.Contains("Inventory")) {
                        inventoryComponents.Add($"{comp.GetType().Name} on {comp.gameObject.name}");
                    }
                }
                Debug.Log($"🔍 Found inventory-related components: {string.Join(", ", inventoryComponents)}");
            }

            Debug.Log($"🎒 Final state - Menu: {isInMenu}, Inventory: {isInInventory}");
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

        public bool IsCrouching() {
            return isCrouching;
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