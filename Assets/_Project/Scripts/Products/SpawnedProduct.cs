using UnityEngine;
using Unity.Netcode;
using DispensarySimulator.Products;
using DispensarySimulator.Player;

namespace DispensarySimulator.Store {
    public class SpawnedProduct : NetworkBehaviour, IInteractable {
        [Header("Product Info")]
        public ProductData productData;

        [Header("Pickup Settings")]
        public bool canBePickedUp = true;
        public float pickupRange = 2f;

        [Header("Visual Feedback")]
        public Material highlightMaterial;
        public GameObject pickupEffect;

        // Network state
        private NetworkVariable<bool> isPickedUp = new NetworkVariable<bool>(false);
        private NetworkVariable<ulong> pickedUpByPlayer = new NetworkVariable<ulong>(999);

        // Components
        private Renderer productRenderer;
        private Collider productCollider;
        private Rigidbody productRigidbody;
        private Material originalMaterial;
        private ProductSpawnPoint spawnPoint;

        // State
        private bool isHighlighted = false;

        public override void OnNetworkSpawn() {
            InitializeComponents();

            // Subscribe to network variable changes
            isPickedUp.OnValueChanged += OnPickupStateChanged;
            pickedUpByPlayer.OnValueChanged += OnPickupPlayerChanged;
        }

        public void Initialize(ProductData data, ProductSpawnPoint spawn) {
            productData = data;
            spawnPoint = spawn;
            gameObject.name = $"Spawned_{data.productName}";
        }

        private void InitializeComponents() {
            productRenderer = GetComponent<Renderer>();
            productCollider = GetComponent<Collider>();
            productRigidbody = GetComponent<Rigidbody>();

            if (productRenderer != null) {
                originalMaterial = productRenderer.material;
            }

            // Ensure we have physics components
            if (productCollider == null) {
                Debug.LogWarning("SpawnedProduct: Adding missing BoxCollider");
                productCollider = gameObject.AddComponent<BoxCollider>();
                // Make sure it's NOT a trigger for physics collision
                productCollider.isTrigger = false;
            }
            else {
                // Ensure existing collider is NOT a trigger
                if (productCollider.isTrigger) {
                    Debug.LogWarning($"SpawnedProduct: Converting trigger collider to solid collider for {gameObject.name}");
                    productCollider.isTrigger = false;
                }
            }

            if (productRigidbody == null) {
                Debug.LogWarning("SpawnedProduct: Adding missing Rigidbody");
                productRigidbody = gameObject.AddComponent<Rigidbody>();
                // Configure rigidbody for realistic physics
                productRigidbody.mass = 1f;
                productRigidbody.drag = 1f;
                productRigidbody.angularDrag = 5f;
                productRigidbody.useGravity = true;
                productRigidbody.isKinematic = false;
            }
            else {
                // Ensure rigidbody is configured properly
                productRigidbody.useGravity = true;
                productRigidbody.isKinematic = false;
            }

            Debug.Log($"✅ SpawnedProduct initialized: Collider.isTrigger={productCollider.isTrigger}, Rigidbody.isKinematic={productRigidbody.isKinematic}");
        }

        // IInteractable implementation
        public bool CanInteract() {
            return canBePickedUp && !isPickedUp.Value && productData != null;
        }

        public void Interact(PlayerInteraction player) {
            if (!CanInteract()) return;

            Debug.Log($"🤏 Attempting to pick up {productData.productName}");

            // Use the player's inventory system
            var playerInventory = player.GetComponent<PlayerInventory>();
            if (playerInventory != null) {
                playerInventory.PickupItem(this);
            }
            else {
                Debug.LogError("Player does not have PlayerInventory component!");
            }
        }

