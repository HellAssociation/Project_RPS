using System;

[Serializable]
public class LobbyPlayer
{
    public string PlayerId;
    public string DisplayName;
    public bool IsHost;
    public bool IsReady;
    public bool IsLocal;

    public LobbyPlayer(string playerId, string displayName, bool isHost, bool isLocal, bool isReady = false)
    {
        PlayerId = playerId;
        DisplayName = displayName;
        IsHost = isHost;
        IsReady = isReady;
        IsLocal = isLocal;
    }
}
