using System;
using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

/// <summary>
/// 로비 API. 세션 코드로만 방 생성·참가·시작을 담당합니다.
/// </summary>
public class LobbyManager : SceneManagerBase
{
    [Header("Session")]
    [SerializeField] private string localDisplayName = "Player";
    [SerializeField] private int defaultMaxPlayers = 4;
    [SerializeField] private int minPlayersToStart = 1;
    [SerializeField] private bool requireAllReady = true;

    LobbySession _session = new();

    NetworkManager Network => App.Game.Network;

    public LobbySession Session => _session;
    public ELobbyState State => _session.State;
    public bool IsInLobby => State == ELobbyState.InLobby;
    public bool IsHost => _session.IsHost;
    public string SessionCode => _session.SessionCode;
    public IReadOnlyList<LobbyPlayer> Players => _session.Players;
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

    public event Action<LobbySession> OnSessionUpdated;
    public event Action OnPlayersChanged;
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
        Network.OnPlayersChanged += HandleLobbyPlayersChanged;
        Network.OnError += HandleLobbyError;
    }

    void Start()
    {
        Network.Initialize(localDisplayName, defaultMaxPlayers, minPlayersToStart, RequiresAllPlayersReady);
    }

    void OnDestroy()
    {
        Network.OnSessionUpdated -= HandleLobbySessionUpdated;
        Network.OnPlayersChanged -= HandleLobbyPlayersChanged;
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

    public bool TrySetPlayerReady(string playerId, bool isReady)
    {
        return Network.TrySetPlayerReady(playerId, isReady);
    }

    public void StartGame(Action<LobbyRequestResult> onComplete = null)
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

        Network.StartGame(result => Complete(result, onComplete));
    }

    public bool CanStartGame()
    {
        return Network.CanStartGame();
    }

    void HandleLobbySessionUpdated(LobbySession session)
    {
        _session = session;
        OnSessionUpdated?.Invoke(_session);
        RefreshLobbyUI();
    }

    void HandleLobbyPlayersChanged()
    {
        OnPlayersChanged?.Invoke();

        if (!IsInLobby || App.UI.Lobby == null)
        {
            return;
        }

        if (App.UI.Lobby.TryGetPanel(out LobbyRoomPanel roomPanel))
        {
            roomPanel.RefreshContent();
        }
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
