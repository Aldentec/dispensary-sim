using UnityEngine;
using DispensarySimulator.Player;

namespace DispensarySimulator.Products {
    // ScriptableObject for product data
    [CreateAssetMenu(fileName = "New Product", menuName = "Dispensary Simulator/Product Data")]
    public class ProductData : ScriptableObject {
        [Header("Basic Info")]
        public string productName;
        public string description;
        public ProductCategory category;
        public Sprite icon;
        public GameObject prefab;

        [Header("Pricing")]
        public float basePrice = 10f;
        public float sellPrice = 15f;
        public float margin => sellPrice - basePrice;

        [Header("Stats")]
        [Range(0f, 1f)]
        public float potency = 0.5f;
        [Range(0f, 1f)]
        public float quality = 0.5f;
        public float weight = 1f; // in grams

        [Header("Business")]
        public int minStock = 5;
        public int maxStock = 50;
        public float popularityScore = 0.5f; // affects customer interest

        [Header("Display")]
        public bool canDisplayOnShelf = true;
        public bool requiresShowcase = false;
        public bool needsRefrigeration = false;

        public float GetProfitMargin() {
            return (sellPrice - basePrice) / basePrice * 100f;
        }
    }

    [System.Serializable]
    public enum ProductCategory {
        Flower,
        Edibles,
        Concentrates,
        Accessories,
        Beverages,
        Topicals,
        PreRolls
    }
}