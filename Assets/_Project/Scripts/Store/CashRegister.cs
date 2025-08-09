using UnityEngine;
using DispensarySimulator.Player;
using DispensarySimulator.Products;

namespace DispensarySimulator.Store {
    public class CashRegister : MonoBehaviour, IInteractable {
        [Header("Register Settings")]
        public Transform customerPosition;
        public AudioClip registerSound;

        [Header("Sale UI")]
        public GameObject saleUI;
        public UnityEngine.UI.Text itemNameText;
        public UnityEngine.UI.Text priceText;
        public UnityEngine.UI.Button confirmButton;
        public UnityEngine.UI.Button cancelButton;

        // Current sale state
        private StoreManager storeManager;
        private Product currentSaleItem;
        private bool isProcessingSale = false;
        private AudioSource audioSource;

        public void Initialize(StoreManager store) {
            storeManager = store;
            audioSource = GetComponent<AudioSource>();

            // Set up UI buttons
            if (confirmButton != null) {
                confirmButton.onClick.AddListener(ConfirmSale);
            }

            if (cancelButton != null) {
                cancelButton.onClick.AddListener(CancelSale);
            }

            // Hide sale UI initially
            if (saleUI != null) {
                saleUI.SetActive(false);
            }
        }

        public bool CanInteract() {
            return !isProcessingSale && storeManager.isOpen;
        }

        public void Interact(PlayerInteraction player) {
            if (isProcessingSale) return;

            // Check if player has selected a product
            GameObject targetObject = player.GetCurrentTarget();
            if (targetObject != null) {
                Product product = targetObject.GetComponent<Product>();
                if (product != null && product.IsInStock()) {
                    StartSale(product);
                }
                else {
                    Debug.Log("Please select a product to sell first!");
                }
            }
            else {
                // Show store status or available products
                ShowStoreInfo();
            }
        }

        public string GetInteractionText() {
            if (isProcessingSale)
                return "Processing sale...";
            else
                return "Use Cash Register";
        }

        private void StartSale(Product product) {
            currentSaleItem = product;
            isProcessingSale = true;

            // Show sale UI
            if (saleUI != null) {
                saleUI.SetActive(true);

                if (itemNameText != null) {
                    itemNameText.text = product.productData.productName;
                }

                if (priceText != null) {
                    priceText.text = product.GetPriceString();
                }
            }

            // Lock cursor for UI interaction
            Cursor.lockState = CursorLockMode.None;

            Debug.Log($"Starting sale for {product.productData.productName}");
        }

        public void ConfirmSale() {
            if (currentSaleItem == null) return;

            // Process the sale
            bool saleSuccessful = storeManager.SellProduct(currentSaleItem.productData);

            if (saleSuccessful) {
                // Play register sound
                if (audioSource != null && registerSound != null) {
                    audioSource.PlayOneShot(registerSound);
                }

                Debug.Log("Sale completed successfully!");
            }

            EndSale();
        }

        public void CancelSale() {
            Debug.Log("Sale cancelled");
            EndSale();
        }

        private void EndSale() {
            isProcessingSale = false;
            currentSaleItem = null;

            // Hide sale UI
            if (saleUI != null) {
                saleUI.SetActive(false);
            }

            // Re-lock cursor
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void ShowStoreInfo() {
            int totalProducts = storeManager.GetTotalInventoryCount();
            Debug.Log($"Store has {totalProducts} items in stock");
        }
    }
}