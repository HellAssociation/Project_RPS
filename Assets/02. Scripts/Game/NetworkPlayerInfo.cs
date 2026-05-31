/// <summary>
/// NetworkRunner 기준 활성 플레이어 스냅샷 (로비/인게임 공통 조회용).
/// </summary>
public readonly struct NetworkPlayerInfo
{
    public string PlayerId { get; }
    public string DisplayName { get; }
    public bool IsHost { get; }
    public bool IsLocal { get; }

    public NetworkPlayerInfo(string playerId, string displayName, bool isHost, bool isLocal)
    {
        PlayerId = playerId;
        DisplayName = displayName;
        IsHost = isHost;
        IsLocal = isLocal;
    }
}
