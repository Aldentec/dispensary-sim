using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using DispensarySimulator.Products;

namespace DispensarySimulator.Store {
    public class ProductSpawnPoint : NetworkBehaviour {
        [Header("Spawn Settings")]
        public Transform spawnTransform;
        public float spawnRadius = 2f;
        public int maxProducts = 20;

        [Header("Physics Settings")]
        public LayerMask groundLayers = 1; // Default layer
        public float raycastDistance = 10f;
        public float spawnHeight = 1f;

        [Header("Visual Feedback")]
        public GameObject spawnEffect;
        public AudioClip spawnSound;

        private List<SpawnedProduct> spawnedProducts = new List<SpawnedProduct>();
        private AudioSource audioSource;

        void Start() {
            if (spawnTransform == null) {
                spawnTransform = transform;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Validate spawn point position
            Debug.Log($"🎯 ProductSpawnPoint initialized at position: {spawnTransform.position}");
        }

        [ServerRpc(RequireOwnership = false)]
        public void SpawnProductsServerRpc(int productIndex, int quantity) {
            if (!IsServer) return;

            // Get product data from StoreManager
            var storeManager = FindObjectOfType<StoreManager>();
            if (storeManager == null || productIndex >= storeManager.availableProducts.Length) {
                Debug.LogError("Invalid product index or no StoreManager found!");
                return;
            }

            var productData = storeManager.availableProducts[productIndex];

            for (int i = 0; i < quantity; i++) {
                SpawnSingleProduct(productData);
            }
        }

        private void SpawnSingleProduct(ProductData productData) {
            if (spawnedProducts.Count >= maxProducts) {
                Debug.LogWarning("Spawn point at max capacity!");
                return;
            }

            Vector3 spawnPos = GetRandomSpawnPosition();

            // DEBUG: Extensive logging
            Debug.Log($"📦 Attempting to spawn {productData.productName} at {spawnPos}");
            Debug.Log($"📦 Ground check: raycast from {spawnPos + Vector3.up * 5f} down {raycastDistance}m");

            // Visualize spawn position for debugging
            Debug.DrawRay(spawnPos, Vector3.down * 2f, Color.red, 10f); // Red line down from spawn point
            Debug.DrawRay(spawnPos, Vector3.up * 2f, Color.green, 10f); // Green line up from spawn point

            // Instantiate the product prefab
            GameObject productObj = Instantiate(productData.prefab, spawnPos, Quaternion.identity);

            // Add SpawnedProduct component for networking
            SpawnedProduct spawnedProduct = productObj.GetComponent<SpawnedProduct>();
            if (spawnedProduct == null) {
                spawnedProduct = productObj.AddComponent<SpawnedProduct>();
            }

            // Initialize the spawned product
            spawnedProduct.Initialize(productData, this);

            // Spawn it on the network
            NetworkObject netObj = productObj.GetComponent<NetworkObject>();
            if (netObj == null) {
                netObj = productObj.AddComponent<NetworkObject>();
            }
            netObj.Spawn();

            spawnedProducts.Add(spawnedProduct);

            // Visual/Audio feedback
            PlaySpawnEffects();

            // Additional debug info
            var rb = productObj.GetComponent<Rigidbody>();
            var col = productObj.GetComponent<Collider>();
            Debug.Log($"📦 Spawned {productData.productName}:");
            Debug.Log($"   Position: {productObj.transform.position}");
            Debug.Log($"   Rigidbody: {(rb != null ? $"Mass={rb.mass}, Kinematic={rb.isKinematic}, UseGravity={rb.useGravity}" : "MISSING")}");
            Debug.Log($"   Collider: {(col != null ? $"IsTrigger={col.isTrigger}, Enabled={col.enabled}" : "MISSING")}");
        }

        private Vector3 GetRandomSpawnPosition() {
            Vector3 basePos = spawnTransform.position;
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 targetPos = basePos + new Vector3(randomCircle.x, 0, randomCircle.y);

            // Start raycast from higher up to ensure we don't start inside anything
            Vector3 rayStart = targetPos + Vector3.up * 5f;

            Debug.Log($"🔍 Ground raycast from {rayStart} down {raycastDistance}m on layers {groundLayers.value}");

            // Raycast down to find ground level
            RaycastHit hit;
            if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDistance, groundLayers)) {
                Vector3 groundPos = hit.point + Vector3.up * spawnHeight;
                Debug.Log($"✅ Ground found at {hit.point}, spawning at {groundPos}");
                Debug.Log($"   Hit object: {hit.collider.name}, layer: {hit.collider.gameObject.layer}");

                // Draw debug ray showing successful hit
                Debug.DrawLine(rayStart, hit.point, Color.cyan, 10f);
                return groundPos;
            }
            else {
                // Fallback: spawn at spawn point height
                Vector3 fallbackPos = targetPos + Vector3.up * spawnHeight;
                Debug.LogWarning($"❌ No ground found! Using fallback position: {fallbackPos}");
                Debug.LogWarning($"   Raycast layers: {groundLayers.value}, Distance: {raycastDistance}");

                // Draw debug ray showing failed raycast
                Debug.DrawRay(rayStart, Vector3.down * raycastDistance, Color.red, 10f);
                return fallbackPos;
            }
        }

        private void PlaySpawnEffects() {
            // Visual effect
            if (spawnEffect != null) {
                Instantiate(spawnEffect, spawnTransform.position, Quaternion.identity);
            }

            // Sound effect
            if (audioSource != null && spawnSound != null) {
                audioSource.PlayOneShot(spawnSound);
            }
        }

        public void RemoveProduct(SpawnedProduct product) {
            spawnedProducts.Remove(product);
        }

        // Debug visualization
        void OnDrawGizmosSelected() {
            if (spawnTransform != null) {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(spawnTransform.position, spawnRadius);
                Gizmos.DrawWireCube(spawnTransform.position, Vector3.one * 0.5f);

                // Show raycast range
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(spawnTransform.position + Vector3.up * 5f,
                               spawnTransform.position + Vector3.down * (raycastDistance - 5f));
            }
        }

        // Debug method to test spawning manually
        [ContextMenu("Test Spawn Position")]
        void TestSpawnPosition() {
            Vector3 testPos = GetRandomSpawnPosition();
            Debug.Log($"Test spawn position: {testPos}");
        }
    }
}