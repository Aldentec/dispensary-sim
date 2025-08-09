using UnityEngine;
using Unity.Netcode;
using DispensarySimulator.Store;

namespace DispensarySimulator.Player {
    public class PlayerInventory : NetworkBehaviour {
        [Header("Inventory Settings")]
        public Transform holdPosition;
        public Vector3 holdOffset = new Vector3(0.3f, -0.2f, 1.2f); // Better positioning
        public float holdDistance = 1.5f;

        [Header("Carrying Physics")]
        public float carryingMoveSpeedMultiplier = 0.9f; // Less restrictive
        public bool dropOnDeath = true;

        // Network state
        private NetworkVariable<ulong> heldItemNetworkId = new NetworkVariable<ulong>(0);

        // Local state
        private GameObject heldItem;
        private SpawnedProduct heldSpawnedProduct;
        private Vector3 originalHoldPosition;

        // Components
        private Camera playerCamera;
        private FirstPersonController playerController;

        public override void OnNetworkSpawn() {
            // Subscribe to network changes
            heldItemNetworkId.OnValueChanged += OnHeldItemChanged;

            InitializeComponents();
        }

        void Start() {
            InitializeComponents();
        }

        private void InitializeComponents() {
            playerCamera = GetComponentInChildren<Camera>();
            playerController = GetComponent<FirstPersonController>();

            // Set up hold position if not assigned
            if (holdPosition == null && playerCamera != null) {
                GameObject holdPositionObj = new GameObject("HoldPosition");
                holdPositionObj.transform.SetParent(playerCamera.transform);
                holdPositionObj.transform.localPosition = holdOffset;
                holdPositionObj.transform.localRotation = Quaternion.identity;
                holdPosition = holdPositionObj.transform;
            }

            originalHoldPosition = holdPosition.localPosition;
        }

        void Update() {
            if (heldItem != null) {
                UpdateHeldItemPosition();
            }
        }

        private void UpdateHeldItemPosition() {
            if (heldItem == null || holdPosition == null) return;

            // Smoothly position the item, but ensure it doesn't interfere with player physics
            Vector3 targetPosition = holdPosition.position;
            Quaternion targetRotation = holdPosition.rotation;

            // Use smooth interpolation for natural movement
            heldItem.transform.position = Vector3.Lerp(heldItem.transform.position, targetPosition, Time.deltaTime * 15f);
            heldItem.transform.rotation = Quaternion.Slerp(heldItem.transform.rotation, targetRotation, Time.deltaTime * 12f);
        }

