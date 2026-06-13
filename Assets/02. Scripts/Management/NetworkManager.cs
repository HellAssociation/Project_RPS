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

    public void StartGame(EScene targetScene, Action<LobbyRequestResult> onComplete)
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

        StartCoroutine(LoadInGameSceneCoroutine(targetScene, onComplete));
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

    IEnumerator LoadInGameSceneCoroutine(EScene targetScene, Action<LobbyRequestResult> onComplete)
    {
        if (!IsRunning || !_runner.IsServer)
        {
            onComplete?.Invoke(LobbyRequestResult.Fail("호스트만 인게임 씬을 로드할 수 있습니다."));
            yield break;
        }

        var sceneRef = SceneRef.FromIndex((int)targetScene);
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
        if (!runner.IsServer) return;
        int idx = SceneManager.GetActiveScene().buildIndex;
        if (idx == (int)EScene.PvE_1v1 || idx == (int)EScene.PvE_1v5 || idx == (int)EScene.PvP_1v1)
            OnInGameSceneReady?.Invoke();
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
    public event Action<int, int> OnStageSelected;
    public event Action<IReadOnlyList<CardRef>> OnCardsApplied;
    public event Action<ECardKind, int[], float> OnCardOfferReceived;
    public event Action<ECardKind, int> OnCardVoteResult;
    public event Action<int> OnBossIntro;

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

    public void ServerBroadcastStageSelected(int stageIndex)
    {
        if (!IsServerHost) return;

        // Host-authoritative seed so every client derives the same enemy hand sequence.
        int seed = new System.Random().Next();
        byte[] payload = { (byte)EInGameRpsMessage.StageSelected, (byte)stageIndex };
        payload = AppendInt(payload, seed);

        BroadcastInGamePayload(payload);
        OnStageSelected?.Invoke(stageIndex, seed);
    }

    /// <summary>호스트가 적용 확정한 카드 목록을 브로드캐스트합니다. 모든 클라가 동일 효과를 RunState에 적용합니다.</summary>
    public void ServerApplyCards(IReadOnlyList<CardRef> cards)
    {
        if (!IsServerHost || cards == null) return;

        var payload = new List<byte>(2 + cards.Count * 2)
        {
            (byte)EInGameRpsMessage.CardsApplied,
            (byte)cards.Count,
        };
        foreach (CardRef card in cards)
        {
            payload.Add((byte)card.Kind);
            payload.Add((byte)card.Index);
        }

        BroadcastInGamePayload(payload.ToArray());
        OnCardsApplied?.Invoke(cards);
    }

    /// <summary>호스트가 이번 투표에 제시할 카드(종류+인덱스 목록)와 제한시간을 브로드캐스트합니다.</summary>
    public void ServerBroadcastCardOffer(ECardKind kind, int[] indices, float duration)
    {
        if (!IsServerHost || indices == null) return;

        var payload = new List<byte>(7 + indices.Length)
        {
            (byte)EInGameRpsMessage.CardOffer,
            (byte)kind,
        };
        payload.AddRange(BitConverter.GetBytes(duration));
        payload.Add((byte)indices.Length);
        foreach (int index in indices) payload.Add((byte)index);

        BroadcastInGamePayload(payload.ToArray());
        OnCardOfferReceived?.Invoke(kind, indices, duration);
    }

    /// <summary>로컬 플레이어의 카드 투표를 [Networked] 값으로 반영합니다. (라이브 집계는 모든 클라가 직접 읽음)</summary>
    public void ClientSendCardVote(int choice)
    {
        if (!App.IsGameScene || !TryGetAliveRunner(out NetworkRunner runner) || !runner.IsRunning)
            return;

        if (!App.Game.Players.TryGetLocal(out PlayerNetworkObject playerObject))
            return;

        if (runner.IsServer)
            playerObject.CardVote = choice;
        else
            playerObject.RPC_SetCardVote(choice);
    }

    /// <summary>새 투표 시작 시 호스트가 모든 플레이어의 투표를 무효표로 초기화합니다.</summary>
    public void ServerResetCardVotes()
    {
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer) return;

        PlayerManager players = App.Game.Players;
        foreach (PlayerRef player in GetActivePlayers())
        {
            if (players.TryGet(player, out PlayerNetworkObject playerObject))
                playerObject.CardVote = PlayerNetworkObject.NO_VOTE;
        }
    }

    /// <summary>활성 플레이어 전원이 투표를 마쳤는지(조기 확정 판정용).</summary>
    public bool AllPlayersVoted()
    {
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsServer) return false;

        PlayerManager players = App.Game.Players;
        bool any = false;
        foreach (PlayerRef player in GetActivePlayers())
        {
            if (!players.TryGet(player, out PlayerNetworkObject playerObject))
                continue;
            any = true;
            if (playerObject.CardVote == PlayerNetworkObject.NO_VOTE)
                return false;
        }
        return any;
    }

    /// <summary>제시된 카드 인덱스별 득표수를 집계합니다. 인덱스 순서는 offered와 동일.</summary>
    public void CollectCardVotes(int[] offered, int[] tallyBuffer)
    {
        for (int i = 0; i < tallyBuffer.Length; i++) tallyBuffer[i] = 0;
        if (!TryGetAliveRunner(out NetworkRunner runner) || !runner.IsRunning) return;

        PlayerManager players = App.Game.Players;
        foreach (PlayerRef player in GetActivePlayers())
        {
            if (!players.TryGet(player, out PlayerNetworkObject playerObject)) continue;
            int vote = playerObject.CardVote;
            for (int i = 0; i < offered.Length; i++)
            {
                if (offered[i] == vote) { tallyBuffer[i]++; break; }
            }
        }
    }

    /// <summary>투표 확정 카드를 브로드캐스트합니다. chosenIndex == -1이면 해당 투표 없음.</summary>
    public void ServerBroadcastCardResult(ECardKind kind, int chosenIndex)
    {
        if (!IsServerHost) return;

        byte[] payload =
        {
            (byte)EInGameRpsMessage.CardVoteResult,
            (byte)kind,
            (byte)(chosenIndex < 0 ? 255 : chosenIndex),
        };

        BroadcastInGamePayload(payload);
        OnCardVoteResult?.Invoke(kind, chosenIndex);
    }

    /// <summary>보스(매 라운드 10번째 적) 등장을 브로드캐스트합니다.</summary>
    public void ServerBroadcastBossIntro(int round)
    {
        if (!IsServerHost) return;

        byte[] payload =
        {
            (byte)EInGameRpsMessage.BossIntro,
            (byte)round,
        };

        BroadcastInGamePayload(payload);
        OnBossIntro?.Invoke(round);
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
        if (ModeData.IsSingleControl)
            return App.SystemManager.Input.ExtendedFingersMask;

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

    static byte[] AppendInt(byte[] payload, int value)
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
                if (data.Count < 6)
                {
                    return;
                }

                int stageIndex = data.Array[data.Offset + 1];
                int stageSeed = BitConverter.ToInt32(data.Array, data.Offset + 2);
                OnStageSelected?.Invoke(stageIndex, stageSeed);
                break;

            case EInGameRpsMessage.CardsApplied:
                if (data.Count < 2)
                {
                    return;
                }

                int cardCount = data.Array[data.Offset + 1];
                if (data.Count < 2 + cardCount * 2)
                {
                    return;
                }

                var cards = new List<CardRef>(cardCount);
                for (int i = 0; i < cardCount; i++)
                {
                    var kind = (ECardKind)data.Array[data.Offset + 2 + i * 2];
                    int index = data.Array[data.Offset + 3 + i * 2];
                    cards.Add(new CardRef(kind, index));
                }

                OnCardsApplied?.Invoke(cards);
                break;

            case EInGameRpsMessage.CardOffer:
                if (data.Count < 7)
                {
                    return;
                }

                var offerKind = (ECardKind)data.Array[data.Offset + 1];
                float offerDuration = BitConverter.ToSingle(data.Array, data.Offset + 2);
                int offerCount = data.Array[data.Offset + 6];
                if (data.Count < 7 + offerCount)
                {
                    return;
                }

                var offered = new int[offerCount];
                for (int i = 0; i < offerCount; i++)
                    offered[i] = data.Array[data.Offset + 7 + i];

                OnCardOfferReceived?.Invoke(offerKind, offered, offerDuration);
                break;

            case EInGameRpsMessage.CardVoteResult:
                if (data.Count < 3)
                {
                    return;
                }

                var resultKind = (ECardKind)data.Array[data.Offset + 1];
                byte resultByte = data.Array[data.Offset + 2];
                int chosenIndex = resultByte == 255 ? -1 : resultByte;
                OnCardVoteResult?.Invoke(resultKind, chosenIndex);
                break;

            case EInGameRpsMessage.BossIntro:
                if (data.Count < 2)
                {
                    return;
                }

                int bossRound = data.Array[data.Offset + 1];
                OnBossIntro?.Invoke(bossRound);
                break;
        }
    }

    #endregion
}
