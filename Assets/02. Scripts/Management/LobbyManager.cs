using System;
using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

/// <summary>
/// 로비 UI·API. Fusion 튜토리얼 흐름: Host StartGame(생성) → Client StartGame(코드 참가) → Host LoadScene(인게임).
/// </summary>
public class LobbyManager : SceneManagerBase
{
    [Header("Session")]
    [SerializeField] private string localDisplayName = "Player";
    [SerializeField] private int defaultMaxPlayers = 5;
    [SerializeField] private int minPlayersToStart = 1;
    [SerializeField] private bool requireAllReady = true;

    NetworkManager Network => App.SystemManager.Network;

    public LobbySession Session => Network.Session;
    public ELobbyState State => Session.State;
    public bool IsInLobby => State == ELobbyState.InLobby;
    public bool IsHost => Session.IsHost;
    public string SessionCode => Session.SessionCode;
    public IReadOnlyList<LobbyPlayer> Players => Session.Players;
    public string LocalDisplayName => localDisplayName;
    public bool IsLocalReady => Network.IsLocalReady;

    /// <summary>빌드에서는 requireAllReady, 에디터에서는 준비 체크 생략.</summary>
    public bool RequiresAllPlayersReady
    {
        get
        {
#if UNITY_EDITOR
            return false;
#else
            return requireAllReady;
#endif
        }
    }

    public event Action<string> OnLobbyError;

    protected override void Awake()
    {
        base.Awake();

        PlayerProfile.Load();
        if (!string.IsNullOrWhiteSpace(PlayerProfile.DisplayName))
        {
            localDisplayName = PlayerProfile.DisplayName;
        }

        Network.OnSessionUpdated += HandleLobbySessionUpdated;
        Network.OnError += HandleLobbyError;
    }

    void Start()
    {
        Network.Initialize(localDisplayName, defaultMaxPlayers, minPlayersToStart, RequiresAllPlayersReady);
    }

    void OnDestroy()
    {
        Network.OnSessionUpdated -= HandleLobbySessionUpdated;
        Network.OnError -= HandleLobbyError;
    }

    void OnApplicationQuit()
    {
        Network.Shutdown();
    }

    public void CreateSession(string sessionCode = null, Action<LobbyRequestResult> onComplete = null)
    {
        if (IsInLobby)
        {
            Complete(LobbyRequestResult.Fail("이미 로비에 있습니다."), onComplete);
            return;
        }

        Network.CreateSession(sessionCode, defaultMaxPlayers, result => Complete(result, onComplete));
    }

    public void JoinSession(string sessionCode, Action<LobbyRequestResult> onComplete = null)
    {
        if (IsInLobby)
        {
            Complete(LobbyRequestResult.Fail("이미 로비에 있습니다."), onComplete);
            return;
        }

        Network.JoinSession(sessionCode, result => Complete(result, onComplete));
    }

    public void QuickJoinSession(Action<LobbyRequestResult> onComplete = null)
    {
        if (IsInLobby)
        {
            Complete(LobbyRequestResult.Fail("이미 로비에 있습니다."), onComplete);
            return;
        }

        Network.QuickJoinSession(result => Complete(result, onComplete));
    }

    public void LeaveSession()
    {
        Network.LeaveSession();
    }

    public void SetLocalDisplayName(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            localDisplayName = displayName.Trim();
        }

        Network.SetLocalDisplayName(localDisplayName);
    }

    public void SetLocalReady(bool isReady)
    {
        Network.SetLocalReady(isReady);
    }

    public void StartGame(EScene targetScene, Action<LobbyRequestResult> onComplete = null)
    {
        if (!IsHost)
        {
            Complete(LobbyRequestResult.Fail("호스트만 게임을 시작할 수 있습니다."), onComplete);
            return;
        }

        if (!CanStartGame())
        {
            Complete(LobbyRequestResult.Fail("플레이어 수 또는 준비 상태가 부족합니다."), onComplete);
            return;
        }

        Network.StartGame(targetScene, result => Complete(result, onComplete));
    }

    public bool CanStartGame()
    {
        return Network.CanStartGame();
    }

    void HandleLobbySessionUpdated(LobbySession session)
    {
        RefreshLobbyUI();
    }

    public void RefreshLobbyUI()
    {
        if (App.UI.Lobby == null)
        {
            return;
        }

        if (IsInLobby)
        {
            App.UI.Lobby.ClosePanel<LobbyEntryPanel>();
            App.UI.Lobby.OpenPanel<LobbyRoomPanel>();

            if (App.UI.Lobby.TryGetPanel(out LobbyRoomPanel roomPanel))
            {
                roomPanel.RefreshContent();
            }
        }
        else
        {
            App.UI.Lobby.ClosePanel<LobbyRoomPanel>();
            App.UI.Lobby.OpenPanel<LobbyEntryPanel>();
        }
    }

    void HandleLobbyError(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            Debug.LogError($"[LobbyManager] {message}");
        }

        OnLobbyError?.Invoke(message);
    }

    static void Complete(LobbyRequestResult result, Action<LobbyRequestResult> onComplete)
    {
        if (!result.IsSuccess && !string.IsNullOrEmpty(result.ErrorMessage))
        {
            if (result.ErrorMessage == NetworkManager.SessionNotFoundMessage)
            {
                Debug.Log($"[LobbyManager] {result.ErrorMessage}");
            }
            else
            {
                Debug.LogError($"[LobbyManager] {result.ErrorMessage}");
            }
        }

        onComplete?.Invoke(result);
    }
}
