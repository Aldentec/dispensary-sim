using UnityEngine;
using TMPro;
using DispensarySimulator.Economy;

namespace DispensarySimulator.UI {
    public class MoneyUI : MonoBehaviour {
        [Header("UI References")]
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI dailyEarningsText; // Optional - add another TMP if you want daily earnings display

        private MoneyManager moneyManager;

        void Start() {
            // Find the money manager in the scene
            moneyManager = FindObjectOfType<MoneyManager>();

            if (moneyManager == null) {
                Debug.LogError("MoneyUI: No MoneyManager found in scene!");
                return;
            }

            // Subscribe to money change events
            MoneyManager.OnMoneyChanged += UpdateMoneyDisplay;
            MoneyManager.OnDailyEarningsChanged += UpdateDailyEarningsDisplay;

            // Initialize display
            UpdateMoneyDisplay(moneyManager.CurrentMoney);
            UpdateDailyEarningsDisplay(moneyManager.CurrentDailyEarnings);
        }

        void OnDestroy() {
            // Unsubscribe from events to prevent memory leaks
            MoneyManager.OnMoneyChanged -= UpdateMoneyDisplay;
            MoneyManager.OnDailyEarningsChanged -= UpdateDailyEarningsDisplay;
        }

        private void UpdateMoneyDisplay(float newAmount) {
            if (moneyText != null) {
                moneyText.text = $"${newAmount:F2}";
            }
        }

        private void UpdateDailyEarningsDisplay(float dailyEarnings) {
            if (dailyEarningsText != null) {
                dailyEarningsText.text = $"Daily: ${dailyEarnings:F2}";
            }
        }
    }
}