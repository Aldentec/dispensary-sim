using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DispensarySimulator.Player;
using DispensarySimulator.Products;
using DispensarySimulator.Economy;
using System.Collections.Generic;

namespace DispensarySimulator.Store {
    public class EnhancedCashRegister : MonoBehaviour, IInteractable {
        [Header("Register Settings")]
        public Transform customerPosition;
        public Transform drawerTransform; // The drawer child object
        public AudioClip registerSound;
        public AudioClip errorSound;
        public AudioClip drawerOpenSound;
        public AudioClip drawerCloseSound;

        [Header("Drawer Animation")]
        public float drawerOpenDistance = 0.2f;
        public float drawerAnimationSpeed = 2f;

        [Header("Transaction UI")]
        public GameObject transactionUI;
        public Transform productListParent;
        public GameObject productButtonPrefab;
        public TextMeshProUGUI totalAmountText;
        public TextMeshProUGUI playerMoneyText;
        public Button checkoutButton;
        public Button cancelButton;
        public TextMeshProUGUI statusText;

        [Header("Audio")]
        public AudioSource audioSource;

        // Transaction state
        private StoreManager storeManager;
        private MoneyManager moneyManager;
        private bool isTransactionActive = false;
        private List<CartItem> shoppingCart = new List<CartItem>();
        private List<GameObject> productButtons = new List<GameObject>();

        // Drawer animation state
        private Vector3 drawerClosedPosition;
        private Vector3 drawerOpenPosition;
        private bool isDrawerOpen = false;
        private Coroutine drawerAnimationCoroutine;

        [System.Serializable]
        public class CartItem {
            public ProductData product;
            public int quantity;
            public float totalPrice;

            public CartItem(ProductData product, int quantity) {
                this.product = product;
                this.quantity = quantity;
                this.totalPrice = product.sellPrice * quantity;
            }
        }

        void Start() {
            Initialize();
        }

        public void Initialize() {
            // Find required components
            storeManager = FindObjectOfType<StoreManager>();
            moneyManager = FindObjectOfType<MoneyManager>();
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null) {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Initialize drawer positions
            if (drawerTransform != null) {
                drawerClosedPosition = drawerTransform.localPosition;
                drawerOpenPosition = drawerClosedPosition + Vector3.forward * drawerOpenDistance;
                isDrawerOpen = false;

                Debug.Log($"Drawer initialized. Closed: {drawerClosedPosition}, Open: {drawerOpenPosition}");
            }
            else {
                Debug.LogWarning("Drawer Transform not assigned! Please assign the drawer child object.");
            }

            // Set up UI buttons
            if (checkoutButton != null) {
                checkoutButton.onClick.AddListener(ProcessCheckout);
            }

            if (cancelButton != null) {
                cancelButton.onClick.AddListener(CancelTransaction);
            }

            // Hide transaction UI initially
            if (transactionUI != null) {
                transactionUI.SetActive(false);
            }

            Debug.Log("Enhanced Cash Register initialized");
        }

        public bool CanInteract() {
            return !isTransactionActive && storeManager != null && storeManager.isOpen;
        }

        public void Interact(PlayerInteraction player) {
            if (isTransactionActive) return;

            StartTransaction();
        }

        public string GetInteractionText() {
            if (isTransactionActive)
                return "Transaction in progress...";
            else
                return "Use Cash Register";
        }

        private void StartTransaction() {
            isTransactionActive = true;
            shoppingCart.Clear();

            // Open the drawer with animation
            OpenDrawer();

            // Show transaction UI
            if (transactionUI != null) {
                transactionUI.SetActive(true);
            }

            // Unlock cursor for UI interaction
            Cursor.lockState = CursorLockMode.None;

            // Populate product list
            SetupProductList();

            // Update UI
            UpdateTransactionUI();

            Debug.Log("Transaction started");
        }

        private void SetupProductList() {
            // Clear existing buttons
            ClearProductButtons();

            if (storeManager == null || productListParent == null) return;

            // Get available products from store
            var availableProducts = storeManager.GetAvailableProducts();

            foreach (Product product in availableProducts) {
                if (product.productData != null && product.IsInStock()) {
                    CreateProductButton(product);
                }
            }

            if (availableProducts.Count == 0) {
                SetStatusText("No products in stock!");
            }
        }

        private void CreateProductButton(Product product) {
            if (productButtonPrefab == null) return;

            GameObject buttonObj = Instantiate(productButtonPrefab, productListParent);
            productButtons.Add(buttonObj);

            // Set up button text
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null) {
                buttonText.text = $"{product.productData.productName}\n${product.productData.sellPrice:F2}\nStock: {product.stockAmount}";
            }

            // Set up button click
            Button button = buttonObj.GetComponent<Button>();
            if (button != null) {
                button.onClick.AddListener(() => AddProductToCart(product.productData));
            }
        }

        private void AddProductToCart(ProductData productData) {
            // Check if product already in cart
            CartItem existingItem = shoppingCart.Find(item => item.product == productData);

            if (existingItem != null) {
                existingItem.quantity++;
                existingItem.totalPrice = existingItem.product.sellPrice * existingItem.quantity;
            }
            else {
                shoppingCart.Add(new CartItem(productData, 1));
            }

            // Play add sound
            PlaySound(registerSound);

            UpdateTransactionUI();
            SetStatusText($"Added {productData.productName} to cart");
        }

