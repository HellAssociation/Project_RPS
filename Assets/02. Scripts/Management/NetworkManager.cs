using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using SystemEnums;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Photon Fusion 세션 흐름(튜토리얼 스타일).
/// 1) Host <see cref="StartGame"/> + <see cref="SessionName"/> — 방 생성
/// 2) Client <see cref="StartGame"/> + 동일 <see cref="SessionName"/> — 방 참가
/// 3) Host <see cref="NetworkRunner.LoadScene"/> — 세션 플레이어 전원 인게임 씬 이동
///
/// 인게임 RPS 동기화:
/// 손가락 배정/입력 상태는 <see cref="PlayerNetworkObject"/>의 [Networked] 값으로 동기화하고,
/// 라운드 시작/판정 결과 신호만 ReliableData(<see cref="InGameRpsKey"/>)로 브로드캐스트합니다.
/// </summary>
[DefaultExecutionOrder((int)EExecutionOrder.GameManagement)]
public class NetworkManager : CommonManagerBase, INetworkRunnerCallbacks
{
    public const int MaxPlayers = 5;
    public const string SessionNotFoundMessage = "해당하는 대기실이 없습니다.";

    [SerializeField] NetworkRunner _runner;
    LobbySession _session = new();

    string _localDisplayName = "Player";
    int _defaultMaxPlayers = 5;
    int _minPlayersToStart = 1;
    bool _requireAllReady = true;
    bool _isRestoringCloudLobby;
    string _lastCloudConnectError = "클라우드 로비에 연결할 수 없습니다.";

    public NetworkRunner Runner => TryGetAliveRunner(out NetworkRunner runner) ? runner : null;
    public bool IsRunning => TryGetAliveRunner(out NetworkRunner runner) && runner.IsRunning;
    public bool IsCloudConnected { get; private set; }
    public LobbySession Session => _session;
    public bool IsLocalReady => App.Game.Players.IsLocalReady;
    public string LocalDisplayName => _localDisplayName;

    public event Action<LobbySession> OnSessionUpdated;
    public event Action<string> OnError;

    protected override void Awake()
    {
        base.Awake();
        EnsureRunnerComponents();
        _runner.AddCallbacks(this);
    }

    void OnEnable()
    {
        PlayerNetworkObject.InGameDataChanged += HandleInGameDataChanged;

        if (App.Game.Players != null)
        {
            App.Game.Players.OnPlayersChanged += RefreshSessionPlayers;
        }
    }

    void OnDisable()
    {
        PlayerNetworkObject.InGameDataChanged -= HandleInGameDataChanged;

        if (App.Game.Players != null)
        {
            App.Game.Players.OnPlayersChanged -= RefreshSessionPlayers;
        }
    }

    void OnDestroy()
    {
        if (_runner != null)
        {
            _runner.RemoveCallbacks(this);
        }
    }

    public bool IsServerHost => TryGetAliveRunner(out NetworkRunner runner) && runner.IsServer;

    public void ConnectToCloud(Action<LobbyRequestResult> onComplete)
    {
        StartCoroutine(ConnectToCloudCoroutine(onComplete));
    }

    public void Initialize(string localDisplayName, int defaultMaxPlayers, int minPlayersToStart, bool requireAllReady)
    {
        if (!string.IsNullOrWhiteSpace(localDisplayName))
        {
            _localDisplayName = localDisplayName.Trim();
        }

        _defaultMaxPlayers = Mathf.Clamp(defaultMaxPlayers, 2, MaxPlayers);
        _minPlayersToStart = minPlayersToStart;
        _requireAllReady = requireAllReady;
    }

    public void CreateSession(string sessionCode, int maxPlayers, Action<LobbyRequestResult> onComplete)
    {
        string code = string.IsNullOrWhiteSpace(sessionCode)
            ? LobbyCodeUtility.GenerateCode()
            : LobbyCodeUtility.NormalizeCode(sessionCode);

        if (!LobbyCodeUtility.IsValidCode(code))
        {
            onComplete?.Invoke(LobbyRequestResult.Fail("유효하지 않은 세션 코드입니다."));
            return;
        }

        int playerCount = Mathf.Clamp(maxPlayers, 2, MaxPlayers);
        StartCoroutine(StartSessionCoroutine(GameMode.Host, code, playerCount, isHost: true, onComplete));
    }

    public void JoinSession(string sessionCode, Action<LobbyRequestResult> onComplete)
    {
        string code = LobbyCodeUtility.NormalizeCode(sessionCode);
        if (!LobbyCodeUtility.IsValidCode(code))
        {
            onComplete?.Invoke(LobbyRequestResult.Fail("유효하지 않은 세션 코드입니다."));
            return;
        }

        StartCoroutine(StartSessionCoroutine(GameMode.Client, code, 0, isHost: false, onComplete));
    }

