using SystemEnums;
using UnityEngine;

// Dev-only: boots straight into a game scene via Fusion Single mode, skipping Title/Lobby/cloud login.
public class DevBootstrap : MonoBehaviour
{
    [SerializeField] EScene targetScene = EScene.PvE_1v1;
    [SerializeField] string displayName = "DevPlayer";

    void Start()
    {
        NetworkManager network = App.SystemManager.Network;
        if (network == null)
        {
            Debug.LogError("[DevBootstrap] NetworkManager missing — add the SystemManagement prefab to this scene.");
            return;
        }

        network.Initialize(displayName, NetworkManager.MaxPlayers, minPlayersToStart: 1, requireAllReady: false);
        network.StartOfflineGame(targetScene, OnStarted);
    }

    void OnStarted(LobbyRequestResult result)
    {
        if (!result.IsSuccess)
            Debug.LogError($"[DevBootstrap] Offline start failed: {result.ErrorMessage}");
    }
}
