using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using DispensarySimulator.Products;

namespace DispensarySimulator.Store {

    // Move struct outside of class to avoid generic constraint issues
    [System.Serializable]
    public struct ProductInventoryItem : INetworkSerializable, System.IEquatable<ProductInventoryItem> {
        public int productIndex; // Index in availableProducts array
        public int quantity;
        public bool isDisplayed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref productIndex);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref isDisplayed);
        }

        // IEquatable implementation required for NetworkList
        public bool Equals(ProductInventoryItem other) {
            return productIndex == other.productIndex &&
                   quantity == other.quantity &&
                   isDisplayed == other.isDisplayed;
        }

        public override bool Equals(object obj) {
            return obj is ProductInventoryItem other && Equals(other);
        }

        public override int GetHashCode() {
            // Unity-compatible hash code generation
            unchecked {
                int hash = 17;
                hash = hash * 23 + productIndex.GetHashCode();
                hash = hash * 23 + quantity.GetHashCode();
                hash = hash * 23 + isDisplayed.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(ProductInventoryItem left, ProductInventoryItem right) {
            return left.Equals(right);
        }

        public static bool operator !=(ProductInventoryItem left, ProductInventoryItem right) {
            return !left.Equals(right);
        }
    }

    public class StoreManager : NetworkBehaviour {
        [Header("Product Management")]
        public ProductData[] availableProducts; // All products that can be ordered/sold

        [Header("Store Layout")]
        public Transform[] shelfPositions;
        public ProductSpawnPoint deliveryPoint;
        public ShelfSlot[] shelfSlots;

        [Header("Store Settings")]
        public int maxProductsPerShelf = 10;
        public float restockAlertThreshold = 0.2f; // Alert when 20% or less stock remains
        public bool isOpen = true; // Store operation status

        // Network state
        private NetworkList<ProductInventoryItem> storeInventory;

        // Events
        public System.Action<ProductData, int> OnProductStocked;
        public System.Action<ProductData> OnProductOutOfStock;
        public System.Action<ProductData, int> OnProductSold;

        void Awake() {
            // Initialize network list
            storeInventory = new NetworkList<ProductInventoryItem>();
        }

        public override void OnNetworkSpawn() {
            // Find components if not assigned
            if (deliveryPoint == null) {
                deliveryPoint = FindObjectOfType<ProductSpawnPoint>();
            }

            if (shelfSlots == null || shelfSlots.Length == 0) {
                shelfSlots = FindObjectsOfType<ShelfSlot>();
            }

            // Subscribe to inventory changes
            storeInventory.OnListChanged += OnInventoryChanged;

            Debug.Log($"🏪 StoreManager initialized with {availableProducts.Length} available products");
        }

        private void OnInventoryChanged(NetworkListEvent<ProductInventoryItem> changeEvent) {
            // React to inventory changes across the network
            switch (changeEvent.Type) {
                case NetworkListEvent<ProductInventoryItem>.EventType.Add:
                    Debug.Log($"🏪 Product added to store inventory");
                    break;
                case NetworkListEvent<ProductInventoryItem>.EventType.RemoveAt:
                    Debug.Log($"🏪 Product removed from store inventory");
                    break;
                case NetworkListEvent<ProductInventoryItem>.EventType.Value:
                    Debug.Log($"🏪 Store inventory updated");
                    break;
            }
        }

        // Add products to store inventory (called when products are physically placed on shelves)
        [ServerRpc(RequireOwnership = false)]
        public void AddProductToInventoryServerRpc(int productIndex, int quantity) {
            if (!IsServer) return;

            if (productIndex < 0 || productIndex >= availableProducts.Length) {
                Debug.LogError($"Invalid product index: {productIndex}");
                return;
            }

            // Find existing inventory item or create new one
            for (int i = 0; i < storeInventory.Count; i++) {
                var item = storeInventory[i];
                if (item.productIndex == productIndex) {
                    item.quantity += quantity;
                    storeInventory[i] = item;

                    OnProductStocked?.Invoke(availableProducts[productIndex], quantity);
                    Debug.Log($"🏪 Added {quantity}x {availableProducts[productIndex].productName} to inventory");
                    return;
                }
            }

            // Create new inventory item
            var newItem = new ProductInventoryItem {
                productIndex = productIndex,
                quantity = quantity,
                isDisplayed = false
            };

            storeInventory.Add(newItem);
            OnProductStocked?.Invoke(availableProducts[productIndex], quantity);
            Debug.Log($"🏪 Added new product {availableProducts[productIndex].productName} x{quantity} to inventory");
        }

        // Legacy method for compatibility (converts ProductData to index)
        public void AddProductToInventory(ProductData productData, int quantity) {
            int index = System.Array.IndexOf(availableProducts, productData);
            if (index >= 0) {
                AddProductToInventoryServerRpc(index, quantity);
            }
            else {
                Debug.LogError($"Product {productData.productName} not found in available products!");
            }
        }

        // Remove products from inventory (when sold)
        [ServerRpc(RequireOwnership = false)]
        public void RemoveProductFromInventoryServerRpc(int productIndex, int quantity) {
            if (!IsServer) return;

            for (int i = 0; i < storeInventory.Count; i++) {
                var item = storeInventory[i];
                if (item.productIndex == productIndex) {
                    item.quantity = Mathf.Max(0, item.quantity - quantity);

                    if (item.quantity <= 0) {
                        storeInventory.RemoveAt(i);
                        OnProductOutOfStock?.Invoke(availableProducts[productIndex]);
                    }
                    else {
                        storeInventory[i] = item;
                    }

                    OnProductSold?.Invoke(availableProducts[productIndex], quantity);
                    Debug.Log($"🏪 Sold {quantity}x {availableProducts[productIndex].productName}");
                    return;
                }
            }

            Debug.LogWarning($"Tried to remove {availableProducts[productIndex].productName} but not in inventory");
        }

        // Get current stock of a product
        public int GetProductStock(ProductData productData) {
            int index = System.Array.IndexOf(availableProducts, productData);
            if (index < 0) return 0;

            foreach (var item in storeInventory) {
                if (item.productIndex == index) {
                    return item.quantity;
                }
            }
            return 0;
        }

        // Check if product is in stock
        public bool IsInStock(ProductData productData) {
            return GetProductStock(productData) > 0;
        }

        // Get all products that need restocking
        public List<ProductData> GetProductsNeedingRestock() {
            var restockList = new List<ProductData>();

            foreach (var product in availableProducts) {
                int currentStock = GetProductStock(product);
                int threshold = Mathf.RoundToInt(product.maxStock * restockAlertThreshold);

                if (currentStock <= threshold) {
                    restockList.Add(product);
                }
            }

            return restockList;
        }

        // Get formatted inventory display
        public string GetInventoryDisplayText() {
            var text = "Store Inventory:\n";

            foreach (var item in storeInventory) {
                if (item.productIndex < availableProducts.Length) {
                    var product = availableProducts[item.productIndex];
                    text += $"• {product.productName}: {item.quantity}\n";
                }
            }

            return text;
        }

        // Find available shelf slots
        public ShelfSlot FindAvailableShelfSlot(ProductData productData) {
            foreach (var slot in shelfSlots) {
                if (slot.CanAcceptProduct(productData)) {
                    return slot;
                }
            }
            return null;
        }

        // Legacy compatibility methods for existing CashRegister scripts
        public bool SellProduct(ProductData productData, int quantity = 1) {
            if (!IsInStock(productData)) {
                return false;
            }

            int index = System.Array.IndexOf(availableProducts, productData);
            if (index >= 0) {
                RemoveProductFromInventoryServerRpc(index, quantity);
                return true;
            }
            return false;
        }

        public int GetTotalInventoryCount() {
            int total = 0;
            foreach (var item in storeInventory) {
                total += item.quantity;
            }
            return total;
        }

        public ProductData[] GetAvailableProducts() {
            return availableProducts;
        }

        public bool HasInStock(ProductData productData) {
            return IsInStock(productData);
        }

        // Cleanup
        public override void OnNetworkDespawn() {
            if (storeInventory != null) {
                storeInventory.OnListChanged -= OnInventoryChanged;
            }
        }

        // Debug method to manually add products for testing
        [ContextMenu("Add Test Products")]
        public void AddTestProducts() {
            if (availableProducts.Length > 0) {
                AddProductToInventory(availableProducts[0], 5);
                Debug.Log("Added test products to inventory");
            }
        }
    }
}