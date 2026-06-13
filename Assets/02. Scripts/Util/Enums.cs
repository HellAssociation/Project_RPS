using System;

namespace SystemEnums
{
    public enum EScene
    {
        Title,
        Lobby,
        PvE_1v1,
        PvE_1v5,
        PvP_1v1,
    }

    public enum EGameMode
    {
        PvE_1v1 = 2,
        PvE_1v5 = 3,
        PvP_1v1 = 4,
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
        WaitingForRoundSelect,
        WaitingForSetup,
        CardVoting,
        BossIntro,
        AssignmentsReady,
        WaveInput,
        WaveJudging,
        WaveComplete,
        GameOver,
        RoundClear,
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
        Versus,
        HUD,
        ForbidHandShape,
        Wave,
        Indicate,
        Modal,
        CardVote,
        Boss,
    }

    /// <summary>
    /// 스타포스류 미니게임에서 플레이어를 구분하기 위한 색상. Frame과 Star에 동일하게 적용됩니다.
    /// </summary>
    public enum EStarColor
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
    }

    public enum EStarforceResult
    {
        None,
        Success,
        Fail,
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

    public enum EEffect
    {
        None,
        CameraImpulse,
    }

    public enum EInGameRpsMessage : byte
    {
        StartRound = 1,
        RoundResult = 2,
        StageSelected = 3,
        CardsApplied = 4,
        CardOffer = 5,
        CardVoteResult = 6,
        BossIntro = 7,
    }

    public enum EBoon { BOON_A, BOON_B, BOON_C }

    public enum EDeviation { BOON_A, BOON_B, BOON_C }

    public enum EDefine
    {
        DEFINE_PLAYER_DEFAULT_HP,
        DEFINE_PLAYER_DEFAULT_DAMAGE,
        DEFINE_PLAYER_DEFAULT_TIMER,
    }

    /// <summary>RunState가 수정자 파이프라인으로 해석하는 스탯 종류.</summary>
    public enum EStat
    {
        PlayerMaxHp,
        PlayerDamage,
        WaveTimer,
        EnemyHpMultiplier,
    }

    public enum EModifierOp { Add, Mul }

    public enum ECardKind : byte { Boon, Deviation }

    public enum EEnemyType
    {
        ENEMY_TYPE_1,  ENEMY_TYPE_2,  ENEMY_TYPE_3,
        ENEMY_TYPE_4,  ENEMY_TYPE_5,  ENEMY_TYPE_6,
        ENEMY_TYPE_7,  ENEMY_TYPE_8,  ENEMY_TYPE_9,
        ENEMY_TYPE_10,
    }

    public enum ERound
    {
        ROUND_1,  ROUND_2,  ROUND_3,  ROUND_4,  ROUND_5,
        ROUND_6,  ROUND_7,  ROUND_8,  ROUND_9,  ROUND_10,
    }
}
