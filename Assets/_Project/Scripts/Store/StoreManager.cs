using UnityEngine;
using System.Collections.Generic;
using DispensarySimulator.Products;
using DispensarySimulator.Economy;
using DispensarySimulator.Player;

namespace DispensarySimulator.Store {
    public class StoreManager : MonoBehaviour {
        [Header("Store Settings")]
        public string storeName = "Green Leaf Dispensary";
        public bool isOpen = true;
        public float storeHours = 12f; // hours per day

        [Header("Store References")]
        public Transform[] displayShelves;
        public Transform[] productSpawnPoints;
        public CashRegister cashRegister;

        [Header("Inventory")]
        public List<Product> availableProducts = new List<Product>();
        public int maxInventorySize = 100;

        // Store state
        private List<Product> currentInventory = new List<Product>();
        private float currentStoreTime = 0f;
        private bool storeInitialized = false;

        // References
        private MoneyManager moneyManager;

        // Events
        public System.Action OnStoreOpened;
        public System.Action OnStoreClosed;
        public System.Action<Product> OnProductSold;

        void Start() {
            InitializeStore();
        }

        void Update() {
            UpdateStoreTime();
        }

        private void InitializeStore() {
            // Get money manager reference
            moneyManager = FindObjectOfType<MoneyManager>();
            if (moneyManager == null) {
                Debug.LogError("StoreManager: No MoneyManager found!");
            }

            // Initialize starting inventory
            SetupStartingInventory();

            // Set up cash register
            if (cashRegister != null) {
                cashRegister.Initialize(this);
            }

            storeInitialized = true;
            Debug.Log($"{storeName} initialized and ready for business!");
        }

        private void SetupStartingInventory() {
            // Add some starting products
            foreach (Product productPrefab in availableProducts) {
                if (productPrefab != null && productPrefab.productData != null) {
                    AddProductToInventory(productPrefab.productData, 10);
                }
            }
        }

        private void UpdateStoreTime() {
            if (!isOpen) return;

            currentStoreTime += Time.deltaTime;

            // Handle store closing (simplified)
            if (currentStoreTime >= storeHours * 60f) // Convert hours to seconds (simplified)
            {
                CloseStore();
            }
        }

        public void OpenStore() {
            isOpen = true;
            currentStoreTime = 0f;
            OnStoreOpened?.Invoke();
            Debug.Log($"{storeName} is now open!");
        }

        public void CloseStore() {
            isOpen = false;
            OnStoreClosed?.Invoke();
            Debug.Log($"{storeName} is now closed!");
        }

        public bool AddProductToInventory(ProductData productData, int amount) {
            if (currentInventory.Count >= maxInventorySize) {
                Debug.Log("Inventory is full!");
                return false;
            }

            // Check if product already exists in inventory
            Product existingProduct = currentInventory.Find(p => p.productData == productData);

            if (existingProduct != null) {
                existingProduct.AddToStock(amount);
            }
            else {
                // Create new product instance
                GameObject newProductObj = Instantiate(productData.prefab);
                Product newProduct = newProductObj.GetComponent<Product>();

                if (newProduct != null) {
                    newProduct.productData = productData;
                    newProduct.AddToStock(amount);
                    currentInventory.Add(newProduct);

                    // Place in storage area or on shelf
                    PlaceProductInStore(newProduct);
                }
            }

            Debug.Log($"Added {amount} {productData.productName} to inventory");
            return true;
        }

        // Separate method for paid restocking (keeps the old functionality)
        public bool RestockProductPaid(ProductData productData, int amount) {
            // Calculate restock cost
            float restockCost = productData.basePrice * amount;

            if (moneyManager != null && !moneyManager.CanAfford(restockCost)) {
                Debug.Log($"Cannot afford to restock {productData.productName}. Cost: ${restockCost:F2}");
                return false;
            }

            // Spend money for restocking
            if (moneyManager != null) {
                moneyManager.SpendMoney(restockCost);
            }

            // Add to inventory
            return AddProductToInventory(productData, amount);
        }

        private void PlaceProductInStore(Product product) {
            // Simple placement logic - put on first available shelf
            if (displayShelves.Length > 0) {
                Transform shelf = displayShelves[Random.Range(0, displayShelves.Length)];
                product.PlaceOnDisplay(shelf);
            }
        }

        public bool SellProduct(ProductData productData, int amount = 1) {
            Product product = currentInventory.Find(p => p.productData == productData);

            if (product == null || !product.IsInStock()) {
                Debug.Log($"Cannot sell {productData.productName} - not in stock!");
                return false;
            }

            if (product.stockAmount < amount) {
                Debug.Log($"Cannot sell {amount} {productData.productName} - only {product.stockAmount} in stock!");
                return false;
            }

            if (product.RemoveFromStock(amount)) {
                float saleAmount = productData.sellPrice * amount;

                // Add money to economy system
                if (moneyManager != null) {
                    moneyManager.AddSaleEarnings(saleAmount);
                }

                OnProductSold?.Invoke(product);
                Debug.Log($"Sold {amount} {productData.productName} for ${saleAmount:F2}");
                return true;
            }

            return false;
        }

        public List<Product> GetAvailableProducts() {
            return currentInventory.FindAll(p => p.IsInStock());
        }

        public int GetTotalInventoryCount() {
            int total = 0;
            foreach (Product product in currentInventory) {
                total += product.stockAmount;
            }
            return total;
        }

        // Check if specific product has enough stock
        public bool HasInStock(ProductData productData, int requiredAmount) {
            Product product = currentInventory.Find(p => p.productData == productData);
            return product != null && product.stockAmount >= requiredAmount;
        }
    }
}