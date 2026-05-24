using System;
using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

/// <summary>
/// 현재 세션에 참가한 플레이어 목록을 중앙에서 관리합니다.
/// LobbyManager 세션 갱신과 동기화되며, GetPlayer 등으로 개별 플레이어를 조회·조작합니다.
/// </summary>
[DefaultExecutionOrder((int)EExecutionOrder.SystemManagement)]
public class PlayerManager : CommonManagerBase
{
    readonly Dictionary<string, LobbyPlayer> _playersById = new();
    readonly List<LobbyPlayer> _players = new();

    LobbyPlayer _localPlayer;
    string _localPlayerId = string.Empty;

    public event Action OnPlayersChanged;
    public event Action<LobbyPlayer> OnLocalPlayerChanged;

    public bool HasSession => App.SceneManager.Lobby.IsInLobby;
    public int Count => _players.Count;
    public string LocalPlayerId => _localPlayerId;
    public LobbyPlayer LocalPlayer => _localPlayer;
    public IReadOnlyList<LobbyPlayer> AllPlayers => _players;

    public bool AllReady => _players.Count > 0 && _players.TrueForAll(player => player.IsReady);

    protected override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        SubscribeLobby();
    }

    void OnDestroy()
    {
        UnsubscribeLobby();
    }

    void SubscribeLobby()
    {
        App.SceneManager.Lobby.OnSessionUpdated -= HandleSessionUpdated;
        App.SceneManager.Lobby.OnSessionUpdated += HandleSessionUpdated;
        SyncFromSession(App.SceneManager.Lobby.Session);
    }

    void UnsubscribeLobby()
    {
        App.SceneManager.Lobby.OnSessionUpdated -= HandleSessionUpdated;
    }

    void HandleSessionUpdated(LobbySession session)
    {
        SyncFromSession(session);
    }

    void SyncFromSession(LobbySession session)
    {
        var previousLocalId = _localPlayerId;

        _playersById.Clear();
        _players.Clear();
        _localPlayer = null;
        _localPlayerId = session.LocalPlayerId;

        IReadOnlyList<LobbyPlayer> source = session.Players;
        for (int i = 0; i < source.Count; i++)
        {
            LobbyPlayer player = source[i];
            _players.Add(player);
            _playersById[player.PlayerId] = player;

            if (player.IsLocal)
            {
                _localPlayer = player;
            }
        }

        OnPlayersChanged?.Invoke();

        if (_localPlayer != null && previousLocalId != _localPlayerId)
        {
            OnLocalPlayerChanged?.Invoke(_localPlayer);
        }
    }

    public LobbyPlayer GetPlayer(string playerId)
    {
        return _playersById[playerId];
    }

    public bool TryGetPlayer(string playerId, out LobbyPlayer player)
    {
        return _playersById.TryGetValue(playerId, out player);
    }

    public bool TryGetPlayer(int playerId, out LobbyPlayer player)
    {
        return TryGetPlayer(playerId.ToString(), out player);
    }

    public bool TryGetLocalPlayer(out LobbyPlayer player)
    {
        player = _localPlayer;
        return player != null;
    }

    public bool Contains(string playerId)
    {
        return _playersById.ContainsKey(playerId);
    }

    public bool IsLocal(string playerId)
    {
        return playerId == _localPlayerId;
    }

    public void SetLocalDisplayName(string displayName)
    {
        App.SceneManager.Lobby.SetLocalDisplayName(displayName);
    }

    public void SetLocalReady(bool isReady)
    {
        App.SceneManager.Lobby.SetLocalReady(isReady);
    }

    /// <summary>
    /// 준비 상태 변경. 현재는 로컬 플레이어만 지원합니다.
    /// </summary>
    public bool TrySetReady(string playerId, bool isReady)
    {
        LobbyPlayer player = GetPlayer(playerId);

        if (player.IsLocal)
        {
            SetLocalReady(isReady);
            return true;
        }

        if (!App.SceneManager.Lobby.IsHost)
        {
            return false;
        }

        return App.SceneManager.Lobby.TrySetPlayerReady(playerId, isReady);
    }
}
