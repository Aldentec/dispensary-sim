using UnityEngine;
using TMPro;
using DispensarySimulator.Products;
using DispensarySimulator.Store;
using DispensarySimulator.Core;

namespace DispensarySimulator.Player {
    public class PlayerInteraction : MonoBehaviour {
        [Header("Interaction Settings")]
        public float interactionRange = 3f;
        public LayerMask interactionLayers = 1;
        public KeyCode interactionKey = KeyCode.E;
        public KeyCode dropKey = KeyCode.Q;

        [Header("UI")]
        public GameObject interactionPrompt;
        public TextMeshProUGUI promptText;

        [Header("Audio")]
        public AudioSource interactionAudio;
        public AudioClip interactionSound;

        // Components
        private Camera playerCamera;
        private FirstPersonController playerController;
        private PlayerInventory playerInventory;

        // Current interaction target
        private IInteractable currentTarget;
        private GameObject currentTargetObject;

        // Raycast info
        private RaycastHit hitInfo;

        void Start() {
            InitializeComponents();
        }

        void Update() {
            if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

            HandleInventoryInput();
            HandleInteractionDetection();
            HandleInteractionInput();
        }

        private void InitializeComponents() {
            playerCamera = GetComponentInChildren<Camera>();
            playerController = GetComponent<FirstPersonController>();
            playerInventory = GetComponent<PlayerInventory>();

            if (playerCamera == null) {
                Debug.LogError("PlayerInteraction: No camera found!");
            }

            if (playerInventory == null) {
                Debug.LogError("PlayerInteraction: No PlayerInventory found! Adding one...");
                playerInventory = gameObject.AddComponent<PlayerInventory>();
            }

            // Hide interaction prompt initially
            if (interactionPrompt != null) {
                interactionPrompt.SetActive(false);
            }
        }

        private void HandleInventoryInput() {
            // Handle drop input
            if (Input.GetKeyDown(dropKey) && playerInventory != null) {
                if (playerInventory.IsHoldingItem()) {
                    playerInventory.DropItem();
                }
            }
        }

        private void HandleInteractionDetection() {
            // Cast ray from camera
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (Physics.Raycast(ray, out hitInfo, interactionRange, interactionLayers)) {
                // Check if we hit an interactable object
                IInteractable interactable = hitInfo.collider.GetComponent<IInteractable>();

                if (interactable != null && interactable.CanInteract()) {
                    // New target found
                    if (currentTarget != interactable) {
                        ExitInteraction();
                        EnterInteraction(interactable, hitInfo.collider.gameObject);
                    }
                }
                else {
                    // No interactable found, exit current interaction
                    ExitInteraction();
                }
            }
            else {
                // Nothing hit, exit current interaction
                ExitInteraction();
            }
        }

        private void HandleInteractionInput() {
            if (Input.GetKeyDown(interactionKey) && currentTarget != null) {
                PerformInteraction();
            }
        }

        private void EnterInteraction(IInteractable target, GameObject targetObject) {
            currentTarget = target;
            currentTargetObject = targetObject;

            // Show interaction prompt with context-aware text
            ShowInteractionPrompt(GetContextualInteractionText(target));

            // Highlight object if it has the component
            HighlightObject(targetObject, true);

            Debug.Log($"Can interact with: {targetObject.name}");
        }

        private void ExitInteraction() {
            if (currentTarget == null) return;

            // Hide interaction prompt
            HideInteractionPrompt();

            // Remove highlight
            HighlightObject(currentTargetObject, false);

            currentTarget = null;
            currentTargetObject = null;
        }

        private void PerformInteraction() {
            if (currentTarget == null) return;

            // Play interaction sound
            if (interactionAudio != null && interactionSound != null) {
                interactionAudio.PlayOneShot(interactionSound);
            }

            // Perform the interaction
            currentTarget.Interact(this);

            Debug.Log($"Interacted with: {currentTargetObject.name}");
        }

        private string GetContextualInteractionText(IInteractable target) {
            // Get base interaction text
            string baseText = target.GetInteractionText();

            // Add contextual information based on what player is holding
            if (playerInventory != null && playerInventory.IsHoldingItem()) {
                // Player is holding something
                if (target is ShelfSlot) {
                    var heldItem = playerInventory.GetHeldItem();
                    return $"Place {heldItem.name} on shelf";
                }
                else if (target is SpawnedProduct) {
                    return "Drop current item first";
                }
            }
            else {
                // Player is not holding anything
                if (target is SpawnedProduct) {
                    return baseText; // "Pick up [ItemName]"
                }
                else if (target is ShelfSlot) {
                    return "Need item to place on shelf";
                }
            }

            return baseText;
        }

        private void ShowInteractionPrompt(string text) {
            if (interactionPrompt == null) return;

            interactionPrompt.SetActive(true);

            if (promptText != null) {
                // Show interaction key and add inventory hint
                string fullText = $"[{interactionKey}] {text}";

                if (playerInventory != null && playerInventory.IsHoldingItem()) {
                    fullText += $"\n[{dropKey}] Drop item";
                }

                promptText.text = fullText;
            }
        }

        private void HideInteractionPrompt() {
            if (interactionPrompt != null) {
                interactionPrompt.SetActive(false);
            }
        }

        private void HighlightObject(GameObject obj, bool highlight) {
            if (obj == null) return;

            // Try to find outline component (optional - works with third-party outline assets)
            Component outline = obj.GetComponent("Outline");
            if (outline != null && outline is MonoBehaviour) {
                ((MonoBehaviour)outline).enabled = highlight;
                return;
            }

            // Alternative: Change material color slightly
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null) {
                Color targetColor = highlight ? Color.white * 1.2f : Color.white;
                renderer.material.color = targetColor;
            }
        }

        // Public methods for external access
        public bool IsInteracting() {
            return currentTarget != null;
        }

        public GameObject GetCurrentTarget() {
            return currentTargetObject;
        }

        public PlayerInventory GetPlayerInventory() {
            return playerInventory;
        }

        // Debug visualization
        void OnDrawGizmosSelected() {
            if (playerCamera != null) {
                Gizmos.color = currentTarget != null ? Color.green : Color.red;
                Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * interactionRange);
            }
        }

        // GUI debug info
        void OnGUI() {
            if (currentTarget != null) {
                GUI.Label(new Rect(10, 80, 300, 20), $"Looking at: {currentTargetObject.name}");
            }
        }
    }

    // Interface for interactable objects
    public interface IInteractable {
        bool CanInteract();
        void Interact(PlayerInteraction player);
        string GetInteractionText();
    }
}