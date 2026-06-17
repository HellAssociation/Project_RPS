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
        Stage,
        Versus,
        HUD,
        ForbidHandShape,
        Wave,
        Indicate,
        StarforceModal,
        CardVote,
        Boss,
        CardIcon,
        KeyControl,
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
        BGM_Battle,
        SFX_Ready,
        SFX_Fight,
        SFX_KO_01,
        SFX_KO_02,
        SFX_KO_03,
        SFX_KO_04,
        SFX_Attack_01,
        SFX_Attack_02,
        SFX_Attack_03,
    }

    public enum EEffect
    {
        None,
        CameraImpulse,
        AttackImpact,
        HitImpact,
        HudPlayerHit,
        HudEnemyHit,
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
        RoundClear = 8,
        RewardDone = 9,
    }

    public enum EBoon
    {
        Boon_Training,
        Boon_Gunslinger,
        Boon_ZombieHand,
        Boon_Rockstar,
        Boon_Reflexes,
        Boon_GamblerHand,
        Boon_OraOra,
        Boon_MudaMuda,
        Boon_ScissorsBreakRock,
        Boon_HeavyPunch,
        Boon_PaperCounter,
        Boon_BlackFlashRock,
        Boon_BlackFlashScissors,
        Boon_BlackFlashPaper,
        Boon_FxxkU,
        Boon_Chance,
        Boon_Luck,
        Boon_Preparation,
        Boon_Purification,
        Boon_Amaterasu,
        Boon_Waterfall,
        Boon_Lightning,
        Boon_ThatFace,
        Boon_Insight,
        Boon_SeatTaken,
        Boon_Diamond,
        Boon_AlphaMuk,
        Boon_RecklessCharge,
        Boon_SubstitutionJutsu,
    }

    public enum EDeviation
    {
        Deviation_Arthritis,
        Deviation_KeyChaos,
        Deviation_Fracture,
        Deviation_TremorThumb,
        Deviation_TremorIndex,
        Deviation_TremorMiddle,
        Deviation_TremorRing,
        Deviation_TremorPinky,
        Deviation_Haste,
        Deviation_Collar,
        Deviation_LightsOut,
        Deviation_CarpalTunnel,
        Deviation_Sandbag,
        Deviation_Log,
        Deviation_NumbersAdvantage,
        Deviation_ThumbsUp,
        Deviation_Pacifist,
        Deviation_Caveman,
        Deviation_Lumberjack,
        Deviation_Guppy,
        Deviation_FxxkUToo,
        Deviation_FingerDoctor,
        Deviation_IndustrialSpy,
        Deviation_OldManPattern,
        Deviation_ADHD,
        Deviation_DoubleLifeLoss,
        Deviation_Doppelganger,
        Deviation_Mutation,
        Deviation_WeAreOne,
        Deviation_SlowHand,
        Deviation_TapDancer,
        Deviation_Corruption,
        Deviation_YouDoNotListenToRock,
    }

    /// <summary>Identifies a card's gameplay effect; bridged from a card's data code by name.</summary>
    public enum ECardEffect
    {
        Boon_Training,
        Boon_Gunslinger,
        Boon_ZombieHand,
        Boon_Rockstar,
        Boon_Reflexes,
        Boon_GamblerHand,
        Boon_OraOra,
        Boon_MudaMuda,
        Boon_ScissorsBreakRock,
        Boon_HeavyPunch,
        Boon_PaperCounter,
        Boon_BlackFlashRock,
        Boon_BlackFlashScissors,
        Boon_BlackFlashPaper,
        Boon_FxxkU,
        Boon_Chance,
        Boon_Luck,
        Boon_Preparation,
        Boon_Purification,
        Boon_Amaterasu,
        Boon_Waterfall,
        Boon_Lightning,
        Boon_ThatFace,
        Boon_Insight,
        Boon_SeatTaken,
        Boon_Diamond,
        Boon_AlphaMuk,
        Boon_RecklessCharge,
        Boon_SubstitutionJutsu,
        Deviation_Arthritis,
        Deviation_KeyChaos,
        Deviation_Fracture,
        Deviation_TremorThumb,
        Deviation_TremorIndex,
        Deviation_TremorMiddle,
        Deviation_TremorRing,
        Deviation_TremorPinky,
        Deviation_Haste,
        Deviation_Collar,
        Deviation_LightsOut,
        Deviation_CarpalTunnel,
        Deviation_Sandbag,
        Deviation_Log,
        Deviation_NumbersAdvantage,
        Deviation_ThumbsUp,
        Deviation_Pacifist,
        Deviation_Caveman,
        Deviation_Lumberjack,
        Deviation_Guppy,
        Deviation_FxxkUToo,
        Deviation_FingerDoctor,
        Deviation_IndustrialSpy,
        Deviation_OldManPattern,
        Deviation_ADHD,
        Deviation_DoubleLifeLoss,
        Deviation_Doppelganger,
        Deviation_Mutation,
        Deviation_WeAreOne,
        Deviation_SlowHand,
        Deviation_TapDancer,
        Deviation_Corruption,
        Deviation_YouDoNotListenToRock,
    }

    public enum EStringType
    {
        STR_NAME_BOON_TRAINING,
        STR_NAME_BOON_GUNSLINGER,
        STR_NAME_BOON_ZOMBIE_HAND,
        STR_NAME_BOON_ROCKSTAR,
        STR_NAME_BOON_REFLEXES,
        STR_NAME_BOON_GAMBLER_HAND,
        STR_NAME_BOON_ORA_ORA,
        STR_NAME_BOON_MUDA_MUDA,
        STR_NAME_BOON_SCISSORS_BREAK_ROCK,
        STR_NAME_BOON_HEAVY_PUNCH,
        STR_NAME_BOON_PAPER_COUNTER,
        STR_NAME_BOON_BLACK_FLASH_ROCK,
        STR_NAME_BOON_BLACK_FLASH_SCISSORS,
        STR_NAME_BOON_BLACK_FLASH_PAPER,
        STR_NAME_BOON_FXXK_U,
        STR_NAME_BOON_CHANCE,
        STR_NAME_BOON_LUCK,
        STR_NAME_BOON_PREPARATION,
        STR_NAME_BOON_PURIFICATION,
        STR_NAME_BOON_AMATERASU,
        STR_NAME_BOON_WATERFALL,
        STR_NAME_BOON_LIGHTNING,
        STR_NAME_BOON_THAT_FACE,
        STR_NAME_BOON_INSIGHT,
        STR_NAME_BOON_SEAT_TAKEN,
        STR_NAME_BOON_DIAMOND,
        STR_NAME_BOON_ALPHA_MUK,
        STR_NAME_BOON_RECKLESS_CHARGE,
        STR_NAME_BOON_SUBSTITUTION_JUTSU,
        STR_NAME_DEVIATION_ARTHRITIS,
        STR_NAME_DEVIATION_KEY_CHAOS,
        STR_NAME_DEVIATION_FRACTURE,
        STR_NAME_DEVIATION_TREMOR_THUMB,
        STR_NAME_DEVIATION_TREMOR_INDEX,
        STR_NAME_DEVIATION_TREMOR_MIDDLE,
        STR_NAME_DEVIATION_TREMOR_RING,
        STR_NAME_DEVIATION_TREMOR_PINKY,
        STR_NAME_DEVIATION_HASTE,
        STR_NAME_DEVIATION_COLLAR,
        STR_NAME_DEVIATION_LIGHTS_OUT,
        STR_NAME_DEVIATION_CARPAL_TUNNEL,
        STR_NAME_DEVIATION_SANDBAG,
        STR_NAME_DEVIATION_LOG,
        STR_NAME_DEVIATION_NUMBERS_ADVANTAGE,
        STR_NAME_DEVIATION_THUMBS_UP,
        STR_NAME_DEVIATION_PACIFIST,
        STR_NAME_DEVIATION_CAVEMAN,
        STR_NAME_DEVIATION_LUMBERJACK,
        STR_NAME_DEVIATION_GUPPY,
        STR_NAME_DEVIATION_FXXK_U_TOO,
        STR_NAME_DEVIATION_FINGER_DOCTOR,
        STR_NAME_DEVIATION_INDUSTRIAL_SPY,
        STR_NAME_DEVIATION_OLD_MAN_PATTERN,
        STR_NAME_DEVIATION_ADHD,
        STR_NAME_DEVIATION_DOUBLE_LIFE_LOSS,
        STR_NAME_DEVIATION_DOPPELGANGER,
        STR_NAME_DEVIATION_MUTATION,
        STR_NAME_DEVIATION_WE_ARE_ONE,
        STR_NAME_DEVIATION_SLOW_HAND,
        STR_NAME_DEVIATION_TAP_DANCER,
        STR_NAME_DEVIATION_CORRUPTION,
        STR_NAME_DEVIATION_YOU_DO_NOT_LISTEN_TO_ROCK,
        STR_DESC_BOON_TRAINING,
        STR_DESC_BOON_GUNSLINGER,
        STR_DESC_BOON_ZOMBIE_HAND,
        STR_DESC_BOON_ROCKSTAR,
        STR_DESC_BOON_REFLEXES,
        STR_DESC_BOON_GAMBLER_HAND,
        STR_DESC_BOON_ORA_ORA,
        STR_DESC_BOON_MUDA_MUDA,
        STR_DESC_BOON_SCISSORS_BREAK_ROCK,
        STR_DESC_BOON_HEAVY_PUNCH,
        STR_DESC_BOON_PAPER_COUNTER,
        STR_DESC_BOON_BLACK_FLASH_ROCK,
        STR_DESC_BOON_BLACK_FLASH_SCISSORS,
        STR_DESC_BOON_BLACK_FLASH_PAPER,
        STR_DESC_BOON_FXXK_U,
        STR_DESC_BOON_CHANCE,
        STR_DESC_BOON_LUCK,
        STR_DESC_BOON_PREPARATION,
        STR_DESC_BOON_PURIFICATION,
        STR_DESC_BOON_AMATERASU,
        STR_DESC_BOON_WATERFALL,
        STR_DESC_BOON_LIGHTNING,
        STR_DESC_BOON_THAT_FACE,
        STR_DESC_BOON_INSIGHT,
        STR_DESC_BOON_SEAT_TAKEN,
        STR_DESC_BOON_DIAMOND,
        STR_DESC_BOON_ALPHA_MUK,
        STR_DESC_BOON_RECKLESS_CHARGE,
        STR_DESC_BOON_SUBSTITUTION_JUTSU,
        STR_DESC_DEVIATION_ARTHRITIS,
        STR_DESC_DEVIATION_KEY_CHAOS,
        STR_DESC_DEVIATION_FRACTURE,
        STR_DESC_DEVIATION_TREMOR_THUMB,
        STR_DESC_DEVIATION_TREMOR_INDEX,
        STR_DESC_DEVIATION_TREMOR_MIDDLE,
        STR_DESC_DEVIATION_TREMOR_RING,
        STR_DESC_DEVIATION_TREMOR_PINKY,
        STR_DESC_DEVIATION_HASTE,
        STR_DESC_DEVIATION_COLLAR,
        STR_DESC_DEVIATION_LIGHTS_OUT,
        STR_DESC_DEVIATION_CARPAL_TUNNEL,
        STR_DESC_DEVIATION_SANDBAG,
        STR_DESC_DEVIATION_LOG,
        STR_DESC_DEVIATION_NUMBERS_ADVANTAGE,
        STR_DESC_DEVIATION_THUMBS_UP,
        STR_DESC_DEVIATION_PACIFIST,
        STR_DESC_DEVIATION_CAVEMAN,
        STR_DESC_DEVIATION_LUMBERJACK,
        STR_DESC_DEVIATION_GUPPY,
        STR_DESC_DEVIATION_FXXK_U_TOO,
        STR_DESC_DEVIATION_FINGER_DOCTOR,
        STR_DESC_DEVIATION_INDUSTRIAL_SPY,
        STR_DESC_DEVIATION_OLD_MAN_PATTERN,
        STR_DESC_DEVIATION_ADHD,
        STR_DESC_DEVIATION_DOUBLE_LIFE_LOSS,
        STR_DESC_DEVIATION_DOPPELGANGER,
        STR_DESC_DEVIATION_MUTATION,
        STR_DESC_DEVIATION_WE_ARE_ONE,
        STR_DESC_DEVIATION_SLOW_HAND,
        STR_DESC_DEVIATION_TAP_DANCER,
        STR_DESC_DEVIATION_CORRUPTION,
        STR_DESC_DEVIATION_YOU_DO_NOT_LISTEN_TO_ROCK,
    }

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