        public string GetInteractionText() {
            if (productData == null) return "Pick up Item";
            return $"Pick up {productData.productName}";
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPickupServerRpc(ulong playerId) {
            if (!IsServer) return;

            if (isPickedUp.Value) {
                Debug.Log("Product already picked up by someone else");
                return;
            }

            // Mark as picked up
            isPickedUp.Value = true;
            pickedUpByPlayer.Value = playerId;

            // Add to player's inventory (we'll create this next)
            // For now, just move to player position
            var playerNetObj = NetworkManager.Singleton.ConnectedClients[playerId].PlayerObject;
            if (playerNetObj != null) {
                transform.position = playerNetObj.transform.position + Vector3.up * 1.5f;
                transform.SetParent(playerNetObj.transform);

                // Disable physics while held
                if (productRigidbody != null) {
                    productRigidbody.isKinematic = true;
                }

                // Disable collider while held to prevent interference
                if (productCollider != null) {
                    productCollider.enabled = false;
                }
            }

            Debug.Log($"📦 {productData.productName} picked up by player {playerId}");
        }

        [ServerRpc(RequireOwnership = false)]
        public void PlaceOnShelfServerRpc(Vector3 position, Quaternion rotation) {
            if (!IsServer) return;

            Debug.Log($"📦 PlaceOnShelfServerRpc called for {gameObject.name}");

            // Get the player who was holding this item (for network variable clearing)
            ulong previousHolder = pickedUpByPlayer.Value;

            // Place the product at the specified location
            transform.position = position;
            transform.rotation = rotation;
            transform.SetParent(null); // Always set to world space for NetworkObjects

            // Re-enable physics and restore normal layer
            if (productRigidbody != null) {
                productRigidbody.isKinematic = false;
                productRigidbody.useGravity = true;
                Debug.Log($"📦 Restored rigidbody physics for {gameObject.name}");
            }

            if (productCollider != null) {
                productCollider.enabled = true;
                Debug.Log($"📦 Re-enabled collider for {gameObject.name}");
            }

            // Restore to normal layer
            gameObject.layer = 0; // Default layer
            Debug.Log($"📦 Restored layer for {gameObject.name}");

            // Mark as no longer picked up
            isPickedUp.Value = false;
            pickedUpByPlayer.Value = 999;

            // Clear the network variable in the player's inventory
            if (previousHolder != 999 && NetworkManager.Singleton.ConnectedClients.ContainsKey(previousHolder)) {
                var playerNetObj = NetworkManager.Singleton.ConnectedClients[previousHolder].PlayerObject;
                if (playerNetObj != null) {
                    var playerInventory = playerNetObj.GetComponent<PlayerInventory>();
                    if (playerInventory != null) {
                        playerInventory.ClearNetworkVariableServerRpc();
                        Debug.Log($"🎒 Cleared NetworkVariable for player {previousHolder}");
                    }
                }
            }

            // Remove from spawn point tracking
            if (spawnPoint != null) {
                spawnPoint.RemoveProduct(this);
            }

            Debug.Log($"📦 {productData.productName} placement complete - should be visible on shelf now");
        }

        private void OnPickupStateChanged(bool oldValue, bool newValue) {
            // Update visual state based on pickup status
            // Collider enabling/disabling is now handled in the ServerRpc methods
        }

        private void OnPickupPlayerChanged(ulong oldValue, ulong newValue) {
            // Could be used for visual feedback about who's holding what
        }

        // Mouse interaction for highlighting
        void OnMouseEnter() {
            if (CanInteract()) {
                SetHighlight(true);
            }
        }

        void OnMouseExit() {
            SetHighlight(false);
        }

        private void SetHighlight(bool highlight) {
            if (productRenderer == null) return;

            isHighlighted = highlight;

            if (highlight && highlightMaterial != null) {
                productRenderer.material = highlightMaterial;
            }
            else if (!highlight && originalMaterial != null) {
                productRenderer.material = originalMaterial;
            }
        }

        // Cleanup
        public override void OnNetworkDespawn() {
            if (isPickedUp != null) isPickedUp.OnValueChanged -= OnPickupStateChanged;
            if (pickedUpByPlayer != null) pickedUpByPlayer.OnValueChanged -= OnPickupPlayerChanged;
        }

        void OnDestroy() {
            // Remove from spawn point if still tracked
            if (spawnPoint != null) {
                spawnPoint.RemoveProduct(this);
            }
        }
    }
}