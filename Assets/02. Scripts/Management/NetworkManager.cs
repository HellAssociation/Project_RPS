using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using SystemEnums;
using UnityEngine;

/// <summary>
/// Photon Fusion 네트워크 매니저. Title부터 InGame까지 유지(DDOL).
/// 클라우드 로비·방 세션·준비 동기화·씬 로드 및 인게임 ReliableData를 담당합니다.
/// </summary>
[DefaultExecutionOrder((int)EExecutionOrder.GameManagement)]
public class NetworkManager : CommonManagerBase, INetworkRunnerCallbacks
{
    public const int MaxPlayers = 5;
    public const int MaxRemotePlayers = MaxPlayers - 1;
    public static readonly ReliableKey CanvasSyncKey = ReliableKey.FromInts(0x434E5653, 0, 0, 0);
    public const string SessionNotFoundMessage = "해당하는 대기실이 없습니다.";
    const float SessionListLookupTimeoutSeconds = 5f;
    static readonly ReliableKey LobbyReadyKey = ReliableKey.FromInts(0x4C424452, 0, 0, 0);
    static readonly ReliableKey InGameRpsKey = ReliableKey.FromInts(0x52475053, 0, 0, 0);

    [SerializeField] NetworkRunner _runner;
    LobbySession _session = new();

    string _localDisplayName = "Player";
    bool _localReady;
    int _defaultMaxPlayers = 5;
    int _minPlayersToStart = 1;
    bool _requireAllReady = true;
    bool _isRestoringCloudLobby;
    string _pendingSessionLookupName;
    bool _pendingSessionLookupComplete;
    bool _pendingSessionLookupFound;
    string _lastCloudConnectError = "클라우드 로비에 연결할 수 없습니다.";
    readonly Dictionary<int, bool> _readyByPlayerId = new();

    public event Action OnInGameSceneReady;
    public event Action<IReadOnlyDictionary<int, EFingerType>> OnFingerAssignmentsReceived;
    public event Action<int, bool> OnRemoteFingerStateChanged;
    public event Action<float> OnRoundStarted;
    public event Action<EHandPosition> OnRoundResultReceived;

    public NetworkRunner Runner => TryGetAliveRunner(out NetworkRunner runner) ? runner : null;
    public bool IsRunning => TryGetAliveRunner(out NetworkRunner runner) && runner.IsRunning;
    public bool IsCloudConnected { get; private set; }
    public LobbySession Session => _session;
    public bool IsLocalReady => _localReady;

    public event Action<LobbySession> OnSessionUpdated;
    public event Action<string> OnError;
    public event Action OnPlayersChanged;

    protected override void Awake()
    {
        base.Awake();
        _runner.AddCallbacks(this);
    }

    void OnDestroy()
    {
        ClearPendingSessionLookup();
    }

    public void HandleReliableData(NetworkRunner runner, PlayerRef from, ArraySegment<byte> data)
    {
        HandleInGameRpsData(runner, from, data);
    }

    public bool IsServerHost => TryGetAliveRunner(out NetworkRunner runner) && runner.IsServer;

    public void ServerInitializeFingerAssignments()
    {
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        IReadOnlyList<PlayerRef> activePlayers = GetActivePlayers();
        var playerIds = new List<int>(activePlayers.Count);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            playerIds.Add(activePlayers[i].PlayerId);
        }

        playerIds.Sort();

