using UnityEngine;
using UnityEngine.UI;
using DispensarySimulator.Core;

public class PauseMenuUI : MonoBehaviour {
    [Header("Root")]
    public GameObject panel;

    [Header("Buttons")]
    public Button resumeButton;
    public Button saveButton;
    public Button loadButton;
    public Button multiplayerButton;
    public Button settingsButton;
    public Button mainMenuButton;

    [Header("Other Panels")]
    public GameObject settingsPanel;

    [Header("References")]
    public GameObject multiplayerUICanvas;
    public RelayManager relayManager;

    void Awake() {
        if (panel == null) panel = gameObject;
        panel.SetActive(false);

        if (resumeButton) resumeButton.onClick.AddListener(OnResume);
        if (saveButton) saveButton.onClick.AddListener(OnSave);
        if (loadButton) loadButton.onClick.AddListener(OnLoad);
        if (multiplayerButton) multiplayerButton.onClick.AddListener(OnMultiplayer);
        if (settingsButton) settingsButton.onClick.AddListener(OnSettings);
        if (mainMenuButton) mainMenuButton.onClick.AddListener(OnMainMenu);

        GameManager.OnGamePaused += HandlePaused;
        GameManager.OnGameResumed += HandleResumed;
    }

    void OnDestroy() {
        GameManager.OnGamePaused -= HandlePaused;
        GameManager.OnGameResumed -= HandleResumed;
    }

    void HandlePaused() {
        panel.SetActive(true);

        // Set FirstPersonController to menu mode for proper cursor handling
        var fpc = FindObjectOfType<DispensarySimulator.Player.FirstPersonController>();
        if (fpc != null) {
            fpc.SetMenuMode(true);
            // Make sure inventory is closed when pause menu opens
            fpc.SetInventoryMode(false);
        }

        Debug.Log("🎮 Pause menu shown, cursor unlocked");
    }

    void HandleResumed() {
        panel.SetActive(false);

        // Set FirstPersonController back to game mode
        var fpc = FindObjectOfType<DispensarySimulator.Player.FirstPersonController>();
        if (fpc != null) {
            fpc.SetMenuMode(false);
            fpc.SetInventoryMode(false);
        }

        Debug.Log("🎮 Pause menu hidden, cursor locked");
    }

    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }

    void OnResume() { GameManager.Instance.ResumeGame(); }
    void OnSave() { GameManager.Instance.SaveGame(); }
    void OnLoad() { GameManager.Instance.LoadGame(); }

    void OnMultiplayer() {
        if (relayManager != null) relayManager.ShowUIFromPauseMenu();
        if (multiplayerUICanvas != null) multiplayerUICanvas.SetActive(true);
    }

    void OnSettings() {
        if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    void OnMainMenu() {
        if (GameManager.Instance.currentState == GameState.Paused) GameManager.Instance.ResumeGame();
        GameManager.Instance.ChangeGameState(GameState.MainMenu);
    }
}