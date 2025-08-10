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

        // Network state - SIMPLIFIED
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
            Debug.Log($"🌐 CLIENT {NetworkManager.Singleton.LocalClientId}: SpawnedProduct OnNetworkSpawn for {gameObject.name}");

            InitializeComponents();

            // Subscribe to network variable changes
            isPickedUp.OnValueChanged += OnPickupStateChanged;
            pickedUpByPlayer.OnValueChanged += OnPickupPlayerChanged;

            // CRITICAL: Ensure interaction works on all clients
            SetupForInteraction();
        }

        private void SetupForInteraction() {
            Debug.Log($"🔧 CLIENT {NetworkManager.Singleton.LocalClientId}: Setting up {gameObject.name} for interaction");

            // Ensure object is on correct layer (layer 0 = Default)
            gameObject.layer = 0;

            // Ensure collider is properly configured
            if (productCollider != null) {
                productCollider.enabled = true;
                productCollider.isTrigger = false; // CRITICAL: Must NOT be trigger for raycast detection
            }

            Debug.Log($"✅ CLIENT {NetworkManager.Singleton.LocalClientId}: {gameObject.name} setup complete - Layer: {gameObject.layer}, Collider: {(productCollider != null ? $"enabled={productCollider.enabled}, trigger={productCollider.isTrigger}" : "null")}");
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

            // Ensure we have required components
            if (productCollider == null) {
                Debug.LogWarning($"Adding missing BoxCollider to {gameObject.name}");
                productCollider = gameObject.AddComponent<BoxCollider>();
                productCollider.isTrigger = false;
            }

            if (productRigidbody == null) {
                Debug.LogWarning($"Adding missing Rigidbody to {gameObject.name}");
                productRigidbody = gameObject.AddComponent<Rigidbody>();
                productRigidbody.mass = 1f;
                productRigidbody.useGravity = true;
                productRigidbody.isKinematic = false;
            }

            Debug.Log($"✅ Components initialized for {gameObject.name}");
        }

        // IInteractable implementation
        public bool CanInteract() {
            bool canInteract = canBePickedUp && !isPickedUp.Value && productData != null;

            Debug.Log($"🔍 CLIENT {NetworkManager.Singleton.LocalClientId}: CanInteract check for {gameObject.name}:");
            Debug.Log($"   canBePickedUp: {canBePickedUp}");
            Debug.Log($"   isPickedUp: {isPickedUp.Value}");
            Debug.Log($"   hasProductData: {productData != null}");
            Debug.Log($"   Result: {canInteract}");

            return canInteract;
        }

        public void Interact(PlayerInteraction player) {
            if (!CanInteract()) {
                Debug.LogWarning($"Cannot interact with {gameObject.name}");
                return;
            }

            Debug.Log($"🤏 CLIENT {NetworkManager.Singleton.LocalClientId}: Attempting to pick up {productData.productName}");

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

            Debug.Log($"📦 SERVER: Processing pickup request for {gameObject.name} by player {playerId}");

            // Mark as picked up
            isPickedUp.Value = true;
            pickedUpByPlayer.Value = playerId;

            // SIMPLIFIED: Just move to player and disable collider
            var playerNetObj = NetworkManager.Singleton.ConnectedClients[playerId].PlayerObject;
            if (playerNetObj != null) {
                Vector3 newPos = playerNetObj.transform.position + Vector3.up * 1.5f;
                transform.position = newPos;
                transform.SetParent(playerNetObj.transform);

                // Disable physics and collider while held
                if (productRigidbody != null) {
                    productRigidbody.isKinematic = true;
                }

                if (productCollider != null) {
                    productCollider.enabled = false;
                }

                // Notify clients to disable collider
                UpdateColliderStateClientRpc(false);
            }

            Debug.Log($"📦 SERVER: {productData.productName} picked up by player {playerId}");
        }

        [ServerRpc(RequireOwnership = false)]
        public void PlaceOnShelfServerRpc(Vector3 position, Quaternion rotation) {
            if (!IsServer) return;

            Debug.Log($"📦 SERVER: PlaceOnShelfServerRpc called for {gameObject.name} at {position}");

            // Place the product at the specified location
            transform.position = position;
            transform.rotation = rotation;
            transform.SetParent(null);

            // Re-enable physics
            if (productRigidbody != null) {
                productRigidbody.isKinematic = false;
                productRigidbody.useGravity = true;
            }

            if (productCollider != null) {
                productCollider.enabled = true;
            }

            gameObject.layer = 0; // Ensure correct layer

            // Notify clients to re-enable collider and correct setup
            UpdateColliderStateClientRpc(true);

            // Mark as no longer picked up
            isPickedUp.Value = false;
            pickedUpByPlayer.Value = 999;

            // Remove from spawn point tracking
            if (spawnPoint != null) {
                spawnPoint.RemoveProduct(this);
            }

            Debug.Log($"📦 SERVER: {productData.productName} placement complete at {position}");
        }

        [ClientRpc]
        private void UpdateColliderStateClientRpc(bool enabled) {
            Debug.Log($"🔄 CLIENT {NetworkManager.Singleton.LocalClientId}: UpdateColliderStateClientRpc for {gameObject.name} - enabled: {enabled}");

            if (productCollider != null) {
                productCollider.enabled = enabled;
                productCollider.isTrigger = false; // Always ensure it's not a trigger
            }

            // Ensure correct layer
            gameObject.layer = 0;

            Debug.Log($"🔄 CLIENT {NetworkManager.Singleton.LocalClientId}: Updated {gameObject.name} - Collider enabled: {productCollider?.enabled}, Layer: {gameObject.layer}");
        }

        private void OnPickupStateChanged(bool oldValue, bool newValue) {
            Debug.Log($"🔄 CLIENT {NetworkManager.Singleton.LocalClientId}: Pickup state changed for {gameObject.name}: {oldValue} → {newValue}");
        }

        private void OnPickupPlayerChanged(ulong oldValue, ulong newValue) {
            Debug.Log($"🔄 CLIENT {NetworkManager.Singleton.LocalClientId}: Pickup player changed for {gameObject.name}: {oldValue} → {newValue}");
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
            if (spawnPoint != null) {
                spawnPoint.RemoveProduct(this);
            }
        }
    }
}