        Dictionary<int, EFingerType> assignments = RpsHandUtility.CreateRandomAssignments(playerIds);
        ApplyFingerAssignmentsToPlayers(assignments);
        BroadcastAssignFingers(assignments);
    }

    public void ClientSendFingerState(bool isExtended)
    {
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsRunning)
        {
            return;
        }

        byte[] payload =
        {
            (byte)EInGameRpsMessage.FingerState,
            (byte)(isExtended ? 1 : 0),
        };

        if (runner.IsServer)
        {
            ServerSetFingerState(runner.LocalPlayer.PlayerId, isExtended);
            return;
        }

        runner.SendReliableDataToServer(InGameRpsKey, payload);
    }

    public void ServerStartRound(float durationSeconds)
    {
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        ResetFingerExtendedStates();

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
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        EHandPosition handPosition = RpsHandUtility.Judge(App.Game.Manager.GetExtendedFingersMask());
        BroadcastRoundResult(handPosition);
    }

    void ResetFingerExtendedStates()
    {
        App.Game.Manager.ResetAllFingerExtended();
    }

    void ServerSetFingerState(int playerId, bool isExtended)
    {
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        if (!App.Game.Manager.TryGetGamePlayer(playerId, out GamePlayer player) || player.CurrFinger == EFingerType.None)
        {
            return;
        }

        App.Game.Manager.SetFingerExtended(playerId.ToString(), isExtended);
        OnRemoteFingerStateChanged?.Invoke(playerId, isExtended);
        BroadcastFingerStateSync(playerId, isExtended);
    }

    void BroadcastFingerStateSync(int playerId, bool isExtended)
    {
        var buffer = new List<byte>(6)
        {
            (byte)EInGameRpsMessage.FingerStateSync,
        };
        buffer.AddRange(BitConverter.GetBytes(playerId));
        buffer.Add((byte)(isExtended ? 1 : 0));
        BroadcastInGamePayload(buffer.ToArray());
    }

    void ApplyFingerStateSync(int playerId, bool isExtended)
    {
        if (!App.Game.Manager.TryGetGamePlayer(playerId, out GamePlayer player))
        {
            return;
        }

        if (player.IsFingerExtended == isExtended)
        {
            return;
        }

        App.Game.Manager.SetFingerExtended(playerId.ToString(), isExtended);
        OnRemoteFingerStateChanged?.Invoke(playerId, isExtended);
    }

    void BroadcastAssignFingers(IReadOnlyDictionary<int, EFingerType> assignments)
    {
        byte[] payload = BuildAssignFingersPayload(assignments);
        BroadcastInGamePayload(payload);
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

    static byte[] BuildAssignFingersPayload(IReadOnlyDictionary<int, EFingerType> assignments)
    {
        var buffer = new List<byte>(assignments.Count * 5 + 2)
        {
            (byte)EInGameRpsMessage.AssignFingers,
            (byte)assignments.Count,
        };

        foreach (KeyValuePair<int, EFingerType> entry in assignments)
        {
            buffer.AddRange(BitConverter.GetBytes(entry.Key));
            buffer.Add((byte)entry.Value);
        }

        return buffer.ToArray();
    }

    static byte[] AppendFloat(byte[] payload, float value)
    {
        var buffer = new List<byte>(payload.Length + 4);
        buffer.AddRange(payload);
        buffer.AddRange(BitConverter.GetBytes(value));
        return buffer.ToArray();
    }

    void ApplyFingerAssignmentsToPlayers(IReadOnlyDictionary<int, EFingerType> assignments)
    {
        PlayerManager players = App.Game.Manager;

        if (!players.HasGameRoster)
        {
            players.SyncGamePlayersFromNetwork();
        }

        var byStringId = new Dictionary<string, EFingerType>(assignments.Count);

        foreach (KeyValuePair<int, EFingerType> entry in assignments)
        {
            byStringId[entry.Key.ToString()] = entry.Value;
        }

        players.ApplyFingerAssignments(byStringId);
        OnFingerAssignmentsReceived?.Invoke(assignments);
    }

    void HandleInGameRpsData(NetworkRunner runner, PlayerRef from, ArraySegment<byte> data)
    {
        if (data.Count < 1 || data.Array == null)
        {
            return;
        }

        var messageType = (EInGameRpsMessage)data.Array[data.Offset];

        switch (messageType)
        {
            case EInGameRpsMessage.AssignFingers:
                ApplyAssignFingersPayload(data);
                break;

            case EInGameRpsMessage.FingerState:
                if (!runner.IsServer || data.Count < 2)
                {
                    return;
                }

                bool isExtended = data.Array[data.Offset + 1] != 0;
                ServerSetFingerState(from.PlayerId, isExtended);
                break;

            case EInGameRpsMessage.FingerStateSync:
                if (data.Count < 6)
                {
                    return;
                }

                int syncedPlayerId = BitConverter.ToInt32(data.Array, data.Offset + 1);
                bool syncedExtended = data.Array[data.Offset + 5] != 0;
                ApplyFingerStateSync(syncedPlayerId, syncedExtended);
                break;

            case EInGameRpsMessage.StartRound:
                if (data.Count < 5)
                {
                    return;
                }

                float duration = BitConverter.ToSingle(data.Array, data.Offset + 1);
                ResetFingerExtendedStates();
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
        }
    }

    void ApplyAssignFingersPayload(ArraySegment<byte> data)
    {
        if (data.Count < 2 || data.Array == null)
        {
            return;
        }

        int offset = data.Offset + 1;
        int count = data.Array[offset++];

        var assignments = new Dictionary<int, EFingerType>(count);

        for (int i = 0; i < count; i++)
        {
            if (offset + 5 > data.Offset + data.Count)
            {
                break;
            }

            int playerId = BitConverter.ToInt32(data.Array, offset);
            offset += 4;
            var finger = (EFingerType)data.Array[offset++];
            assignments[playerId] = finger;
        }

        ApplyFingerAssignmentsToPlayers(assignments);
    }

    public IReadOnlyList<NetworkPlayerInfo> GetActivePlayerInfos()
    {
        var infos = new List<NetworkPlayerInfo>();

        if (!IsRunning)
        {
            return infos;
        }

        IReadOnlyList<PlayerRef> activePlayers = GetActivePlayers();
        if (activePlayers.Count == 0)
        {
            string localId = GetLocalPlayerId();
            if (!string.IsNullOrEmpty(localId))
            {
                infos.Add(new NetworkPlayerInfo(localId, _localDisplayName, _session.IsHost, true));
            }

            return infos;
        }

        PlayerRef hostPlayer = ResolveHostPlayer(_runner, activePlayers);
        string localPlayerId = GetLocalPlayerId();

        foreach (PlayerRef playerRef in activePlayers)
        {
            string playerId = playerRef.PlayerId.ToString();
            bool isLocal = playerId == localPlayerId;
            bool isHost = playerRef == hostPlayer;
            string displayName = isLocal ? _localDisplayName : $"Player {playerId}";
            infos.Add(new NetworkPlayerInfo(playerId, displayName, isHost, isLocal));
        }

        return infos;
    }

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

    public void LeaveSession()
    {
        ResetLocalRoomState();
        _session.Reset();
        OnSessionUpdated?.Invoke(_session);

        if (IsRunning)
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

        if (_localReady == isReady)
        {
            return;
        }

        _localReady = isReady;

        if (TryGetAliveRunner(out NetworkRunner runner) && runner.IsServer)
        {
            ServerSetPlayerReady(runner.LocalPlayer, isReady);
            return;
        }

        if (TryGetAliveRunner(out NetworkRunner clientRunner))
        {
            _readyByPlayerId[clientRunner.LocalPlayer.PlayerId] = isReady;
        }

        SendReadyStateToServer(isReady);
        RefreshSessionPlayers();
        OnPlayersChanged?.Invoke();
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
        ResetLocalRoomState();
        _session.Reset();
        StartCoroutine(ShutdownGameSessionCoroutine(disconnectCloud: true));
    }

    public bool TrySetPlayerReady(string playerId, bool isReady)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            return false;
        }

        if (playerId == GetLocalPlayerId())
        {
            SetLocalReady(isReady);
            return true;
        }

        return false;
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
        if (IsCloudConnected && TryGetAliveRunner(out NetworkRunner cloudRunner) && cloudRunner.IsCloudReady)
        {
            onComplete?.Invoke(LobbyRequestResult.Success());
            yield break;
        }

        yield return ConnectCloudLobbyCoroutine();

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
            yield return ShutdownGameSessionCoroutine(disconnectCloud: false);
        }

        yield return ConnectCloudLobbyCoroutine();
        if (!IsCloudConnected)
        {
            onComplete?.Invoke(LobbyRequestResult.Fail(_lastCloudConnectError));
            yield break;
        }

        if (gameMode == GameMode.Client)
        {
            bool sessionAvailable = false;
            yield return WaitForJoinableSessionCoroutine(sessionName, found => sessionAvailable = found);

            if (!sessionAvailable)
            {
                onComplete?.Invoke(LobbyRequestResult.Fail(SessionNotFoundMessage));
                yield break;
            }
        }

        int playerCount = maxPlayers > 0 ? Mathf.Clamp(maxPlayers, 2, MaxPlayers) : MaxPlayers;

        var startTask = _runner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = sessionName,
            PlayerCount = playerCount,
            SceneManager = _runner.GetComponent<INetworkSceneManager>(),
            ObjectProvider = _runner.GetComponent<INetworkObjectProvider>(),
        });

        yield return new WaitUntil(() => startTask.IsCompleted);

        if (!startTask.Result.Ok)
        {
            onComplete?.Invoke(LobbyRequestResult.Fail(GetStartGameErrorMessage(startTask.Result.ShutdownReason)));
            yield return ConnectCloudLobbyCoroutine();
            yield break;
        }

        int sessionMaxPlayers = gameMode == GameMode.Host ? playerCount : _defaultMaxPlayers;
        ApplyConnectedSession(sessionName, sessionMaxPlayers, isHost);
        onComplete?.Invoke(LobbyRequestResult.Success());
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
        yield return ShutdownGameSessionCoroutine(disconnectCloud: false);
        yield return ConnectCloudLobbyCoroutine();
    }

    IEnumerator ShutdownGameSessionCoroutine(bool disconnectCloud)
    {
        if (TryGetAliveRunner(out NetworkRunner runner) &&
            (runner.IsRunning || (IsCloudConnected && runner.IsCloudReady)))
        {
            var shutdownTask = runner.Shutdown(false, ShutdownReason.Ok);
            yield return new WaitUntil(() => shutdownTask.IsCompleted);

            if (disconnectCloud)
            {
                IsCloudConnected = false;
            }
        }
    }

    IEnumerator ConnectCloudLobbyCoroutine()
    {
        _isRestoringCloudLobby = true;
        _lastCloudConnectError = "클라우드 로비에 연결할 수 없습니다.";

        if (_runner.IsRunning)
        {
            yield return ShutdownGameSessionCoroutine(disconnectCloud: false);
        }

        if (_runner.IsCloudReady)
        {
            IsCloudConnected = true;
            _isRestoringCloudLobby = false;
            yield break;
        }

        string nickname = PlayerProfile.NormalizeDisplayName(_localDisplayName);
        PlayerProfile.SaveDisplayName(nickname);
        string userId = PlayerProfile.GetOrCreateUserId();
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

    IEnumerator WaitForJoinableSessionCoroutine(string sessionName, Action<bool> onComplete)
    {
        ClearPendingSessionLookup();

        string normalizedName = LobbyCodeUtility.NormalizeCode(sessionName);
        _pendingSessionLookupName = normalizedName;
        _pendingSessionLookupComplete = false;
        _pendingSessionLookupFound = false;

        float deadline = Time.realtimeSinceStartup + SessionListLookupTimeoutSeconds;
        while (!_pendingSessionLookupComplete && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        bool found = _pendingSessionLookupFound;
        ClearPendingSessionLookup();
        onComplete?.Invoke(found);
    }

    void ClearPendingSessionLookup()
    {
        _pendingSessionLookupName = null;
        _pendingSessionLookupComplete = false;
        _pendingSessionLookupFound = false;
    }

    static bool IsJoinableSession(SessionInfo session, string normalizedSessionName)
    {
        if (!session.IsValid || !session.IsOpen)
        {
            return false;
        }

        if (!string.Equals(LobbyCodeUtility.NormalizeCode(session.Name), normalizedSessionName, StringComparison.Ordinal))
        {
            return false;
        }

        if (session.MaxPlayers > 0 && session.PlayerCount >= session.MaxPlayers)
        {
            return false;
        }

        return true;
    }

    static bool TryFindJoinableSession(IReadOnlyList<SessionInfo> sessionList, string normalizedSessionName)
    {
        if (sessionList == null || sessionList.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < sessionList.Count; i++)
        {
            if (IsJoinableSession(sessionList[i], normalizedSessionName))
            {
                return true;
            }
        }

        return false;
    }

    bool TryGetAliveRunner(out NetworkRunner runner)
    {
        runner = _runner;
        return runner != null;
    }

    void ApplyConnectedSession(string sessionCode, int maxPlayers, bool isHost)
    {
        _localReady = false;
        _readyByPlayerId.Clear();
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

        _session.ReplacePlayers(BuildPlayersFromRunner());
        OnSessionUpdated?.Invoke(_session);
    }

    void ResetLocalRoomState()
    {
        _localReady = false;
        _readyByPlayerId.Clear();
        App.Game.Manager.ClearGameRoster();
        App.Game.Manager.ResetInGameState();
    }

    List<LobbyPlayer> BuildPlayersFromRunner()
    {
        var players = new List<LobbyPlayer>();

        if (!IsRunning)
        {
            return players;
        }

        IReadOnlyList<PlayerRef> activePlayers = GetActivePlayers();
        if (activePlayers.Count == 0)
        {
            AddLocalPlayerFallback(players);
            return players;
        }

        PlayerRef hostPlayer = ResolveHostPlayer(_runner, activePlayers);
        string localId = GetLocalPlayerId();

        foreach (PlayerRef playerRef in activePlayers)
        {
            string playerId = playerRef.PlayerId.ToString();
            bool isLocal = playerId == localId;
            bool isHost = playerRef == hostPlayer;
            string displayName = isLocal ? _localDisplayName : $"Player {playerId}";
            bool isReady = !isHost && TryGetReadyState(playerRef.PlayerId, out bool ready) && ready;

            players.Add(new LobbyPlayer(playerId, displayName, isHost, isLocal) { IsReady = isReady });
        }

        return players;
    }

    void AddLocalPlayerFallback(List<LobbyPlayer> players)
    {
        if (!IsRunning || players == null)
        {
            return;
        }

        string localId = GetLocalPlayerId();
        if (string.IsNullOrEmpty(localId))
        {
            return;
        }

        bool isHost = _session.IsHost;
        bool isReady = !isHost && _localReady;
        players.Add(new LobbyPlayer(localId, _localDisplayName, isHost, true) { IsReady = isReady });
    }

    bool TryGetReadyState(int playerId, out bool isReady)
    {
        return _readyByPlayerId.TryGetValue(playerId, out isReady) && isReady;
    }

    void SendReadyStateToServer(bool isReady)
    {
        if (!TryGetAliveRunner(out NetworkRunner runner))
        {
            return;
        }

        byte[] payload = { (byte)ELobbyReadyMessage.SetReady, (byte)(isReady ? 1 : 0) };
        runner.SendReliableDataToServer(LobbyReadyKey, payload);
    }

    void ServerSetPlayerReady(PlayerRef player, bool isReady)
    {
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        _readyByPlayerId[player.PlayerId] = isReady;
        if (player == runner.LocalPlayer)
        {
            _localReady = isReady;
        }

        RefreshSessionPlayers();
        OnPlayersChanged?.Invoke();
        BroadcastReadyState();
    }

    void BroadcastReadyState()
    {
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer)
        {
            return;
        }

        byte[] payload = BuildFullSyncPayload();
        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (player == runner.LocalPlayer)
            {
                continue;
            }

            runner.SendReliableDataToPlayer(player, LobbyReadyKey, payload);
        }
    }

    byte[] BuildFullSyncPayload()
    {
        var buffer = new List<byte>(_readyByPlayerId.Count * 5 + 2)
        {
            (byte)ELobbyReadyMessage.FullSync,
            (byte)_readyByPlayerId.Count,
        };

        foreach (KeyValuePair<int, bool> entry in _readyByPlayerId)
        {
            buffer.AddRange(BitConverter.GetBytes(entry.Key));
            buffer.Add((byte)(entry.Value ? 1 : 0));
        }

        return buffer.ToArray();
    }

    void ApplyFullSyncPayload(ArraySegment<byte> data)
    {
        if (data.Count < 2 || data.Array == null)
        {
            return;
        }

        int offset = data.Offset;
        if ((ELobbyReadyMessage)data.Array[offset] != ELobbyReadyMessage.FullSync)
        {
            return;
        }

        offset++;
        int count = data.Array[offset++];
        _readyByPlayerId.Clear();

        for (int i = 0; i < count; i++)
        {
            if (offset + 5 > data.Offset + data.Count)
            {
                break;
            }

            int playerId = BitConverter.ToInt32(data.Array, offset);
            offset += 4;
            bool isReady = data.Array[offset++] != 0;
            _readyByPlayerId[playerId] = isReady;
        }

        string localId = GetLocalPlayerId();
        if (!string.IsNullOrEmpty(localId) && int.TryParse(localId, out int localPlayerId))
        {
            _localReady = _readyByPlayerId.TryGetValue(localPlayerId, out bool ready) && ready;
        }

        RefreshSessionPlayers();
        OnPlayersChanged?.Invoke();
    }

    void HandleLobbyReadyData(NetworkRunner runner, PlayerRef from, ArraySegment<byte> data)
    {
        if (data.Count < 1 || data.Array == null)
        {
            return;
        }

        var messageType = (ELobbyReadyMessage)data.Array[data.Offset];
        switch (messageType)
        {
            case ELobbyReadyMessage.SetReady:
                if (!runner.IsServer || data.Count < 2)
                {
                    return;
                }

                bool isReady = data.Array[data.Offset + 1] != 0;
                ServerSetPlayerReady(from, isReady);
                break;

            case ELobbyReadyMessage.FullSync:
                ApplyFullSyncPayload(data);
                break;
        }
    }

    static PlayerRef ResolveHostPlayer(NetworkRunner runner, IReadOnlyList<PlayerRef> activePlayers)
    {
        if (runner.IsServer)
        {
            return runner.LocalPlayer;
        }

        PlayerRef host = default;
        int minId = int.MaxValue;

        foreach (PlayerRef playerRef in activePlayers)
        {
            if (playerRef.PlayerId < minId)
            {
                minId = playerRef.PlayerId;
                host = playerRef;
            }
        }

        return host;
    }

    #region INetworkRunnerCallbacks

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            _readyByPlayerId[player.PlayerId] = false;
            BroadcastReadyState();
        }

        RefreshSessionPlayers();
        OnPlayersChanged?.Invoke();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            _readyByPlayerId.Remove(player.PlayerId);
            BroadcastReadyState();
        }
        else
        {
            _readyByPlayerId.Remove(player.PlayerId);
        }

        RefreshSessionPlayers();
        OnPlayersChanged?.Invoke();
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (shutdownReason == ShutdownReason.GameNotFound)
        {
            ResetLocalRoomState();
            _session.Reset();
            OnSessionUpdated?.Invoke(_session);
            StartCoroutine(ConnectCloudLobbyCoroutine());
            return;
        }

        if (shutdownReason != ShutdownReason.Ok)
        {
            IsCloudConnected = false;
            ResetLocalRoomState();
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
        if (key.Equals(LobbyReadyKey))
        {
            HandleLobbyReadyData(runner, player, data);
            return;
        }

        if (key.Equals(InGameRpsKey))
        {
            HandleInGameRpsData(runner, player, data);
            return;
        }

        if (key.Equals(CanvasSyncKey))
        {
            HandleReliableData(runner, player, data);
        }
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        if (string.IsNullOrEmpty(_pendingSessionLookupName) || _pendingSessionLookupComplete)
        {
            return;
        }

        if (TryFindJoinableSession(sessionList, _pendingSessionLookupName))
        {
            _pendingSessionLookupFound = true;
            _pendingSessionLookupComplete = true;
        }
    }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!runner.IsServer)
        {
            return;
        }

        OnInGameSceneReady?.Invoke();
    }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    #endregion
}
