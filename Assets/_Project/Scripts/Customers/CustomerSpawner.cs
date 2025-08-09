using UnityEngine;

namespace DispensarySimulator.Customers {
    public class CustomerSpawner : MonoBehaviour {
        [Header("Spawning Settings")]
        public GameObject[] customerPrefabs;
        public Transform[] spawnPoints;
        public float spawnInterval = 30f;
        public int maxCustomers = 5;

        [Header("Customer Behavior")]
        public float customerLifetime = 120f;
        public bool spawnCustomers = false; // Disabled by default

        // Current state
        private float spawnTimer = 0f;
        private int currentCustomerCount = 0;

        void Start() {
            if (spawnCustomers) {
                Debug.Log("Customer Spawner initialized");
            }
            else {
                Debug.Log("Customer Spawner created but disabled - enable when ready for customers");
            }
        }

        void Update() {
            if (!spawnCustomers) return;

            spawnTimer += Time.deltaTime;

            if (spawnTimer >= spawnInterval && currentCustomerCount < maxCustomers) {
                SpawnCustomer();
                spawnTimer = 0f;
            }
        }

        private void SpawnCustomer() {
            if (customerPrefabs == null || customerPrefabs.Length == 0) {
                Debug.LogWarning("No customer prefabs assigned to spawner");
                return;
            }

            if (spawnPoints == null || spawnPoints.Length == 0) {
                Debug.LogWarning("No spawn points assigned to spawner");
                return;
            }

            // Choose random prefab and spawn point
            GameObject customerPrefab = customerPrefabs[Random.Range(0, customerPrefabs.Length)];
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

            // Spawn the customer
            GameObject newCustomer = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);
            currentCustomerCount++;

            // Set up customer lifetime
            Destroy(newCustomer, customerLifetime);

            Debug.Log($"Spawned customer at {spawnPoint.name}");
        }

        public void EnableSpawning() {
            spawnCustomers = true;
            Debug.Log("Customer spawning enabled");
        }

        public void DisableSpawning() {
            spawnCustomers = false;
            Debug.Log("Customer spawning disabled");
        }

        // Called when customer is destroyed or leaves
        public void OnCustomerLeft() {
            currentCustomerCount = Mathf.Max(0, currentCustomerCount - 1);
        }

        // Public getters
        public int GetCurrentCustomerCount() {
            return currentCustomerCount;
        }

        public bool IsSpawningEnabled() {
            return spawnCustomers;
        }
    }
}