using System;
using System.Collections;
using SystemEnums;
using UnityEngine;

/// <summary>
/// 인게임 씬 매니저. 손가락 배정, 라운드 타이머, RPS 판정, 스테이지/목숨 관리를 담당합니다.
/// </summary>
[DefaultExecutionOrder((int)EExecutionOrder.BaseManagement)]
public class InGameManager : SceneManagerBase
{
    public const float RoundDurationSeconds = 3f;
    public const int ROUNDS_PER_STAGE = 10;
    public const int MAX_STAGES = 10;
    public const int MAX_LIVES = 3;

    const float BETWEEN_ROUND_DELAY = 1f;
    const float SLIDE_DURATION = 0.5f;

    static readonly WaitForSeconds WAIT_OUTCOME = new(BETWEEN_ROUND_DELAY);
    static readonly WaitForSeconds WAIT_SLIDE = new(SLIDE_DURATION);

    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players => App.Game.Players;
    InputManager Input => App.SystemManager.Input;

    Coroutine _roundCoroutine;
    Coroutine _hostSetupCoroutine;
    Coroutine _betweenRoundCoroutine;
    bool _hostSetupStarted;

    int _currentStage;
    int _wonRoundsInStage;
    int _lives;
    EHandPosition _enemyHandPosition;

    public EInGamePhase Phase { get; private set; } = EInGamePhase.WaitingForSetup;
    public bool IsRoundActive => Phase == EInGamePhase.RoundInput;
    public float RoundTimeRemaining { get; private set; }
    public EFingerType LocalAssignedFinger => Players.LocalAssignedFinger;
    public bool LocalFingerExtended => Input.IsFingerExtended;
    public EHandPosition LastHandPosition { get; private set; } = EHandPosition.Invalid;
    public int CurrentStage => _currentStage;
    public int WonRoundsInStage => _wonRoundsInStage;
    public int Lives => _lives;

    public event Action<EFingerType> OnLocalAssignedFingerChanged;
    public event Action<bool> OnLocalFingerExtendedChanged;
    public event Action<float> OnRoundTimerUpdated;
    public event Action<float> OnRoundStarted;
    public event Action<EHandPosition> OnRoundJudged;
    public event Action<EOutcome> OnOutcomeDetermined;
    public event Action OnRoundResultShown;
    public event Action<int> OnLivesChanged;
    public event Action<int> OnStageClear;
    public event Action OnGameOver;

    void OnEnable()
    {
        Input.OnFingerToggled += HandleFingerToggled;
        Network.OnInGameSceneReady += HandleInGameSceneReady;
        Network.OnFingerAssignmentsReceived += HandleFingerAssignmentsReceived;
        Network.OnRoundStarted += HandleRoundStarted;
        Network.OnRoundResultReceived += HandleRoundResultReceived;
    }

    void OnDisable()
    {
        Input.OnFingerToggled -= HandleFingerToggled;
        Network.OnInGameSceneReady -= HandleInGameSceneReady;
        Network.OnFingerAssignmentsReceived -= HandleFingerAssignmentsReceived;
        Network.OnRoundStarted -= HandleRoundStarted;
        Network.OnRoundResultReceived -= HandleRoundResultReceived;
    }

    void Start()
    {
        BeginInGameSetup();
    }

    void BeginInGameSetup()
    {
        if (_hostSetupCoroutine != null)
        {
            StopCoroutine(_hostSetupCoroutine);
            _hostSetupCoroutine = null;
        }

        _hostSetupStarted = false;
        _currentStage = 1;
        _wonRoundsInStage = 0;
        _lives = MAX_LIVES;
        _enemyHandPosition = EHandPosition.Invalid;
        Phase = EInGamePhase.WaitingForSetup;
        LastHandPosition = EHandPosition.Invalid;
        RoundTimeRemaining = 0f;
        Input.SetEnabled(false);
        Input.ResetFingerState();
        Network.ResetInGameRoundNotification();
    }

    void HandleInGameSceneReady()
    {
        TryBeginHostSetup();
    }

    void TryBeginHostSetup()
    {
        if (_hostSetupStarted || !Network.IsServerHost || Phase != EInGamePhase.WaitingForSetup)
            return;

        _hostSetupStarted = true;
        _hostSetupCoroutine = StartCoroutine(HostSetupCoroutine());
    }

    /// <summary>
    /// 호스트: player object 재확보 → 손가락 배정 → 라운드 시작.
    /// 클라이언트: HandleFingerAssignmentsReceived 경로로 진행.
    /// </summary>
    IEnumerator HostSetupCoroutine()
    {
        yield return Players.ServerEnsurePlayerObjectsCoroutine();

        Network.ServerInitializeFingerAssignments();
        yield return null;

        ApplyAssignmentsReady();
        Network.ServerStartRound(RoundDurationSeconds);
        _hostSetupCoroutine = null;
    }

    void HandleFingerAssignmentsReceived()
    {
        // 스테이지 전환 시 클라이언트가 새 배정을 받을 수 있도록 phase 초기화
        if (Phase != EInGamePhase.WaitingForSetup)
            Phase = EInGamePhase.WaitingForSetup;

        ApplyAssignmentsReady();
    }

    void ApplyAssignmentsReady()
    {
        if (Phase != EInGamePhase.WaitingForSetup || LocalAssignedFinger == EFingerType.None)
            return;

        Phase = EInGamePhase.AssignmentsReady;
        OnLocalAssignedFingerChanged?.Invoke(LocalAssignedFinger);
    }

