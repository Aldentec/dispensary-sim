using UnityEngine;
using System;
using DispensarySimulator.Economy;
using DispensarySimulator.Store;
using DispensarySimulator.Customers;

namespace DispensarySimulator.Core {
    public class GameManager : MonoBehaviour {
        [Header("UI References")]
        public PauseMenuUI pauseMenuUI;

        [Header("Game Settings")]
        public bool debugMode = true;

        [Header("Game State")]
        public GameState currentState = GameState.MainMenu;

        // Singleton pattern for easy access
        public static GameManager Instance { get; private set; }

        // Events for other systems to subscribe to
        public static event Action<GameState> OnGameStateChanged;
        public static event Action OnGamePaused;
        public static event Action OnGameResumed;

        // Game timing
        [Header("Time Management")]
        public float gameTimeScale = 1f;
        public bool isPaused = false;

        // References to major systems
        [Header("System References")]
        public MoneyManager moneyManager;
        public StoreManager storeManager;
        public CustomerSpawner customerSpawner; // Optional - can be left unassigned for now

        private void Awake() {
            // Implement singleton pattern
            if (Instance == null) {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeGame();
            }
            else {
                Destroy(gameObject);
            }
        }

        private void Start() {
            ChangeGameState(GameState.Playing);
        }

        private void Update() {
            HandleInput();
        }

        private void InitializeGame() {
            // Set up initial game settings
            Application.targetFrameRate = 60;
            Time.timeScale = gameTimeScale;

            if (debugMode) {
                Debug.Log("Game Manager Initialized - Debug Mode On");
            }
        }

        private void HandleInput() {
            // Pause with P instead of Escape, and only if not in UI
            if (Input.GetKeyDown(KeyCode.P)) {
                var fpc = FindObjectOfType<DispensarySimulator.Player.FirstPersonController>();
                if (fpc != null && fpc.IsInUIMode()) return;

                if (currentState == GameState.Playing) PauseGame();
                else if (currentState == GameState.Paused) ResumeGame();
            }

            // Debug add money
            if (debugMode && Input.GetKeyDown(KeyCode.R)) {
                if (moneyManager != null) {
                    moneyManager.AddMoney(1000f);
                    Debug.Log("Added $1000 for testing");
                }
            }
        }


        public void ChangeGameState(GameState newState) {
            if (currentState == newState) return;

            GameState previousState = currentState;
            currentState = newState;

            if (debugMode) {
                Debug.Log($"Game State changed from {previousState} to {newState}");
            }

            OnGameStateChanged?.Invoke(newState);
        }

        private void EnsurePauseUI() {
            if (pauseMenuUI == null)
                pauseMenuUI = FindObjectOfType<PauseMenuUI>(true); // includeInactive = true
        }

        public void PauseGame() {
            if (isPaused) return;

            isPaused = true;
            Time.timeScale = 0f;
            ChangeGameState(GameState.Paused);

            EnsurePauseUI();
            if (pauseMenuUI != null) pauseMenuUI.Show();

            OnGamePaused?.Invoke();
            if (debugMode) Debug.Log("Game Paused");
        }

        public void ResumeGame() {
            if (!isPaused) return;

            isPaused = false;
            Time.timeScale = gameTimeScale;
            ChangeGameState(GameState.Playing);

            EnsurePauseUI();
            if (pauseMenuUI != null) pauseMenuUI.Hide();

            OnGameResumed?.Invoke();
            if (debugMode) Debug.Log("Game Resumed");
        }


        public void QuitGame() {
            if (debugMode) {
                Debug.Log("Quitting Game");
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Method for future save system integration
        public void SaveGame() {
            if (debugMode) {
                Debug.Log("Saving Game... (Not yet implemented)");
            }
            // TODO: Implement save system
        }

        public void LoadGame() {
            if (debugMode) {
                Debug.Log("Loading Game... (Not yet implemented)");
            }
            // TODO: Implement load system
        }
    }

    [System.Serializable]
    public enum GameState {
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Loading
    }
}