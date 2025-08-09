using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class NetworkTest : MonoBehaviour {
    async void Start() {
        try {
            await UnityServices.InitializeAsync();
            Debug.Log("✅ Unity Services initialized!");

            if (!AuthenticationService.Instance.IsSignedIn) {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("✅ Authentication successful!");
            }
        }
        catch (System.Exception e) {
            Debug.LogError($"❌ Setup failed: {e.Message}");
        }
    }
}