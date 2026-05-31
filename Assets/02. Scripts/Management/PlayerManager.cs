using System;
using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

/// <summary>
/// 인게임 플레이어 목록 및 손가락 배정/입력 상태를 관리합니다.
/// </summary>
[DefaultExecutionOrder((int)EExecutionOrder.SystemManagement)]
public class PlayerManager : CommonManagerBase
{
    readonly Dictionary<string, GamePlayer> _gamePlayersById = new();
    readonly List<GamePlayer> _gamePlayers = new();

    GamePlayer _localGamePlayer;
    string _localPlayerId = string.Empty;

    public event Action OnGamePlayersChanged;
    public event Action OnFingerAssignmentsChanged;

    public string LocalPlayerId => _localPlayerId;
    public bool HasGameRoster => _gamePlayers.Count > 0;
    public GamePlayer LocalGamePlayer => _localGamePlayer;
    public IReadOnlyList<GamePlayer> GamePlayers => _gamePlayers;
    public EFingerType LocalAssignedFinger => _localGamePlayer?.CurrFinger ?? EFingerType.None;

    /// <summary>
    /// 인게임 진입 시 NetworkRunner 활성 플레이어로 GamePlayer 목록을 구성합니다.
    /// </summary>
    public void SyncGamePlayersFromNetwork()
    {
        _gamePlayersById.Clear();
        _gamePlayers.Clear();
        _localGamePlayer = null;

        NetworkManager network = App.Game.Network;
        if (!network.IsRunning)
        {
            OnGamePlayersChanged?.Invoke();
            return;
        }

        _localPlayerId = network.GetLocalPlayerId();
        IReadOnlyList<NetworkPlayerInfo> infos = network.GetActivePlayerInfos();

        for (int i = 0; i < infos.Count; i++)
        {
            NetworkPlayerInfo info = infos[i];
            var player = new GamePlayer(info.PlayerId, info.DisplayName, info.IsHost, info.IsLocal);
            _gamePlayers.Add(player);
            _gamePlayersById[info.PlayerId] = player;

            if (info.IsLocal)
            {
                _localGamePlayer = player;
            }
        }

        OnGamePlayersChanged?.Invoke();
    }

    public void ClearGameRoster()
    {
        _gamePlayersById.Clear();
        _gamePlayers.Clear();
        _localGamePlayer = null;
        _localPlayerId = string.Empty;
        OnGamePlayersChanged?.Invoke();
    }

    public void ResetInGameState()
    {
        for (int i = 0; i < _gamePlayers.Count; i++)
        {
            _gamePlayers[i].CurrFinger = EFingerType.None;
            _gamePlayers[i].IsFingerExtended = false;
        }

        OnGamePlayersChanged?.Invoke();
    }

    public void ResetAllFingerExtended()
    {
        for (int i = 0; i < _gamePlayers.Count; i++)
        {
            _gamePlayers[i].IsFingerExtended = false;
        }

        OnGamePlayersChanged?.Invoke();
    }

    /// <summary>
    /// 방장이 무작위 배분한 손가락 정보를 모든 클라이언트에 적용합니다.
    /// </summary>
    public void ApplyFingerAssignments(IReadOnlyDictionary<string, EFingerType> assignments)
    {
        if (assignments == null)
        {
            return;
        }

        foreach (KeyValuePair<string, EFingerType> entry in assignments)
        {
            if (!_gamePlayersById.TryGetValue(entry.Key, out GamePlayer player))
            {
                continue;
            }

            player.CurrFinger = entry.Value;
            player.IsFingerExtended = false;
        }

        OnGamePlayersChanged?.Invoke();
        OnFingerAssignmentsChanged?.Invoke();
    }

    public void SetFingerExtended(string playerId, bool isExtended)
    {
        if (!_gamePlayersById.TryGetValue(playerId, out GamePlayer player))
        {
            return;
        }

        if (player.IsFingerExtended == isExtended)
        {
            return;
        }

        player.IsFingerExtended = isExtended;
        LogFingerManipulation(player);
        OnGamePlayersChanged?.Invoke();
    }

    void LogFingerManipulation(GamePlayer player)
    {
        string who = player.IsLocal ? $"[로컬] {player.DisplayName}" : player.DisplayName;
        string action = player.IsFingerExtended ? "펼침" : "접음";
        EFingerType handMask = GetExtendedFingersMask();

        Debug.Log($"[PlayerManager] {who} — {player.CurrFinger} {action} | 손 마스크: {handMask}");
    }

    public void SetLocalFingerExtended(bool isExtended)
    {
        if (_localGamePlayer == null)
        {
            return;
        }

        SetFingerExtended(_localGamePlayer.PlayerId, isExtended);
    }

    public EFingerType GetExtendedFingersMask()
    {
        EFingerType mask = EFingerType.None;

        for (int i = 0; i < _gamePlayers.Count; i++)
        {
            GamePlayer player = _gamePlayers[i];
            if (player.CurrFinger != EFingerType.None && player.IsFingerExtended)
            {
                mask |= player.CurrFinger;
            }
        }

        return mask;
    }

    public bool TryGetGamePlayer(string playerId, out GamePlayer player)
    {
        return _gamePlayersById.TryGetValue(playerId, out player);
    }

    public bool TryGetGamePlayer(int playerId, out GamePlayer player)
    {
        return TryGetGamePlayer(playerId.ToString(), out player);
    }

    public bool IsLocal(string playerId)
    {
        return playerId == _localPlayerId;
    }
}