    /// <summary>
    /// Fusion 기본 랜덤 매칭: Client + <c>SessionName == null</c> (내부 OpJoinRandomRoom).
    /// </summary>
    public void QuickJoinSession(Action<LobbyRequestResult> onComplete)
    {
        StartCoroutine(StartSessionCoroutine(GameMode.Client, sessionName: null, maxPlayers: 0, isHost: false, onComplete));
    }

    public void LeaveSession()
    {
        _session.Reset();
        OnSessionUpdated?.Invoke(_session);

        if (IsRunning || TryGetAliveRunner(out NetworkRunner runner) && runner.IsCloudReady)
        {
            StartCoroutine(LeaveSessionCoroutine());
        }
    }

    public void SetLocalReady(bool isReady)
    {
        if (_session.State != ELobbyState.InLobby || !IsRunning || _session.IsHost)
        {
            return;
        }

        if (!App.Game.Players.TryGetLocal(out PlayerNetworkObject lobbyPlayer))
        {
            return;
        }

        if (lobbyPlayer.IsReady == isReady)
        {
            return;
        }

        // Host 모드: StateAuthority(호스트)에게 변경을 요청하고, [Networked] 복제로 전체에 반영된다.
        lobbyPlayer.RPC_SetReady(isReady);
    }

    public void SetLocalDisplayName(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            _localDisplayName = displayName.Trim();
        }