        private void UpdateTransactionUI() {
            // Update total amount
            float totalAmount = 0f;
            foreach (CartItem item in shoppingCart) {
                totalAmount += item.totalPrice;
            }

            if (totalAmountText != null) {
                totalAmountText.text = $"Total: ${totalAmount:F2}";
            }

            // Update player money display
            if (playerMoneyText != null && moneyManager != null) {
                playerMoneyText.text = $"Your Money: {moneyManager.GetFormattedMoney()}";
            }

            // Enable/disable checkout button based on affordability
            if (checkoutButton != null && moneyManager != null) {
                bool canAfford = moneyManager.CanAfford(totalAmount);
                checkoutButton.interactable = canAfford && shoppingCart.Count > 0;

                if (!canAfford && shoppingCart.Count > 0) {
                    SetStatusText("Insufficient funds!");
                }
                else if (shoppingCart.Count == 0) {
                    SetStatusText("Add items to cart to checkout");
                }
                else {
                    SetStatusText("Ready to checkout");
                }
            }
        }

        public void ProcessCheckout() {
            if (shoppingCart.Count == 0) return;

            float totalAmount = 0f;
            bool allItemsAvailable = true;

            // Calculate total and verify stock
            foreach (CartItem item in shoppingCart) {
                totalAmount += item.totalPrice;

                // Check if items are still in stock
                if (!storeManager.HasInStock(item.product, item.quantity)) {
                    allItemsAvailable = false;
                    SetStatusText($"Not enough {item.product.productName} in stock!");
                    PlaySound(errorSound);
                    return;
                }
            }

            // Check if player can afford it
            if (!moneyManager.CanAfford(totalAmount)) {
                SetStatusText("Insufficient funds!");
                PlaySound(errorSound);
                return;
            }

            // Process the sale
            bool saleSuccessful = true;
            foreach (CartItem item in shoppingCart) {
                if (!storeManager.SellProduct(item.product, item.quantity)) {
                    saleSuccessful = false;
                    break;
                }
            }

            if (saleSuccessful) {
                // Play success sound
                PlaySound(registerSound);

                SetStatusText($"Sale complete! Total: ${totalAmount:F2}");

                // Close drawer and then close transaction after delay
                CloseDrawer();
                Invoke(nameof(CancelTransaction), 3f); // Give time for drawer to close and user to see success message

                Debug.Log($"Checkout successful! Total: ${totalAmount:F2}");
            }
            else {
                SetStatusText("Sale failed! Please try again.");
                PlaySound(errorSound);
            }
        }

        public void CancelTransaction() {
            isTransactionActive = false;
            shoppingCart.Clear();

            // Close drawer if it's open
            if (isDrawerOpen) {
                CloseDrawer();
            }

            // Hide transaction UI
            if (transactionUI != null) {
                transactionUI.SetActive(false);
            }

            // Clear product buttons
            ClearProductButtons();

            // Re-lock cursor
            Cursor.lockState = CursorLockMode.Locked;

            Debug.Log("Transaction cancelled");
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

        // Drawer Animation Methods
        private void OpenDrawer() {
            if (drawerTransform == null || isDrawerOpen) return;

            if (drawerAnimationCoroutine != null) {
                StopCoroutine(drawerAnimationCoroutine);
            }

            drawerAnimationCoroutine = StartCoroutine(AnimateDrawer(drawerOpenPosition, true));
            PlaySound(drawerOpenSound);
        }

        private void CloseDrawer() {
            if (drawerTransform == null || !isDrawerOpen) return;

            if (drawerAnimationCoroutine != null) {
                StopCoroutine(drawerAnimationCoroutine);
            }

            drawerAnimationCoroutine = StartCoroutine(AnimateDrawer(drawerClosedPosition, false));
            PlaySound(drawerCloseSound);
        }

        private System.Collections.IEnumerator AnimateDrawer(Vector3 targetPosition, bool opening) {
            Vector3 startPosition = drawerTransform.localPosition;
            float elapsedTime = 0f;
            float duration = 1f / drawerAnimationSpeed;

            while (elapsedTime < duration) {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;

                // Use smooth animation curve
                progress = Mathf.SmoothStep(0f, 1f, progress);

                drawerTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, progress);
                yield return null;
            }

            // Ensure final position is exact
            drawerTransform.localPosition = targetPosition;
            isDrawerOpen = opening;

            Debug.Log($"Drawer {(opening ? "opened" : "closed")}");
        }

        // Public method to manually test drawer (for debugging)
        [ContextMenu("Test Open Drawer")]
        public void TestOpenDrawer() {
            OpenDrawer();
        }

        [ContextMenu("Test Close Drawer")]
        public void TestCloseDrawer() {
            CloseDrawer();
        }

        void OnDestroy() {
            // Stop any running drawer animation
            if (drawerAnimationCoroutine != null) {
                StopCoroutine(drawerAnimationCoroutine);
            }

            ClearProductButtons();
        }
    }
}