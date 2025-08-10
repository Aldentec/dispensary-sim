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

        [Header("Debug")]
        public bool enableDebugRaycast = true;
        public KeyCode debugKey = KeyCode.F11;

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

            // Debug input
            if (Input.GetKeyDown(debugKey)) {
                DebugInteractionSystem();
            }
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

            Debug.Log($"🔧 PlayerInteraction initialized - Range: {interactionRange}, Layers: {interactionLayers.value}");
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

            // ENHANCED DEBUG: Log raycast details
            if (enableDebugRaycast) {
                Debug.DrawRay(ray.origin, ray.direction * interactionRange, Color.cyan, 0.1f);
            }

            if (Physics.Raycast(ray, out hitInfo, interactionRange, interactionLayers)) {
                if (enableDebugRaycast) {
                    Debug.Log($"🎯 RAYCAST HIT: {hitInfo.collider.gameObject.name} at distance {hitInfo.distance:F2}");
                    Debug.Log($"🎯 Hit object layer: {hitInfo.collider.gameObject.layer}, matches mask: {((1 << hitInfo.collider.gameObject.layer) & interactionLayers.value) != 0}");
                }

                // Check if we hit an interactable object
                IInteractable interactable = hitInfo.collider.GetComponent<IInteractable>();

                if (interactable != null) {
                    Debug.Log($"🎯 Found IInteractable: {interactable.GetType().Name}, CanInteract: {interactable.CanInteract()}");

                    if (interactable.CanInteract()) {
                        // New target found
                        if (currentTarget != interactable) {
                            ExitInteraction();
                            EnterInteraction(interactable, hitInfo.collider.gameObject);
                        }
                    }
                    else {
                        Debug.Log($"🚫 IInteractable found but CanInteract() returned false");
                        ExitInteraction();
                    }
                }
                else {
                    if (enableDebugRaycast) {
                        Debug.Log($"❌ No IInteractable component on {hitInfo.collider.gameObject.name}");
                    }
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

            Debug.Log($"✅ Can interact with: {targetObject.name}");
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
                // Show interaction key and add inventory hint with CORRECT drop key
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

        private void DebugInteractionSystem() {
            Debug.Log("=== INTERACTION SYSTEM DEBUG ===");

            if (playerCamera == null) {
                Debug.LogError("❌ No player camera!");
                return;
            }

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            Debug.Log($"🔍 Raycast from {ray.origin} direction {ray.direction}");
            Debug.Log($"🔍 Range: {interactionRange}, Layer mask: {interactionLayers.value}");

            // Test all hits
            RaycastHit[] hits = Physics.RaycastAll(ray, interactionRange);
            Debug.Log($"🎯 Total hits (no layer filter): {hits.Length}");

            foreach (var hit in hits) {
                Debug.Log($"🎯 Hit: {hit.collider.gameObject.name}, Layer: {hit.collider.gameObject.layer}, Distance: {hit.distance:F2}");

                var interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null) {
                    Debug.Log($"   ✅ Has IInteractable, CanInteract: {interactable.CanInteract()}");
                }
            }

            // Test with layer mask
            if (Physics.Raycast(ray, out RaycastHit layerHit, interactionRange, interactionLayers)) {
                Debug.Log($"✅ Layer-filtered hit: {layerHit.collider.gameObject.name}");
            }
            else {
                Debug.Log("❌ No layer-filtered hits");
            }

            // Check all SpawnedProducts in scene
            var products = FindObjectsOfType<SpawnedProduct>();
            Debug.Log($"📦 SpawnedProducts in scene: {products.Length}");

            foreach (var product in products) {
                var distance = Vector3.Distance(ray.origin, product.transform.position);
                Debug.Log($"📦 {product.name}: Distance {distance:F2}, Layer {product.gameObject.layer}, CanInteract: {product.CanInteract()}");

                var collider = product.GetComponent<Collider>();
                if (collider != null) {
                    Debug.Log($"   Collider: enabled={collider.enabled}, trigger={collider.isTrigger}");
                }
            }

            Debug.Log("=== END DEBUG ===");
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

            // Show debug info
            GUI.Label(new Rect(10, 60, 300, 20), $"Press {debugKey} for interaction debug");
        }
    }

    // Interface for interactable objects
    public interface IInteractable {
        bool CanInteract();
        void Interact(PlayerInteraction player);
        string GetInteractionText();
    }
}