        if (_session.State == ELobbyState.InLobby)
        {
            RefreshSessionPlayers();
        }
    }

    public void StartGame(Action<LobbyRequestResult> onComplete)
    {
        if (_session.State != ELobbyState.InLobby)
        {
            onComplete?.Invoke(LobbyRequestResult.Fail("로비에 있지 않습니다."));
            return;
        }

        if (!_session.IsHost)
        {
            onComplete?.Invoke(LobbyRequestResult.Fail("호스트만 게임을 시작할 수 있습니다."));
            return;
        }

        if (!CanStartGame())
        {
            onComplete?.Invoke(LobbyRequestResult.Fail("시작 조건을 만족하지 않습니다."));
            return;
        }

        StartCoroutine(LoadInGameSceneCoroutine(onComplete));
    }

    public void Shutdown()
    {
        _session.Reset();
        StartCoroutine(ShutdownSessionCoroutine(disconnectCloud: true));
    }

    public IReadOnlyList<PlayerRef> GetActivePlayers()
    {
        if (!IsRunning)
        {
            return Array.Empty<PlayerRef>();
        }

        return new List<PlayerRef>(_runner.ActivePlayers);
    }

    public string GetLocalPlayerId()
    {
        return IsRunning ? _runner.LocalPlayer.PlayerId.ToString() : string.Empty;
    }

    public bool CanStartGame()
    {
        if (_session.State != ELobbyState.InLobby || !_session.IsHost)
        {
            return false;
        }

        if (_session.PlayerCount < _minPlayersToStart)
        {
            return false;
        }

        if (_requireAllReady)
        {
            return _session.AllClientsReady;
        }

        return true;
    }

    IEnumerator ConnectToCloudCoroutine(Action<LobbyRequestResult> onComplete)
    {
        if (IsCloudConnected && TryGetAliveRunner(out NetworkRunner cloudRunner) && cloudRunner.IsCloudReady && !cloudRunner.IsRunning)
        {
            onComplete?.Invoke(LobbyRequestResult.Success());
            yield break;
        }

        yield return EnsureCloudLobbyCoroutine();

        if (IsCloudConnected)
        {
            onComplete?.Invoke(LobbyRequestResult.Success());
            yield break;
        }

        onComplete?.Invoke(LobbyRequestResult.Fail(_lastCloudConnectError));
    }

    IEnumerator StartSessionCoroutine(
        GameMode gameMode,
        string sessionName,
        int maxPlayers,
        bool isHost,
        Action<LobbyRequestResult> onComplete)
    {
        if (IsRunning)
        {
            yield return ShutdownSessionCoroutine(disconnectCloud: false);
        }

        yield return EnsureCloudLobbyCoroutine();
        if (!IsCloudConnected)
        {
            onComplete?.Invoke(LobbyRequestResult.Fail(_lastCloudConnectError));
            yield break;
        }

        int playerCount = maxPlayers > 0 ? Mathf.Clamp(maxPlayers, 2, MaxPlayers) : MaxPlayers;

        var startTask = _runner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = sessionName,
            PlayerCount = gameMode == GameMode.Host ? playerCount : 0,
            SceneManager = _runner.GetComponent<INetworkSceneManager>(),
            ObjectProvider = _runner.GetComponent<INetworkObjectProvider>(),
        });

        yield return new WaitUntil(() => startTask.IsCompleted);

        if (!startTask.Result.Ok)
        {
            onComplete?.Invoke(LobbyRequestResult.Fail(GetStartGameErrorMessage(startTask.Result.ShutdownReason)));
            yield return EnsureCloudLobbyCoroutine(rejoinAfterFailedStart: true);
            yield break;
        }

        string resolvedCode = ResolveSessionCode(sessionName);
        int sessionMaxPlayers = gameMode == GameMode.Host ? playerCount : _defaultMaxPlayers;
        ApplyConnectedSession(resolvedCode, sessionMaxPlayers, isHost);
        onComplete?.Invoke(LobbyRequestResult.Success());
    }

    string ResolveSessionCode(string requestedCode)
    {
        if (TryGetAliveRunner(out NetworkRunner runner) &&
            runner.SessionInfo.IsValid &&
            !string.IsNullOrEmpty(runner.SessionInfo.Name))
        {
            return LobbyCodeUtility.NormalizeCode(runner.SessionInfo.Name);
        }

        return string.IsNullOrEmpty(requestedCode)
            ? string.Empty
            : LobbyCodeUtility.NormalizeCode(requestedCode);
    }

    IEnumerator LoadInGameSceneCoroutine(Action<LobbyRequestResult> onComplete)
    {
        if (!IsRunning || !_runner.IsServer)
        {
            onComplete?.Invoke(LobbyRequestResult.Fail("호스트만 인게임 씬을 로드할 수 있습니다."));
            yield break;
        }

        var sceneRef = SceneRef.FromIndex((int)EScene.InGame);
        var loadOp = _runner.LoadScene(sceneRef);

        yield return new WaitUntil(() => loadOp.IsDone);

        if (loadOp.Error == null)
        {
            _session.SetState(ELobbyState.Starting);
            OnSessionUpdated?.Invoke(_session);
            onComplete?.Invoke(LobbyRequestResult.Success());
            yield break;
        }

        onComplete?.Invoke(LobbyRequestResult.Fail(loadOp.Error.Message));
    }

    IEnumerator LeaveSessionCoroutine()
    {
        yield return ShutdownSessionCoroutine(disconnectCloud: false);
        yield return EnsureCloudLobbyCoroutine();
    }

    IEnumerator ShutdownSessionCoroutine(bool disconnectCloud)
    {
        if (!TryGetAliveRunner(out NetworkRunner runner))
        {
            yield break;
        }

        if (runner.IsRunning || (IsCloudConnected && runner.IsCloudReady))
        {
            var shutdownTask = runner.Shutdown(false, ShutdownReason.Ok);
            yield return new WaitUntil(() => shutdownTask.IsCompleted);

            if (disconnectCloud)
            {
                IsCloudConnected = false;
            }
        }
    }

    IEnumerator EnsureCloudLobbyCoroutine(bool rejoinAfterFailedStart = false)
    {
        _isRestoringCloudLobby = true;
        _lastCloudConnectError = "클라우드 로비에 연결할 수 없습니다.";

        if (rejoinAfterFailedStart && _runner.IsRunning)
        {
            yield return ShutdownSessionCoroutine(disconnectCloud: false);
        }

        if (_runner.IsCloudReady && !_runner.IsRunning)
        {
            IsCloudConnected = true;
            _isRestoringCloudLobby = false;
            yield break;
        }

        if (_runner.IsRunning)
        {
            _isRestoringCloudLobby = false;
            yield break;
        }

        string nickname = PlayerProfile.NormalizeDisplayName(_localDisplayName);
        PlayerProfile.SaveDisplayName(nickname);
        string userId = nickname;
        var auth = new AuthenticationValues(userId) { UserId = userId };

        var lobbyTask = _runner.JoinSessionLobby(SessionLobby.ClientServer, authentication: auth);
        yield return new WaitUntil(() => lobbyTask.IsCompleted);

        if (lobbyTask.Result.Ok)
        {
            IsCloudConnected = true;
            _isRestoringCloudLobby = false;
            yield break;
        }

        IsCloudConnected = false;
        _lastCloudConnectError = FormatStartGameError(lobbyTask.Result, "클라우드 로비 연결");
        Debug.LogError($"[NetworkManager] {_lastCloudConnectError}", this);
        _isRestoringCloudLobby = false;
    }

    void EnsureRunnerComponents()
    {
        if (_runner.GetComponent<INetworkSceneManager>() == null)
        {
            _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        if (_runner.GetComponent<INetworkObjectProvider>() == null)
        {
            _runner.gameObject.AddComponent<NetworkObjectProviderDefault>();
        }
    }

    static string FormatStartGameError(StartGameResult result, string context)
    {
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            return $"{context} 실패: {result.ErrorMessage}";
        }

        if (result.ShutdownReason != ShutdownReason.Ok)
        {
            return $"{context} 실패: {result.ShutdownReason}";
        }

        return $"{context}에 실패했습니다.";
    }

    static string GetStartGameErrorMessage(ShutdownReason shutdownReason)
    {
        if (shutdownReason == ShutdownReason.GameNotFound)
        {
            return SessionNotFoundMessage;
        }

        return shutdownReason != ShutdownReason.Ok
            ? shutdownReason.ToString()
            : "Fusion 세션 시작에 실패했습니다.";
    }

    public bool TryGetAliveRunner(out NetworkRunner runner)
    {
        runner = _runner;
        return runner != null;
    }

    void ApplyConnectedSession(string sessionCode, int maxPlayers, bool isHost)
    {
        _session.Reset();
        _session.ApplyConnected(sessionCode, maxPlayers, isHost, GetLocalPlayerId());
        RefreshSessionPlayers();
    }

    void RefreshSessionPlayers()
    {
        if (_session.State == ELobbyState.Disconnected || string.IsNullOrEmpty(_session.SessionCode))
        {
            return;
        }

        _session.ReplacePlayers(App.Game.Players.BuildLobbyPlayers());
        OnSessionUpdated?.Invoke(_session);
    }

    #region INetworkRunnerCallbacks

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            App.Game.Players.Spawn(runner, player);
        }

        RefreshSessionPlayers();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            App.Game.Players.Despawn(runner, player);
        }

        RefreshSessionPlayers();
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (shutdownReason == ShutdownReason.GameNotFound)
        {
            _session.Reset();
            OnSessionUpdated?.Invoke(_session);
            StartCoroutine(EnsureCloudLobbyCoroutine(rejoinAfterFailedStart: false));
            return;
        }

        if (shutdownReason != ShutdownReason.Ok)
        {
            IsCloudConnected = false;
            _session.Reset();
            OnSessionUpdated?.Invoke(_session);
            OnError?.Invoke($"네트워크 연결이 종료되었습니다. ({shutdownReason})");
        }
    }

    public void OnConnectedToServer(NetworkRunner runner) => IsCloudConnected = true;

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        if (!_isRestoringCloudLobby)
        {
            IsCloudConnected = false;
        }
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        if (key.Equals(InGameRpsKey))
        {
            HandleInGameRpsData(runner, player, data);
        }
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner.IsServer && SceneManager.GetActiveScene().buildIndex == (int)EScene.InGame)
        {
            OnInGameSceneReady?.Invoke();
        }
    }

    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    #endregion

    #region InGame RPS

    static readonly ReliableKey InGameRpsKey = ReliableKey.FromInts(0x52475053, 0, 0, 0);

    bool _localAssignmentNotified;

    public event Action OnInGameSceneReady;
    public event Action OnFingerAssignmentsReceived;
    public event Action<float> OnRoundStarted;
    public event Action<EHandPosition> OnRoundResultReceived;
    public event Action OnStageSelected;

    /// <summary>호스트가 각 플레이어의 손가락 배정을 [Networked] 값으로 기록합니다.</summary>
    public void ServerInitializeFingerAssignments()
    {
        if (!App.IsGameScene || !TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        PlayerManager players = App.Game.Players;
        IReadOnlyList<PlayerRef> activePlayers = GetActivePlayers();
        var playerIds = new List<int>(activePlayers.Count);
        foreach (PlayerRef player in activePlayers)
        {
            playerIds.Add(player.PlayerId);
        }

        playerIds.Sort();

        Dictionary<int, EFingerType> assignments = RpsHandUtility.CreateRandomAssignments(playerIds);

        foreach (PlayerRef player in activePlayers)
        {
            if (players.TryGet(player, out PlayerNetworkObject playerObject))
            {
                playerObject.AssignedFinger = assignments.TryGetValue(player.PlayerId, out EFingerType finger)
                    ? finger
                    : EFingerType.None;
                playerObject.IsFingerExtended = false;
            }
        }
    }

    /// <summary>로컬 플레이어의 손가락 입력을 [Networked] 값으로 반영합니다.</summary>
    public void ClientSendFingerState(bool isExtended)
    {
        if (!App.IsGameScene || !TryGetAliveRunner(out NetworkRunner runner) || !runner.IsRunning)
        {
            return;
        }

        if (!App.Game.Players.TryGetLocal(out PlayerNetworkObject playerObject))
        {
            return;
        }

        if (runner.IsServer)
        {
            playerObject.IsFingerExtended = isExtended;
        }
        else
        {
            playerObject.RPC_SetFingerExtended(isExtended);
        }
    }

    public void ServerBroadcastStageSelected()
    {
        if (!IsServerHost) return;
        BroadcastInGamePayload(new byte[] { (byte)EInGameRpsMessage.StageSelected });
        OnStageSelected?.Invoke();
    }

    public void ServerStartRound(float durationSeconds)
    {
        if (!App.IsGameScene || !TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        ResetServerFingerExtendedStates();

        byte[] payload =
        {
            (byte)EInGameRpsMessage.StartRound,
        };
        payload = AppendFloat(payload, durationSeconds);

        BroadcastInGamePayload(payload);
        OnRoundStarted?.Invoke(durationSeconds);
    }

    public void ServerJudgeAndBroadcastRoundResult()
    {
        if (!App.IsGameScene || !TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        EHandPosition handPosition = RpsHandUtility.Judge(GetServerExtendedFingersMask());
        BroadcastRoundResult(handPosition);
    }

    /// <summary>호스트 기준으로 모든 player object의 손가락 상태를 모아 RPS 마스크를 계산합니다.</summary>
    EFingerType GetServerExtendedFingersMask()
    {
        EFingerType mask = EFingerType.None;

        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return mask;
        }

        PlayerManager players = App.Game.Players;

        foreach (PlayerRef player in GetActivePlayers())
        {
            if (players.TryGet(player, out PlayerNetworkObject playerObject) &&
                playerObject.AssignedFinger != EFingerType.None &&
                playerObject.IsFingerExtended)
            {
                mask |= playerObject.AssignedFinger;
            }
        }

        return mask;
    }

    void ResetServerFingerExtendedStates()
    {
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        PlayerManager players = App.Game.Players;

        foreach (PlayerRef player in GetActivePlayers())
        {
            if (players.TryGet(player, out PlayerNetworkObject playerObject))
            {
                playerObject.IsFingerExtended = false;
            }
        }
    }

    void BroadcastRoundResult(EHandPosition handPosition)
    {
        byte[] payload =
        {
            (byte)EInGameRpsMessage.RoundResult,
            (byte)handPosition,
        };

        BroadcastInGamePayload(payload);
        OnRoundResultReceived?.Invoke(handPosition);
    }

    void BroadcastInGamePayload(byte[] payload)
    {
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (player == runner.LocalPlayer)
            {
                continue;
            }

            runner.SendReliableDataToPlayer(player, InGameRpsKey, payload);
        }
    }

    static byte[] AppendFloat(byte[] payload, float value)
    {
        var buffer = new List<byte>(payload.Length + 4);
        buffer.AddRange(payload);
        buffer.AddRange(BitConverter.GetBytes(value));
        return buffer.ToArray();
    }

    /// <summary>씬 재진입 시 로컬 배정 알림 상태를 초기화합니다.</summary>
    public void ResetInGameRoundNotification()
    {
        _localAssignmentNotified = false;
    }

    void HandleInGameDataChanged()
    {
        if (!App.IsGameScene)
        {
            return;
        }

        if (!_localAssignmentNotified &&
            App.Game.Players.TryGetLocal(out PlayerNetworkObject playerObject) &&
            playerObject.AssignedFinger != EFingerType.None)
        {
            _localAssignmentNotified = true;
            OnFingerAssignmentsReceived?.Invoke();
        }
    }

    void HandleInGameRpsData(NetworkRunner runner, PlayerRef from, ArraySegment<byte> data)
    {
        if (!App.IsGameScene || data.Count < 1 || data.Array == null)
        {
            return;
        }

        var messageType = (EInGameRpsMessage)data.Array[data.Offset];

        switch (messageType)
        {
            case EInGameRpsMessage.StartRound:
                if (data.Count < 5)
                {
                    return;
                }

                float duration = BitConverter.ToSingle(data.Array, data.Offset + 1);
                OnRoundStarted?.Invoke(duration);
                break;

            case EInGameRpsMessage.RoundResult:
                if (data.Count < 2)
                {
                    return;
                }

                var handPosition = (EHandPosition)data.Array[data.Offset + 1];
                OnRoundResultReceived?.Invoke(handPosition);
                break;

            case EInGameRpsMessage.StageSelected:
                OnStageSelected?.Invoke();
                break;
        }
    }

    #endregion
}
