using UnityEngine;
using Unity.Netcode;
using System.Collections;
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
        public void SpawnProductsServerRpc(int productIndex, int quantity, ServerRpcParams rpcParams = default) {
            if (!IsServer) return;

            // Log which client requested the spawn
            ulong clientId = rpcParams.Receive.SenderClientId;
            Debug.Log($"🌐 SpawnProductsServerRpc called by client {clientId} for product index {productIndex}, quantity {quantity}");

            // Get product data from StoreManager
            var storeManager = FindObjectOfType<StoreManager>();
            if (storeManager == null || productIndex >= storeManager.availableProducts.Length) {
                Debug.LogError("Invalid product index or no StoreManager found!");
                return;
            }

            var productData = storeManager.availableProducts[productIndex];

            Debug.Log($"📦 Server spawning {quantity}x {productData.productName} requested by client {clientId}");

            for (int i = 0; i < quantity; i++) {
                SpawnSingleProduct(productData, clientId);
            }
        }

        private void SpawnSingleProduct(ProductData productData, ulong requestingClientId) {
            if (spawnedProducts.Count >= maxProducts) {
                Debug.LogWarning("Spawn point at max capacity!");
                return;
            }

            Vector3 spawnPos = GetRandomSpawnPosition();

            Debug.Log($"📦 SERVER: Spawning {productData.productName} at {spawnPos} (requested by client {requestingClientId})");

            // Instantiate the product prefab
            GameObject productObj = Instantiate(productData.prefab, spawnPos, Quaternion.identity);

            // Ensure components exist
            SpawnedProduct spawnedProduct = productObj.GetComponent<SpawnedProduct>();
            if (spawnedProduct == null) {
                Debug.LogWarning($"Adding missing SpawnedProduct component to {productData.productName}");
                spawnedProduct = productObj.AddComponent<SpawnedProduct>();
            }

            NetworkObject netObj = productObj.GetComponent<NetworkObject>();
            if (netObj == null) {
                Debug.LogError($"❌ CRITICAL: {productData.productName} prefab missing NetworkObject component!");
                Destroy(productObj);
                return;
            }

            // Initialize the spawned product BEFORE spawning
            spawnedProduct.Initialize(productData, this);

            // CRITICAL: Ensure proper setup before network spawn
            var collider = productObj.GetComponent<Collider>();
            if (collider != null) {
                collider.enabled = true;
                collider.isTrigger = false; // Must NOT be trigger for interaction
            }

            // Ensure correct layer for interaction
            productObj.layer = 0; // Default layer

            Debug.Log($"🌐 SERVER: About to spawn NetworkObject for {productData.productName}");
            Debug.Log($"🌐 SERVER: Pre-spawn setup - Layer: {productObj.layer}, Collider: {(collider != null ? $"enabled={collider.enabled}, trigger={collider.isTrigger}" : "missing")}");

            // Spawn the object - this should sync to ALL clients automatically
            netObj.Spawn(destroyWithScene: true);

            // Verify spawn success
            if (netObj.IsSpawned) {
                Debug.Log($"✅ SERVER: Successfully spawned {productData.productName} with NetworkID {netObj.NetworkObjectId}");
                Debug.Log($"✅ SERVER: Object is now visible to all {NetworkManager.Singleton.ConnectedClients.Count} clients");

                // Notify all clients about the new spawn with explicit setup
                NotifyClientsOfNewSpawnClientRpc(netObj.NetworkObjectId, spawnPos, productData.productName);
            }
            else {
                Debug.LogError($"❌ SERVER: FAILED to spawn {productData.productName}!");
                Destroy(productObj);
                return;
            }

            spawnedProducts.Add(spawnedProduct);
            PlaySpawnEffects();

            // Debug component state
            var rb = productObj.GetComponent<Rigidbody>();
            Debug.Log($"📦 SERVER: Spawned {productData.productName} details:");
            Debug.Log($"   Position: {productObj.transform.position}");
            Debug.Log($"   NetworkID: {netObj.NetworkObjectId}");
            Debug.Log($"   IsSpawned: {netObj.IsSpawned}");
            Debug.Log($"   Layer: {productObj.layer}");
            Debug.Log($"   Collider: {(collider != null ? $"enabled={collider.enabled}, trigger={collider.isTrigger}" : "missing")}");
            Debug.Log($"   Requesting Client: {requestingClientId}");
        }

        [ClientRpc]
        private void NotifyClientsOfNewSpawnClientRpc(ulong networkObjectId, Vector3 position, string productName) {
            Debug.Log($"🔄 CLIENT {NetworkManager.Singleton.LocalClientId}: NotifyClientsOfNewSpawnClientRpc for {productName} (NetworkID: {networkObjectId})");

            // Wait a frame to ensure the NetworkObject is fully spawned
            StartCoroutine(ValidateSpawnedObjectCoroutine(networkObjectId, productName));
        }

        private System.Collections.IEnumerator ValidateSpawnedObjectCoroutine(ulong networkObjectId, string productName) {
            yield return null; // Wait one frame

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj)) {
                Debug.Log($"✅ CLIENT {NetworkManager.Singleton.LocalClientId}: Found spawned {productName} with NetworkID {networkObjectId}");

                var spawnedProduct = netObj.GetComponent<SpawnedProduct>();
                var collider = netObj.GetComponent<Collider>();

                if (spawnedProduct != null) {
                    Debug.Log($"✅ CLIENT {NetworkManager.Singleton.LocalClientId}: SpawnedProduct component exists, CanInteract: {spawnedProduct.CanInteract()}");
                }

                if (collider != null) {
                    Debug.Log($"✅ CLIENT {NetworkManager.Singleton.LocalClientId}: Collider setup - enabled: {collider.enabled}, trigger: {collider.isTrigger}, layer: {netObj.gameObject.layer}");
                }
            }
            else {
                Debug.LogWarning($"⚠️ CLIENT {NetworkManager.Singleton.LocalClientId}: Could not find NetworkObject {networkObjectId} for {productName}");
            }
        }

        private Vector3 GetRandomSpawnPosition() {
            // Generate random position within spawn radius
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 randomPos = spawnTransform.position + new Vector3(randomCircle.x, spawnHeight * 2f, randomCircle.y);

            // Raycast down to find ground
            Vector3 rayStart = randomPos + Vector3.up * 5f;
            Debug.Log($"🔍 Ground raycast from {rayStart} down {raycastDistance}m on layers {groundLayers.value}");

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastDistance, groundLayers)) {
                Vector3 groundPos = hit.point + Vector3.up * spawnHeight;
                Debug.Log($"✅ Ground found at {hit.point}, spawning at {groundPos}");
                Debug.DrawRay(rayStart, Vector3.down * hit.distance, Color.green, 10f);
                return groundPos;
            }
            else {
                Vector3 fallbackPos = spawnTransform.position + Vector3.up * spawnHeight;
                Debug.LogWarning($"❌ No ground found! Using fallback: {fallbackPos}");
                Debug.DrawRay(rayStart, Vector3.down * raycastDistance, Color.red, 10f);
                return fallbackPos;
            }
        }

        private void PlaySpawnEffects() {
            if (spawnEffect != null) {
                Instantiate(spawnEffect, spawnTransform.position, Quaternion.identity);
            }

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

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(spawnTransform.position + Vector3.up * 5f,
                               spawnTransform.position + Vector3.down * (raycastDistance - 5f));
            }
        }

        [ContextMenu("Test Spawn Position")]
        void TestSpawnPosition() {
            Vector3 testPos = GetRandomSpawnPosition();
            Debug.Log($"Test spawn position: {testPos}");
        }

        // DEBUG: Manual spawn test
        [ContextMenu("DEBUG: Test Spawn First Product")]
        void DebugSpawnFirstProduct() {
            if (!IsServer) {
                Debug.LogWarning("Can only spawn on server!");
                return;
            }

            var storeManager = FindObjectOfType<StoreManager>();
            if (storeManager != null && storeManager.availableProducts.Length > 0) {
                Debug.Log("🧪 DEBUG: Manual spawn test");
                SpawnSingleProduct(storeManager.availableProducts[0], 999); // Use fake client ID
            }
        }
    }
}