using UnityEngine;
using DispensarySimulator.Player;

namespace DispensarySimulator.Products {
    public class Product : MonoBehaviour, IInteractable {
        [Header("Product Configuration")]
        public ProductData productData;
        public int stockAmount = 0;

        [Header("Display Settings")]
        public bool isOnDisplay = false;
        public Transform displayPosition;

        [Header("Interaction")]
        public bool canBePickedUp = true;
        public bool isSelected = false;

        // Components
        private Collider productCollider;
        private Renderer productRenderer;
        // Optional outline component (you can add this later with a third-party asset)
        private Component outline;

        // Events
        public System.Action<Product> OnProductSelected;
        public System.Action<Product> OnProductDeselected;

        void Start() {
            InitializeProduct();
        }

        private void InitializeProduct() {
            productCollider = GetComponent<Collider>();
            productRenderer = GetComponent<Renderer>();

            // Try to find outline component (optional - for third-party outline assets)
            outline = GetComponent("Outline");

            if (productData == null) {
                Debug.LogError($"Product {gameObject.name} has no ProductData assigned!");
                return;
            }

            // Set up the product based on data
            gameObject.name = productData.productName;

            // Disable outline by default if it exists
            if (outline != null && outline is MonoBehaviour) {
                ((MonoBehaviour)outline).enabled = false;
            }
        }

        void OnMouseEnter() {
            if (canBePickedUp && !isSelected) {
                HighlightProduct(true);
            }
        }

        void OnMouseExit() {
            if (!isSelected) {
                HighlightProduct(false);
            }
        }

        void OnMouseDown() {
            if (canBePickedUp) {
                SelectProduct();
            }
        }

        public void SelectProduct() {
            isSelected = true;
            HighlightProduct(true);
            OnProductSelected?.Invoke(this);

            Debug.Log($"Selected product: {productData.productName}");
        }

        public void DeselectProduct() {
            isSelected = false;
            HighlightProduct(false);
            OnProductDeselected?.Invoke(this);
        }

        private void HighlightProduct(bool highlight) {
            // Try to use outline if available
            if (outline != null && outline is MonoBehaviour) {
                ((MonoBehaviour)outline).enabled = highlight;
            }
            else if (productRenderer != null) {
                // Alternative: Change material color slightly
                Color newColor = highlight ? Color.white * 1.2f : Color.white;
                productRenderer.material.color = newColor;
            }
        }

        public bool IsInStock() {
            return stockAmount > 0;
        }

        public bool RemoveFromStock(int amount = 1) {
            if (stockAmount >= amount) {
                stockAmount -= amount;

                if (stockAmount <= 0) {
                    OnStockEmpty();
                }

                return true;
            }
            return false;
        }

        public void AddToStock(int amount) {
            stockAmount += amount;
            stockAmount = Mathf.Clamp(stockAmount, 0, productData.maxStock);
        }

        private void OnStockEmpty() {
            Debug.Log($"{productData.productName} is out of stock!");
            // Could trigger restocking alert or hide product
        }

        public void PlaceOnDisplay(Transform newDisplayPosition) {
            if (newDisplayPosition != null) {
                displayPosition = newDisplayPosition;
                transform.position = displayPosition.position;
                transform.rotation = displayPosition.rotation;
                isOnDisplay = true;
                canBePickedUp = false; // Can't pick up displayed items

                Debug.Log($"{productData.productName} placed on display");
            }
        }

        public void RemoveFromDisplay() {
            isOnDisplay = false;
            canBePickedUp = true;
            displayPosition = null;

            Debug.Log($"{productData.productName} removed from display");
        }

        // Get formatted price string
        public string GetPriceString() {
            return $"${productData.sellPrice:F2}";
        }

        // Get stock status
        public StockStatus GetStockStatus() {
            if (stockAmount <= 0) return StockStatus.OutOfStock;
            if (stockAmount <= productData.minStock) return StockStatus.LowStock;
            return StockStatus.InStock;
        }

        // Check if product needs restocking
        public bool NeedsRestocking() {
            return stockAmount <= productData.minStock;
        }

        // IInteractable implementation
        public bool CanInteract() {
            return canBePickedUp && productData != null;
        }

        public void Interact(PlayerInteraction player) {
            if (!CanInteract()) return;

            Debug.Log($"Interacting with {productData.productName}");
            Debug.Log($"Price: {GetPriceString()}, Stock: {stockAmount}");

            // For now, just show product info
            // Later this could open a purchase menu or add to cart
        }

        public string GetInteractionText() {
            if (productData == null) return "Examine Item";

            return $"Examine {productData.productName}";
        }
    }

    public enum StockStatus {
        InStock,
        LowStock,
        OutOfStock
    }
}