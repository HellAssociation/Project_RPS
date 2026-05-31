using System;

namespace SystemEnums
{
    public enum EScene
    {
        Title,
        Lobby,
        InGame,
    }

    [Flags]
    public enum EFingerType
    {
        None = 0,
        Thumb = 1 << 0,
        Index = 1 << 1,
        Middle = 1 << 2,
        Ring = 1 << 3,
        Pinky = 1 << 4,

        All = Thumb | Index | Middle | Ring | Pinky,
    }

    /// <summary>
    /// RPS 판정 결과. Rock/Paper/Scissors 값은 EFingerType 비트 마스크와 동일합니다.
    /// </summary>
    public enum EHandPosition
    {
        Invalid = 255,
        Random = 254,
        Rock = EFingerType.None,
        Paper = EFingerType.All,
        Scissors = EFingerType.Index | EFingerType.Middle,
    }

    public enum EOutcome
    {
        Win,
        Lose,
        Draw,
    }

    public enum EInGamePhase
    {
        WaitingForSetup,
        AssignmentsReady,
        RoundInput,
        RoundJudging,
        RoundComplete,
        GameOver,
        StageClear,
    }

    public enum ELobbyState
    {
        Disconnected,
        InLobby,
        Starting,
    }

    public enum EExecutionOrder
    {
        None,
        BaseManagement,
        SystemManagement,
        UIManagement,
        GameManagement,
        SystemHandler,
        GameContent,
    }

    public enum EUIType
    {
        None,
        Title,
        LobbyEntry,
        LobbyRoom,

        // Game
        Timer,
        NextIcon,
        OutCome,
        Stage,
    }

    /// <summary>
    /// 오디오 클립 식별자. Addressables 주소는 열거형 이름과 동일합니다.
    /// </summary>
    public enum EAudioClip
    {
        None,
        BGM_MAIN,
        BGM_NEWS,
    }

    public enum EInGameRpsMessage : byte
    {
        StartRound = 1,
        RoundResult = 2,
    }
}
