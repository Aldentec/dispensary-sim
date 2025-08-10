using UnityEngine;
using Unity.Netcode;
using DispensarySimulator.Player;
using DispensarySimulator.Products;

namespace DispensarySimulator.Store {
    public class ShelfSlot : NetworkBehaviour, IInteractable {
        [Header("Shelf Slot Settings")]
        public Transform placementPoint;
        public float slotRadius = 0.5f;
        public bool isOccupied = false;

        [Header("Visual Feedback")]
        public GameObject highlightEffect;
        public Material availableMaterial;
        public Material occupiedMaterial;

        // Network state
        private NetworkVariable<bool> isOccupiedNet = new NetworkVariable<bool>(false);
        private NetworkVariable<ulong> placedProductId = new NetworkVariable<ulong>(0);

        // Components
        private Renderer slotRenderer;
        private GameObject currentProduct;

        // State
        private bool isHighlighted = false;

        public override void OnNetworkSpawn() {
            // Subscribe to network state changes
            isOccupiedNet.OnValueChanged += OnOccupiedStateChanged;

            InitializeComponents();
        }

        void Start() {
            if (placementPoint == null) {
                placementPoint = transform;
            }

            InitializeComponents();
        }

        private void InitializeComponents() {
            slotRenderer = GetComponent<Renderer>();

            // Update visual state
            UpdateSlotVisual();
        }

        // IInteractable implementation
        public bool CanInteract() {
            // Can only interact if we have a product to place and slot is empty
            var playerInventory = FindObjectOfType<PlayerInventory>();
            return playerInventory != null &&
                   playerInventory.IsHoldingItem() &&
                   !isOccupiedNet.Value;
        }

        public void Interact(PlayerInteraction player) {
            if (!CanInteract()) return;

            var playerInventory = player.GetComponent<PlayerInventory>();
            if (playerInventory == null) return;

            var heldProduct = playerInventory.GetHeldItem();
            if (heldProduct == null) return;

            Debug.Log($"🏪 Placing {heldProduct.name} on shelf slot");

            // Clear inventory immediately on the client side for instant feedback
            Debug.Log("🎒 About to clear player inventory directly");
            playerInventory.ForceClientClearInventory();

            // Request placement on server (back to simple version)
            RequestPlaceProductServerRpc(heldProduct.GetComponent<NetworkObject>().NetworkObjectId);
        }

        public string GetInteractionText() {
            if (!CanInteract()) return "";

            var playerInventory = FindObjectOfType<PlayerInventory>();
            if (playerInventory != null && playerInventory.IsHoldingItem()) {
                var heldItem = playerInventory.GetHeldItem();
                return $"Place {heldItem.name} on shelf";
            }

            return "Place item on shelf";
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPlaceProductServerRpc(ulong productNetworkId) {
            if (!IsServer) return;

            if (isOccupiedNet.Value) {
                Debug.LogWarning("Shelf slot is already occupied!");
                return;
            }

            // Find the product by network ID
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(productNetworkId, out NetworkObject productNetObj);
            if (productNetObj == null) {
                Debug.LogError("Could not find product to place!");
                return;
            }

            var spawnedProduct = productNetObj.GetComponent<SpawnedProduct>();
            if (spawnedProduct == null) {
                Debug.LogError("Product does not have SpawnedProduct component!");
                return;
            }

            Debug.Log("📦 Server: About to place product on shelf");

            // Place the product on this shelf slot
            PlaceProductOnShelf(spawnedProduct);
        }

        private void PlaceProductOnShelf(SpawnedProduct product) {
            if (!IsServer) return;

            // Position the product at the placement point (don't parent to avoid NetworkObject issues)
            product.transform.position = placementPoint.position;
            product.transform.rotation = placementPoint.rotation;
            // DON'T PARENT: product.transform.SetParent(placementPoint); // This causes NetworkObject errors

            // Update network state
            isOccupiedNet.Value = true;
            placedProductId.Value = product.NetworkObjectId;

            // Store reference
            currentProduct = product.gameObject;

            // Call the product's placement method
            Vector3 pos = placementPoint.position;
            Quaternion rot = placementPoint.rotation;
            product.PlaceOnShelfServerRpc(pos, rot);

            // Update visual
            UpdateSlotVisual();

            Debug.Log($"📦 Product {product.name} placed on shelf slot");
        }

        [ServerRpc(RequireOwnership = false)]
        public void RemoveProductServerRpc() {
            if (!IsServer) return;

            // Clear the slot
            isOccupiedNet.Value = false;
            placedProductId.Value = 0;
            currentProduct = null;

            // Update visual
            UpdateSlotVisual();

            Debug.Log("📦 Product removed from shelf slot");
        }

        private void OnOccupiedStateChanged(bool oldValue, bool newValue) {
            isOccupied = newValue;
            UpdateSlotVisual();
        }

        private void UpdateSlotVisual() {
            if (slotRenderer == null) return;

            // Change material based on occupation state
            if (isOccupiedNet.Value && occupiedMaterial != null) {
                slotRenderer.material = occupiedMaterial;
            }
            else if (!isOccupiedNet.Value && availableMaterial != null) {
                slotRenderer.material = availableMaterial;
            }
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
            isHighlighted = highlight;

            if (highlightEffect != null) {
                highlightEffect.SetActive(highlight);
            }
        }

        // Public methods
        public bool IsSlotOccupied() {
            return isOccupiedNet.Value;
        }

        public bool CanAcceptProduct(ProductData productData) {
            // Can accept if not occupied and product data is valid
            return !isOccupiedNet.Value && productData != null;
        }

        public GameObject GetPlacedProduct() {
            return currentProduct;
        }

        // Debug visualization
        void OnDrawGizmosSelected() {
            if (placementPoint != null) {
                Gizmos.color = isOccupied ? Color.red : Color.green;
                Gizmos.DrawWireSphere(placementPoint.position, slotRadius);
                Gizmos.DrawWireCube(placementPoint.position, Vector3.one * 0.2f);
            }
        }

        // Cleanup
        public override void OnNetworkDespawn() {
            if (isOccupiedNet != null) isOccupiedNet.OnValueChanged -= OnOccupiedStateChanged;
        }
    }
}