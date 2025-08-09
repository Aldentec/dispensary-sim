using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DispensarySimulator.Products;
using DispensarySimulator.Economy;
using DispensarySimulator.Core;
using System.Collections.Generic;

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

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip orderSound;
        public AudioClip errorSound;

        // Order state
        private StoreManager storeManager;
        private MoneyManager moneyManager;
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
            HandleInput();
        }

        private void HandleInput() {
            // Check if game is paused
            if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

            // Toggle inventory with Tab key
            if (Input.GetKeyDown(inventoryKey)) {
                ToggleInventory();
            }
        }

        public void Initialize() {
            // Find required components
            storeManager = FindObjectOfType<StoreManager>();
            moneyManager = FindObjectOfType<MoneyManager>();
            audioSource = GetComponent<AudioSource>();

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

            Debug.Log("Inventory Manager initialized - Press Tab to open inventory");
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

            // Unlock cursor for UI interaction
            Cursor.lockState = CursorLockMode.None;

            // Populate product list
            SetupProductList();

            // Update UI
            UpdateOrderUI();

            Debug.Log("Inventory opened");
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

            // Re-lock cursor
            Cursor.lockState = CursorLockMode.Locked;

            Debug.Log("Inventory closed");
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

            // Add products to store inventory
            bool orderSuccessful = true;
            foreach (OrderItem item in shoppingCart) {
                if (storeManager != null) {
                    // Add inventory without additional cost (we already paid)
                    storeManager.AddProductToInventory(item.product, item.quantity);
                }
            }

            if (orderSuccessful) {
                // Play success sound
                PlaySound(orderSound);

                SetStatusText($"Order placed! Total cost: ${totalAmount:F2}");

                // Close inventory after delay
                Invoke(nameof(CloseInventory), 2f);

                Debug.Log($"Inventory order successful! Total cost: ${totalAmount:F2}");
            }
            else {
                SetStatusText("Order failed! Please try again.");
                PlaySound(errorSound);
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