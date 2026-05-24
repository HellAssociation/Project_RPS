using UnityEngine;
using SystemEnums;

[DefaultExecutionOrder((int)EExecutionOrder.SystemManagement)]
public class LobbyUIManager : UIManagerBase
{
    void Start()
    {
        App.SceneManager.Lobby.RefreshLobbyUI();
    }
}