        public void PickupItem(SpawnedProduct spawnedProduct) {
            if (heldItem != null) {
                Debug.LogWarning("Already holding an item!");
                return;
            }

            if (spawnedProduct == null) {
                Debug.LogError("Cannot pickup null item!");
                return;
            }

            // Request pickup on server
            RequestPickupServerRpc(spawnedProduct.NetworkObjectId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPickupServerRpc(ulong itemNetworkId) {
            if (!IsServer) return;

            // Check if already holding something
            if (heldItemNetworkId.Value != 0) {
                Debug.LogWarning("Player already holding an item!");
                return;
            }

            // Find the item
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out NetworkObject itemNetObj);
            if (itemNetObj == null) {
                Debug.LogError("Could not find item to pickup!");
                return;
            }

            var spawnedProduct = itemNetObj.GetComponent<SpawnedProduct>();
            if (spawnedProduct == null) {
                Debug.LogError("Item is not a SpawnedProduct!");
                return;
            }

            // Update network state
            heldItemNetworkId.Value = itemNetworkId;

            // Call the product's pickup method
            spawnedProduct.RequestPickupServerRpc(OwnerClientId);

            Debug.Log($"🤏 Player {OwnerClientId} picked up {spawnedProduct.name}");
        }

        public void DropItem() {
            if (heldItem == null) {
                Debug.LogWarning("No item to drop!");
                return;
            }

            // Request drop on server
            RequestDropServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDropServerRpc() {
            if (!IsServer) return;

            if (heldItemNetworkId.Value == 0) {
                Debug.LogWarning("Player not holding any item!");
                return;
            }

            // Find the held item
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(heldItemNetworkId.Value, out NetworkObject itemNetObj);
            if (itemNetObj == null) {
                Debug.LogError("Could not find held item to drop!");
                return;
            }

            var spawnedProduct = itemNetObj.GetComponent<SpawnedProduct>();
            if (spawnedProduct != null) {
                // Calculate drop position in front of player
                Vector3 dropPosition = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
                Quaternion dropRotation = Quaternion.identity;

                // Call the product's placement method to drop it
                spawnedProduct.PlaceOnShelfServerRpc(dropPosition, dropRotation);
            }

            // Clear held item
            heldItemNetworkId.Value = 0;

            Debug.Log($"📦 Player {OwnerClientId} dropped item");
        }

        private void OnHeldItemChanged(ulong oldValue, ulong newValue) {
            // Clear previous item
            if (heldItem != null) {
                heldSpawnedProduct = null;
                heldItem = null;
            }

            // Set new item
            if (newValue != 0) {
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(newValue, out NetworkObject itemNetObj);
                if (itemNetObj != null) {
                    heldItem = itemNetObj.gameObject;
                    heldSpawnedProduct = itemNetObj.GetComponent<SpawnedProduct>();

                    Debug.Log($"🎒 Now holding: {heldItem.name}");
                }
            }
            else {
                Debug.Log("🎒 No longer holding any item");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ClearNetworkVariableServerRpc() {
            if (!IsServer) return;

            Debug.Log($"🎒 ClearNetworkVariableServerRpc - clearing held item ID: {heldItemNetworkId.Value}");
            heldItemNetworkId.Value = 0;
            Debug.Log("🎒 NetworkVariable cleared by server");
        }

        [ServerRpc(RequireOwnership = false)]
        public void ClearHeldItemServerRpc() {
            if (!IsServer) return;

            heldItemNetworkId.Value = 0;
            Debug.Log("🎒 Server cleared held item");
        }

        // Simple client-side clearing for immediate feedback
        public void ForceClientClearInventory() {
            Debug.Log("🎒 ForceClientClearInventory called");

            if (heldItem != null) {
                Debug.Log($"🎒 Manually clearing held item: {heldItem.name}");

                // Restore physics to the held item
                SetupHeldItemPhysics(heldItem, false);

                // Clear local references
                heldSpawnedProduct = null;
                heldItem = null;

                Debug.Log("🎒 Client inventory forcibly cleared");

                // Update movement speed
                UpdateMovementSpeed();
            }
            else {
                Debug.Log("🎒 No item to clear");
            }
        }

        // Called directly by server to force clear inventory
        public void ForceServerClearInventory() {
            if (!IsServer) {
                Debug.LogError("🎒 ForceServerClearInventory called on non-server!");
                return;
            }

            Debug.Log($"🎒 ForceServerClearInventory called - current held item ID: {heldItemNetworkId.Value}");

            // Store old value for callback
            ulong oldValue = heldItemNetworkId.Value;

            // Clear the network variable
            heldItemNetworkId.Value = 0;
            Debug.Log($"🎒 Changed held item from {oldValue} to 0");

            // Force trigger the callback manually to ensure visual update
            if (oldValue != 0) {
                Invoke(nameof(ForceInventoryUpdate), 0.1f); // Small delay to ensure network state is updated
            }
        }

        private void ForceInventoryUpdate() {
            if (heldItem != null && heldItemNetworkId.Value == 0) {
                Debug.Log("🎒 Manually triggering inventory clear callback");
                OnHeldItemChanged(999, 0); // Use fake old value
            }
        }

        private void UpdateMovementSpeed() {
            // Notify movement controller about carrying state (only when state changes)
            if (playerController != null) {
                // Check if the controller has the UpdateMovementSpeed method
                var method = playerController.GetType().GetMethod("UpdateMovementSpeed");
                if (method != null) {
                    method.Invoke(playerController, null);
                }
            }
        }

        private void SetupHeldItemPhysics(GameObject item, bool isHeld) {
            if (item == null) return;

            var rb = item.GetComponent<Rigidbody>();
            var col = item.GetComponent<Collider>();

            if (isHeld) {
                Debug.Log($"🔧 Setting up held physics for {item.name}");
                // Completely disable physics while held
                if (rb != null) {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                if (col != null) {
                    col.enabled = false; // Critical: prevent ALL collisions while held
                }

                // Make sure item doesn't interfere with player
                item.layer = LayerMask.NameToLayer("Ignore Raycast"); // Move to non-interfering layer
            }
            else {
                Debug.Log($"🔧 Restoring normal physics for {item.name}");
                // Re-enable physics when dropped/placed
                if (rb != null) {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
                if (col != null) {
                    col.enabled = true;
                }

                // Restore to normal layer
                item.layer = 0; // Default layer
            }
        }

        // Public methods
        public bool IsHoldingItem() {
            return heldItemNetworkId.Value != 0;
        }

        public GameObject GetHeldItem() {
            return heldItem;
        }

        public SpawnedProduct GetHeldSpawnedProduct() {
            return heldSpawnedProduct;
        }

        // Drop item on certain events
        void OnTriggerEnter(Collider other) {
            // Could drop item if player enters certain areas
        }

        // Input handling (you can call this from PlayerInteraction or FirstPersonController)
        public void HandleDropInput() {
            if (Input.GetKeyDown(KeyCode.Q)) {
                DropItem();
            }
        }

        // Cleanup
        public override void OnNetworkDespawn() {
            if (heldItemNetworkId != null) {
                heldItemNetworkId.OnValueChanged -= OnHeldItemChanged;
            }
        }

        void OnDestroy() {
            // Drop item if player is destroyed
            if (dropOnDeath && IsHoldingItem()) {
                DropItem();
            }
        }

        // Debug info
        void OnGUI() {
            if (IsHoldingItem() && heldItem != null) {
                GUI.Label(new Rect(10, 100, 300, 20), $"Holding: {heldItem.name}");
                GUI.Label(new Rect(10, 120, 300, 20), "Press Q to drop");
                GUI.Label(new Rect(10, 140, 300, 20), $"NetworkID: {heldItemNetworkId.Value}");
            }
            else {
                GUI.Label(new Rect(10, 100, 300, 20), "Not holding anything");
                GUI.Label(new Rect(10, 120, 300, 20), $"NetworkID: {heldItemNetworkId.Value}");
            }
        }

        // Test method - call this manually if needed
        [ContextMenu("Test Clear Inventory")]
        public void TestClearInventory() {
            Debug.Log("🎒 TEST: Manually clearing inventory");
            ForceClientClearInventory();
        }
    }
}