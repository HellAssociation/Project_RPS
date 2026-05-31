using System;
using SystemEnums;

/// <summary>
/// 인게임 플레이어 상태. 손가락 배정·입력 상태는 PlayerManager가 관리합니다.
/// </summary>
[Serializable]
public class GamePlayer
{
    public string PlayerId;
    public string DisplayName;
    public bool IsHost;
    public bool IsLocal;
    public EFingerType CurrFinger;
    public bool IsFingerExtended;

    public GamePlayer(string playerId, string displayName, bool isHost, bool isLocal)
    {
        PlayerId = playerId;
        DisplayName = displayName;
        IsHost = isHost;
        IsLocal = isLocal;
        CurrFinger = EFingerType.None;
        IsFingerExtended = false;
    }
}
