using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DispensarySimulator.Products;
using DispensarySimulator.Economy;
using DispensarySimulator.Core;
using DispensarySimulator.Player;
using System.Collections.Generic;
using Unity.Netcode;

namespace DispensarySimulator.Store {
    public class InventoryManager : MonoBehaviour {
        [Header("Input Settings")]
        public KeyCode inventoryKey = KeyCode.Tab;

        [Header("Inventory UI")]
        public GameObject inventoryUI;
        public Transform productListParent;
        public GameObject productButtonPrefab;
        public TextMeshProUGUI totalAmountText;
        public TextMeshProUGUI playerMoneyText;
        public Button orderButton;
        public Button cancelButton;
        public TextMeshProUGUI statusText;

        [Header("Available Products to Order")]
        public ProductData[] availableProducts; // Products you can order from suppliers

        [Header("Spawn Settings")]
        public ProductSpawnPoint spawnPoint; // Where ordered products appear

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip orderSound;
        public AudioClip errorSound;

        // Order state
        private StoreManager storeManager;
        private MoneyManager moneyManager;
        private FirstPersonController playerController;
        private bool isInventoryOpen = false;
        private List<OrderItem> shoppingCart = new List<OrderItem>();
        private List<GameObject> productButtons = new List<GameObject>();

        [System.Serializable]
        public class OrderItem {
            public ProductData product;
            public int quantity;
            public float totalCost;

            public OrderItem(ProductData product, int quantity) {
                this.product = product;
                this.quantity = quantity;
                this.totalCost = product.basePrice * quantity; // Use wholesale price (basePrice)!
            }
        }

        void Start() {
            Initialize();
        }

        void Update() {
            // Remove input handling - let FirstPersonController handle Tab key
            // The FirstPersonController will call our ToggleInventory() method
        }

        public void Initialize() {
            // Find required components
            storeManager = FindObjectOfType<StoreManager>();
            moneyManager = FindObjectOfType<MoneyManager>();
            playerController = FindObjectOfType<FirstPersonController>();
            audioSource = GetComponent<AudioSource>();

            // Find spawn point if not assigned
            if (spawnPoint == null) {
                spawnPoint = FindObjectOfType<ProductSpawnPoint>();
            }

            if (spawnPoint == null) {
                Debug.LogWarning("No ProductSpawnPoint found! Orders will not spawn physically.");
            }

            if (audioSource == null) {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Set up UI buttons
            if (orderButton != null) {
                orderButton.onClick.AddListener(ProcessOrder);
            }

            if (cancelButton != null) {
                cancelButton.onClick.AddListener(CloseInventory);
            }

            // Hide inventory UI initially
            if (inventoryUI != null) {
                inventoryUI.SetActive(false);
            }

            Debug.Log("📦 Inventory Manager initialized - Press Tab to open supplier catalog");
        }

        public void ToggleInventory() {
            if (isInventoryOpen) {
                CloseInventory();
            }
            else {
                OpenInventory();
            }
        }

        public void OpenInventory() {
            if (isInventoryOpen) return;

            isInventoryOpen = true;
            shoppingCart.Clear();

            // Show inventory UI
            if (inventoryUI != null) {
                inventoryUI.SetActive(true);
            }

            // Don't manage cursor here - FirstPersonController handles it

            // Populate product list
            SetupProductList();

            // Update UI
            UpdateOrderUI();

            Debug.Log("📦 Supplier catalog opened");
        }

        public void CloseInventory() {
            if (!isInventoryOpen) return;

            isInventoryOpen = false;
            shoppingCart.Clear();

            // Hide inventory UI
            if (inventoryUI != null) {
                inventoryUI.SetActive(false);
            }

            // Clear product buttons
            ClearProductButtons();

            // Tell FirstPersonController we're out of inventory mode
            // This handles both Tab key and Cancel button scenarios
            if (playerController != null) {
                playerController.SetInventoryMode(false);
            }

            Debug.Log("📦 Supplier catalog closed");
        }

        private void SetupProductList() {
            // Clear existing buttons
            ClearProductButtons();

            if (productListParent == null) return;

            // Use the available products array
            foreach (ProductData product in availableProducts) {
                if (product != null) {
                    CreateProductButton(product);
                }
            }

            if (availableProducts.Length == 0) {
                SetStatusText("No products available to order!");
            }
        }

        private void CreateProductButton(ProductData product) {
            if (productButtonPrefab == null) return;

            GameObject buttonObj = Instantiate(productButtonPrefab, productListParent);
            productButtons.Add(buttonObj);

            // Set up button text
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null) {
                // Show wholesale price (what you pay) vs retail price (what customers pay)
                buttonText.text = $"{product.productName}\nWholesale: ${product.basePrice:F2}\nRetail: ${product.sellPrice:F2}\nProfit: ${product.margin:F2}";
            }

            // Set up button click
            Button button = buttonObj.GetComponent<Button>();
            if (button != null) {
                button.onClick.AddListener(() => AddProductToCart(product));
            }
        }

