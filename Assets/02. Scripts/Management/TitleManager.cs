using System;
using SystemEnums;
using UnityEngine;

public class TitleManager : SceneManagerBase
{
    [SerializeField] private int defaultMaxPlayers = 4;
    [SerializeField] private int minPlayersToStart = 1;
    [SerializeField] private bool requireAllReady = true;

    public bool IsCloudConnected => App.SystemManager.Network.IsCloudConnected;

    protected override void Awake()
    {
        base.Awake();

        PlayerProfile.Load();
    }

    public void Connect(string displayName, Action<LobbyRequestResult> onComplete = null)
    {
        NetworkManager network = App.SystemManager.Network;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            Complete(LobbyRequestResult.Fail("닉네임을 입력해 주세요."), onComplete);
            return;
        }

        string nickname = PlayerProfile.NormalizeDisplayName(displayName);
        PlayerProfile.SaveDisplayName(nickname);

        network.Initialize(nickname, defaultMaxPlayers, minPlayersToStart, requireAllReady);
        network.ConnectToCloud(result =>
        {
            if (result.IsSuccess)
            {
                MoveToNextScene(EScene.Lobby);
            }

            Complete(result, onComplete);
        });
    }

    static void Complete(LobbyRequestResult result, Action<LobbyRequestResult> onComplete)
    {
        if (!result.IsSuccess && !string.IsNullOrEmpty(result.ErrorMessage))
        {
            Debug.LogError($"[TitleManager] {result.ErrorMessage}");
        }

        onComplete?.Invoke(result);
    }
}
