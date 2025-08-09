using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DispensarySimulator.Products;

namespace DispensarySimulator.UI {
    public class ProductButton : MonoBehaviour {
        [Header("UI References")]
        public TextMeshProUGUI productNameText;
        public TextMeshProUGUI priceText;
        public TextMeshProUGUI stockText;
        public Button button;

        [Header("Visual Feedback")]
        public Color normalColor = Color.white;
        public Color hoverColor = Color.cyan;
        public Color pressedColor = Color.green;

        private ProductData productData;
        private int availableStock;

        void Start() {
            if (button == null)
                button = GetComponent<Button>();

            // Add hover effects
            var colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = hoverColor;
            colors.pressedColor = pressedColor;
            button.colors = colors;
        }

        public void SetupButton(ProductData product, int stock, System.Action<ProductData> onClickCallback) {
            productData = product;
            availableStock = stock;

            // Update text displays
            if (productNameText != null)
                productNameText.text = product.productName;

            if (priceText != null)
                priceText.text = $"${product.sellPrice:F2}";

            if (stockText != null)
                stockText.text = $"Stock: {stock}";

            // Set up button click
            if (button != null) {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => {
                    onClickCallback?.Invoke(product);
                    PlayClickSound();
                });
            }

            // Disable button if out of stock
            if (button != null) {
                button.interactable = stock > 0;
            }
        }

        public void UpdateStock(int newStock) {
            availableStock = newStock;

            if (stockText != null)
                stockText.text = $"Stock: {newStock}";

            if (button != null)
                button.interactable = newStock > 0;
        }

        private void PlayClickSound() {
            // Optional: Add click sound effect
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource != null && audioSource.clip != null) {
                audioSource.Play();
            }
        }
    }
}