        private void AddProductToCart(ProductData productData) {
            // Check if product already in cart
            OrderItem existingItem = shoppingCart.Find(item => item.product == productData);

            if (existingItem != null) {
                existingItem.quantity++;
                existingItem.totalCost = existingItem.product.basePrice * existingItem.quantity;
            }
            else {
                shoppingCart.Add(new OrderItem(productData, 1));
            }

            // Play add sound
            PlaySound(orderSound);

            UpdateOrderUI();
            SetStatusText($"Added {productData.productName} to order");
        }

        private void UpdateOrderUI() {
            // Update total amount (wholesale cost)
            float totalAmount = 0f;
            foreach (OrderItem item in shoppingCart) {
                totalAmount += item.totalCost;
            }

            if (totalAmountText != null) {
                totalAmountText.text = $"Order Total: ${totalAmount:F2}";
            }

            // Update player money display
            if (playerMoneyText != null && moneyManager != null) {
                playerMoneyText.text = $"Your Money: {moneyManager.GetFormattedMoney()}";
            }

            // Enable/disable order button based on affordability
            if (orderButton != null && moneyManager != null) {
                bool canAfford = moneyManager.CanAfford(totalAmount);
                orderButton.interactable = canAfford && shoppingCart.Count > 0;

                if (!canAfford && shoppingCart.Count > 0) {
                    SetStatusText("Insufficient funds!");
                }
                else if (shoppingCart.Count == 0) {
                    SetStatusText("Add items to order from supplier");
                }
                else {
                    SetStatusText("Ready to place order");
                }
            }
        }

        public void ProcessOrder() {
            if (shoppingCart.Count == 0) return;

            float totalAmount = 0f;

            // Calculate total cost
            foreach (OrderItem item in shoppingCart) {
                totalAmount += item.totalCost;
            }

            // Check if player can afford it
            if (!moneyManager.CanAfford(totalAmount)) {
                SetStatusText("Insufficient funds!");
                PlaySound(errorSound);
                return;
            }

            // Spend the money
            moneyManager.SpendMoney(totalAmount);

            // NEW: Spawn products at spawn point instead of auto-placing
            SpawnOrderedProducts();

            // Play success sound
            PlaySound(orderSound);

            SetStatusText($"Order placed! Total cost: ${totalAmount:F2}");
            SetStatusText($"Products delivered to receiving area. Go collect them!");

            // Close inventory after delay
            Invoke(nameof(CloseInventory), 3f);

            Debug.Log($"📦 Order successful! Products spawning at delivery point.");
        }

        private void SpawnOrderedProducts() {
            if (spawnPoint == null) {
                Debug.LogError("No ProductSpawnPoint assigned! Cannot spawn ordered products.");
                SetStatusText("Error: No delivery point configured!");
                return;
            }

            // Spawn each ordered product at the spawn point
            foreach (OrderItem item in shoppingCart) {
                // Find the product index in available products array
                int productIndex = System.Array.IndexOf(availableProducts, item.product);

                if (productIndex >= 0) {
                    // Request server to spawn the products
                    spawnPoint.SpawnProductsServerRpc(productIndex, item.quantity);

                    Debug.Log($"📦 Spawning {item.quantity}x {item.product.productName} at delivery point");
                }
                else {
                    Debug.LogError($"Product {item.product.productName} not found in available products!");
                }
            }
        }

        private void ClearProductButtons() {
            foreach (GameObject button in productButtons) {
                if (button != null) {
                    Destroy(button);
                }
            }
            productButtons.Clear();
        }

        private void SetStatusText(string message) {
            if (statusText != null) {
                statusText.text = message;
            }
        }

        private void PlaySound(AudioClip clip) {
            if (audioSource != null && clip != null) {
                audioSource.PlayOneShot(clip);
            }
        }

        // Public getters
        public bool IsInventoryOpen() {
            return isInventoryOpen;
        }

        void OnDestroy() {
            ClearProductButtons();
        }
    }
}