    void HandleFingerToggled(bool isExtended)
    {
        if (!IsRoundActive || LocalAssignedFinger == EFingerType.None)
            return;

        Network.ClientSendFingerState(isExtended);
        OnLocalFingerExtendedChanged?.Invoke(isExtended);

#if UNITY_EDITOR
        Debug.Log($"[InGameManager] 손가락 입력 — {Players.LocalDisplayName}, {LocalAssignedFinger}, {(isExtended ? "펴기" : "접기")}");
#endif
    }

    void HandleRoundStarted(float durationSeconds)
    {
        if (_hostSetupCoroutine != null)
        {
            StopCoroutine(_hostSetupCoroutine);
            _hostSetupCoroutine = null;
        }

        if (_roundCoroutine != null)
            StopCoroutine(_roundCoroutine);

        _roundCoroutine = StartCoroutine(RoundTimerCoroutine(durationSeconds));
    }

    IEnumerator RoundTimerCoroutine(float durationSeconds)
    {
        Phase = EInGamePhase.RoundInput;
        RoundTimeRemaining = durationSeconds;
        _enemyHandPosition = EHandPosition.Invalid;
        Input.ResetFingerState();
        Input.SetEnabled(true);
        OnLocalFingerExtendedChanged?.Invoke(false);
        OnRoundStarted?.Invoke(durationSeconds);
        OnRoundTimerUpdated?.Invoke(RoundTimeRemaining);

        while (RoundTimeRemaining > 0f)
        {
            RoundTimeRemaining -= Time.deltaTime;
            OnRoundTimerUpdated?.Invoke(Mathf.Max(0f, RoundTimeRemaining));
            yield return null;
        }

        RoundTimeRemaining = 0f;
        OnRoundTimerUpdated?.Invoke(0f);
        Input.SetEnabled(false);
        Phase = EInGamePhase.RoundJudging;

        if (Network.IsServerHost)
            Network.ServerJudgeAndBroadcastRoundResult();
    }

    void HandleRoundResultReceived(EHandPosition handPosition)
    {
        LastHandPosition = handPosition;

        EOutcome outcome = DetermineOutcome(handPosition, _enemyHandPosition);
        Phase = EInGamePhase.RoundComplete;
        OnOutcomeDetermined?.Invoke(outcome);
        OnRoundJudged?.Invoke(handPosition);

        bool isStageClear = false;

        switch (outcome)
        {
            case EOutcome.Win:
                _wonRoundsInStage++;
                if (_wonRoundsInStage >= ROUNDS_PER_STAGE)
                    isStageClear = true;
                break;

            case EOutcome.Lose:
                _lives--;
                OnLivesChanged?.Invoke(_lives);
                if (_lives <= 0)
                {
                    Phase = EInGamePhase.GameOver;
                    OnGameOver?.Invoke();
                    return;
                }
                break;

            // Draw: 상태 변경 없음, 다음 라운드로 진행
        }

        if (_betweenRoundCoroutine != null)
            StopCoroutine(_betweenRoundCoroutine);
        _betweenRoundCoroutine = StartCoroutine(BetweenRoundsCoroutine(isStageClear));
    }

    IEnumerator BetweenRoundsCoroutine(bool isStageClear)
    {
        yield return WAIT_OUTCOME;
        OnRoundResultShown?.Invoke();

        // 스테이지 상태 갱신은 모든 클라이언트에서 수행
        if (isStageClear)
        {
            int clearedStage = _currentStage;
            _currentStage++;
            _wonRoundsInStage = 0;
            OnStageClear?.Invoke(clearedStage);
        }

        if (!Network.IsServerHost) yield break;

        yield return WAIT_SLIDE;

        if (isStageClear)
        {
            if (_currentStage > MAX_STAGES)
            {
                // 전 스테이지 클리어 — 추후 구현
                Phase = EInGamePhase.StageClear;
                _betweenRoundCoroutine = null;
                yield break;
            }

            // 새 스테이지: 손가락 재배정
            Phase = EInGamePhase.WaitingForSetup;
            _hostSetupStarted = false;
            yield return Players.ServerEnsurePlayerObjectsCoroutine();
            Network.ServerInitializeFingerAssignments();
            yield return null;
            ApplyAssignmentsReady();
        }

        Network.ServerStartRound(RoundDurationSeconds);
        _betweenRoundCoroutine = null;
    }

    public void SetEnemyHandPosition(EHandPosition position)
    {
        _enemyHandPosition = position;
    }

    /// <summary>
    /// 플레이어(합산 손 모양) vs 적 손 모양으로 승/패/무승부를 판정합니다.
    /// Invalid(유효하지 않은 손 모양)는 상대가 Invalid가 아닌 이상 패배 처리됩니다.
    /// </summary>
    static EOutcome DetermineOutcome(EHandPosition player, EHandPosition enemy)
    {
        bool playerInvalid = player == EHandPosition.Invalid;
        bool enemyInvalid = enemy == EHandPosition.Invalid || enemy == EHandPosition.Random;

        if (playerInvalid && enemyInvalid) return EOutcome.Draw;
        if (playerInvalid) return EOutcome.Lose;
        if (enemyInvalid) return EOutcome.Win;

        if (player == enemy) return EOutcome.Draw;

        bool win = (player == EHandPosition.Rock     && enemy == EHandPosition.Scissors) ||
                   (player == EHandPosition.Paper    && enemy == EHandPosition.Rock)     ||
                   (player == EHandPosition.Scissors && enemy == EHandPosition.Paper);

        return win ? EOutcome.Win : EOutcome.Lose;
    }
}
