using System;
using System.Collections.Generic;
using SystemEnums;

[Serializable]
public class LobbySession
{
    readonly List<LobbyPlayer> _players = new();

    public string SessionCode { get; private set; }
    public ELobbyState State { get; private set; } = ELobbyState.Disconnected;
    public int MaxPlayers { get; private set; }
    public bool IsHost { get; private set; }
    public string LocalPlayerId { get; private set; }

    public IReadOnlyList<LobbyPlayer> Players => _players;
    public int PlayerCount => _players.Count;
    public bool IsFull => PlayerCount >= MaxPlayers;
    /// <summary>호스트를 제외한 모든 클라이언트가 준비되었는지 (빌드 시작 조건).</summary>
    public bool AllClientsReady
    {
        get
        {
            if (_players.Count == 0)
            {
                return false;
            }

            bool hasClient = false;
            for (int i = 0; i < _players.Count; i++)
            {
                LobbyPlayer player = _players[i];
                if (player.IsHost)
                {
                    continue;
                }

                hasClient = true;
                if (!player.IsReady)
                {
                    return false;
                }
            }

            return hasClient || _players.Count == 1;
        }
    }

    public void Reset()
    {
        SessionCode = string.Empty;
        State = ELobbyState.Disconnected;
        MaxPlayers = 0;
        IsHost = false;
        LocalPlayerId = string.Empty;
        _players.Clear();
    }

    public void ApplyConnected(string sessionCode, int maxPlayers, bool isHost, string localPlayerId)
    {
        SessionCode = sessionCode;
        MaxPlayers = maxPlayers;
        IsHost = isHost;
        LocalPlayerId = localPlayerId;
        State = ELobbyState.InLobby;
    }

    public void SetState(ELobbyState state)
    {
        State = state;
    }

    public void ReplacePlayers(IReadOnlyList<LobbyPlayer> players)
    {
        _players.Clear();
        _players.AddRange(players);
    }
}
