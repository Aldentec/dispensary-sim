using UnityEngine;
using Unity.Netcode;
using System;

namespace DispensarySimulator.Economy {
    public class MoneyManager : NetworkBehaviour {
        [Header("Starting Money")]
        public float startingMoney = 500f;

        [Header("Daily Goals")]
        public float startingDailyTarget = 1000f;

        // Network variables that sync across all clients
        private NetworkVariable<float> currentMoney = new NetworkVariable<float>();
        private NetworkVariable<float> currentDailyEarnings = new NetworkVariable<float>();
        private NetworkVariable<float> dailyTarget = new NetworkVariable<float>();

        // Events for UI updates (same as before)
        public static event Action<float> OnMoneyChanged;
        public static event Action<float> OnDailyEarningsChanged;
        public static event Action OnDailyTargetReached;

        // Properties (same as before)
        public float CurrentMoney => currentMoney.Value;
        public float CurrentDailyEarnings => currentDailyEarnings.Value;
        public float DailyTarget => dailyTarget.Value;
        public float DailyProgress => currentDailyEarnings.Value / dailyTarget.Value;

        void Start() {
            // Initialize for single-player mode if not networked
            if (!NetworkManager.Singleton.IsListening) {
                InitializeSinglePlayer();
            }
        }

        public override void OnNetworkSpawn() {
            // Subscribe to network variable changes for all clients
            currentMoney.OnValueChanged += OnMoneyValueChanged;
            currentDailyEarnings.OnValueChanged += OnDailyEarningsValueChanged;
            dailyTarget.OnValueChanged += OnDailyTargetValueChanged;

            // Initialize values on server
            if (IsServer) {
                InitializeNetworkValues();
            }

            // Fire initial events for UI
            OnMoneyChanged?.Invoke(currentMoney.Value);
            OnDailyEarningsChanged?.Invoke(currentDailyEarnings.Value);

            Debug.Log($"💰 Network Money Manager spawned. Balance: ${currentMoney.Value:F2}");
        }

        private void InitializeSinglePlayer() {
            // For single-player mode, set values directly
            Debug.Log("💰 Initializing single-player money manager");
            currentMoney = new NetworkVariable<float>(startingMoney);
            currentDailyEarnings = new NetworkVariable<float>(0f);
            dailyTarget = new NetworkVariable<float>(startingDailyTarget);

            OnMoneyChanged?.Invoke(currentMoney.Value);
            OnDailyEarningsChanged?.Invoke(currentDailyEarnings.Value);
        }

        private void InitializeNetworkValues() {
            currentMoney.Value = startingMoney;
            currentDailyEarnings.Value = 0f;
            dailyTarget.Value = startingDailyTarget;

            Debug.Log($"💰 Server initialized money: ${currentMoney.Value:F2}");
        }

        // Network variable change callbacks
        private void OnMoneyValueChanged(float oldValue, float newValue) {
            OnMoneyChanged?.Invoke(newValue);
            Debug.Log($"💰 Money synced: ${newValue:F2}");
        }

        private void OnDailyEarningsValueChanged(float oldValue, float newValue) {
            OnDailyEarningsChanged?.Invoke(newValue);

            // Check daily target on all clients
            if (newValue >= dailyTarget.Value && oldValue < dailyTarget.Value) {
                OnDailyTargetReached?.Invoke();
                Debug.Log("🎯 Daily target reached!");
            }
        }

        private void OnDailyTargetValueChanged(float oldValue, float newValue) {
            Debug.Log($"🎯 Daily target updated: ${newValue:F2}");
        }

        // Public methods (same interface as before)
        public bool CanAfford(float amount) {
            return currentMoney.Value >= amount;
        }

        public bool SpendMoney(float amount) {
            if (!CanAfford(amount)) {
                Debug.Log($"❌ Cannot afford ${amount:F2}. Current balance: ${currentMoney.Value:F2}");
                return false;
            }

            // Send request to server
            SpendMoneyServerRpc(amount);
            return true;
        }

        public void AddMoney(float amount) {
            AddMoneyServerRpc(amount);
        }

        public void AddSaleEarnings(float amount) {
            AddSaleEarningsServerRpc(amount);
        }

        public void ResetDailyEarnings() {
            ResetDailyEarningsServerRpc();
        }

        public void SetDailyTarget(float newTarget) {
            SetDailyTargetServerRpc(newTarget);
        }

        // Server RPCs - only server can modify money
        [ServerRpc(RequireOwnership = false)]
        private void SpendMoneyServerRpc(float amount) {
            if (!IsServer) return;

            if (currentMoney.Value >= amount) {
                currentMoney.Value -= amount;
                Debug.Log($"💸 Server: Spent ${amount:F2}. New balance: ${currentMoney.Value:F2}");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void AddMoneyServerRpc(float amount) {
            if (!IsServer) return;

            currentMoney.Value += amount;
            Debug.Log($"💵 Server: Added ${amount:F2}. New balance: ${currentMoney.Value:F2}");
        }

        [ServerRpc(RequireOwnership = false)]
        private void AddSaleEarningsServerRpc(float amount) {
            if (!IsServer) return;

            // Add to both current money and daily earnings
            currentMoney.Value += amount;
            currentDailyEarnings.Value += amount;

            Debug.Log($"💰 Server: Sale complete! Earned ${amount:F2}. Daily: ${currentDailyEarnings.Value:F2}");
        }

        [ServerRpc(RequireOwnership = false)]
        private void ResetDailyEarningsServerRpc() {
            if (!IsServer) return;

            currentDailyEarnings.Value = 0f;
            Debug.Log("🌅 Server: Daily earnings reset for new day");
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetDailyTargetServerRpc(float newTarget) {
            if (!IsServer) return;

            dailyTarget.Value = newTarget;
            Debug.Log($"🎯 Server: Daily target set to ${newTarget:F2}");
        }

        // Formatting methods (same as before)
        public string GetFormattedMoney() {
            return $"${currentMoney.Value:F2}";
        }

        public string GetFormattedDailyEarnings() {
            return $"${currentDailyEarnings.Value:F2}";
        }

        public string GetFormattedDailyTarget() {
            return $"${dailyTarget.Value:F2}";
        }

        // Save/Load methods (updated for network variables)
        [System.Serializable]
        public class MoneyData {
            public float currentMoney;
            public float currentDailyEarnings;
            public float dailyTarget;
        }

        public MoneyData GetSaveData() {
            return new MoneyData {
                currentMoney = this.currentMoney.Value,
                currentDailyEarnings = this.currentDailyEarnings.Value,
                dailyTarget = this.dailyTarget.Value
            };
        }

        [ServerRpc(RequireOwnership = false)]
        public void LoadSaveDataServerRpc(float money, float earnings, float target) {
            if (!IsServer) return;

            currentMoney.Value = money;
            currentDailyEarnings.Value = earnings;
            dailyTarget.Value = target;

            Debug.Log($"💾 Server: Money data loaded: ${money:F2}");
        }

        public void LoadSaveData(MoneyData data) {
            if (IsServer) {
                LoadSaveDataServerRpc(data.currentMoney, data.currentDailyEarnings, data.dailyTarget);
            }
        }

        // Cleanup
        public override void OnNetworkDespawn() {
            if (currentMoney != null) currentMoney.OnValueChanged -= OnMoneyValueChanged;
            if (currentDailyEarnings != null) currentDailyEarnings.OnValueChanged -= OnDailyEarningsValueChanged;
            if (dailyTarget != null) dailyTarget.OnValueChanged -= OnDailyTargetValueChanged;
        }
    }
}