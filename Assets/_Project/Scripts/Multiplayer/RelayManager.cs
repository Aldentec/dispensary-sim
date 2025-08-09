using UnityEngine;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;
using TMPro;
using DispensarySimulator.Player;
using DispensarySimulator.Core;

public class RelayManager : MonoBehaviour {
    [Header("UI References")]
    public GameObject mainMenu;
    public GameObject hostButton;
    public GameObject joinButton;
    public TextMeshProUGUI joinCodeDisplay;
    public TMP_InputField joinCodeInput;
    public TextMeshProUGUI statusText;

    private string currentJoinCode;
    private FirstPersonController playerController;

    async void Start() {
        // Find the player controller
        playerController = FindObjectOfType<FirstPersonController>();

        // Initialize Unity Services
        await UnityServices.InitializeAsync();

        // Sign in anonymously
        if (!AuthenticationService.Instance.IsSignedIn) {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        SetStatus("Ready to create or join dispensary!");
        Debug.Log("🎮 Use ESCAPE key to toggle between menu and game mode!");

        // Ensure UI starts visible for first-time setup
        SwitchToMenuMode();
    }

    public async void CreateDispensary() {
        SetStatus("Creating dispensary...");

        try {
            // Create allocation for up to 4 players
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);

            // Get join code
            currentJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Configure transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            // Start hosting
            NetworkManager.Singleton.StartHost();

            // Update UI
            joinCodeDisplay.text = $"Join Code: {currentJoinCode}";
            SetStatus($"Dispensary created! Share code: {currentJoinCode}");
            SwitchToGameMode();

            Debug.Log($"✅ Dispensary created! Join code: {currentJoinCode}");
        }
        catch (System.Exception e) {
            SetStatus($"Failed to create dispensary: {e.Message}");
            Debug.LogError($"❌ Failed to create dispensary: {e.Message}");
        }
    }

    public async void JoinDispensary() {
        string codeToJoin = joinCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(codeToJoin)) {
            SetStatus("Please enter a join code!");
            return;
        }

        SetStatus($"Joining dispensary {codeToJoin}...");

        try {
            // Join allocation
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(codeToJoin);

            // Configure transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetClientRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.HostConnectionData
            );

            // Join as client
            NetworkManager.Singleton.StartClient();

            SetStatus($"Joining dispensary...");
            SwitchToGameMode();

            Debug.Log($"✅ Joining dispensary with code: {codeToJoin}");
        }
        catch (System.Exception e) {
            SetStatus($"Failed to join: {e.Message}");
            Debug.LogError($"❌ Failed to join dispensary: {e.Message}");
        }
    }

    private void SwitchToGameMode() {
        // Hide ALL multiplayer UI when in game
        if (mainMenu != null) mainMenu.SetActive(false);

        // Switch player controller to game mode (closes both menu and inventory)
        if (playerController != null) {
            playerController.SetMenuMode(false);
            playerController.SetInventoryMode(false);
        }

        Debug.Log("🎮 Game mode: Multiplayer UI hidden, cursor locked");
    }

    private void SwitchToMenuMode() {
        // Only show multiplayer UI if we're NOT connected
        bool isConnected = NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsHost;
        bool shouldShowMultiplayerUI = !isConnected;

        if (mainMenu != null) mainMenu.SetActive(shouldShowMultiplayerUI);

        // Switch player controller to menu mode (closes inventory if open)
        if (playerController != null) {
            playerController.SetMenuMode(true);
            if (playerController.IsInInventory()) {
                playerController.SetInventoryMode(false);
            }
        }

        if (shouldShowMultiplayerUI) {
            Debug.Log("📱 Menu mode: Multiplayer UI shown, cursor unlocked");
        }
        else {
            Debug.Log("🎮 Menu mode: Connected to game, UI stays hidden");
        }
    }

    // Add disconnect functionality
    public void DisconnectFromDispensary() {
        if (NetworkManager.Singleton.IsHost) {
            NetworkManager.Singleton.Shutdown();
        }
        else if (NetworkManager.Singleton.IsClient) {
            NetworkManager.Singleton.Shutdown();
        }

        // Clear join code display
        if (joinCodeDisplay != null) joinCodeDisplay.text = "";

        SetStatus("Disconnected. Ready to create or join dispensary!");
        SwitchToMenuMode(); // Will show multiplayer UI since we're disconnected

        Debug.Log("🚪 Disconnected from dispensary");
    }

    private void SetStatus(string message) {
        if (statusText != null) statusText.text = message;
        Debug.Log(message);
    }

    // Network event handlers
    void OnEnable() {
        if (NetworkManager.Singleton != null) {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    void OnDisable() {
        if (NetworkManager.Singleton != null) {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId) {
        SetStatus($"Player {clientId} joined the dispensary!");
    }

    private void OnClientDisconnected(ulong clientId) {
        SetStatus($"Player {clientId} left the dispensary.");

        // If we're disconnected, return to menu
        if (!NetworkManager.Singleton.IsConnectedClient && !NetworkManager.Singleton.IsHost) {
            SwitchToMenuMode();
            SetStatus("Disconnected. Ready to create or join dispensary!");
        }
    }

    // Update method to handle escape key and auto-hide UI
    void Update() {
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;
        // Handle escape key to toggle between menu and game
        if (Input.GetKeyDown(KeyCode.Escape) && playerController != null) {
            if (playerController.IsInUIMode()) {
                // If in any UI mode and connected, switch to game mode
                if (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsHost) {
                    SwitchToGameMode();
                }
            }
            else {
                // If in game, switch to menu mode
                SwitchToMenuMode();
            }
        }

        // Auto-manage UI visibility based on connection state
        if (mainMenu != null && playerController != null) {
            bool isConnected = NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsHost;
            bool inMenuMode = playerController.IsInMenu(); // Specifically menu, not inventory
            bool shouldShowUI = inMenuMode && !isConnected;

            // Only update if state changed (prevents constant SetActive calls)
            if (mainMenu.activeSelf != shouldShowUI) {
                mainMenu.SetActive(shouldShowUI);
            }
        }

        // Debug key for quick disconnect (optional)
        if (Input.GetKeyDown(KeyCode.F1)) {
            DisconnectFromDispensary();
        }
    }

    public void ShowUIFromPauseMenu() {
        if (mainMenu != null) mainMenu.SetActive(true);
        if (playerController != null) playerController.SetMenuMode(true);
    }
}