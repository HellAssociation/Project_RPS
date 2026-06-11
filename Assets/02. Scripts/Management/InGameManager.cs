using System;
using System.Collections;
using SystemEnums;
using UnityEngine;

[DefaultExecutionOrder((int)EExecutionOrder.BaseManagement)]
public class InGameManager : SceneManagerBase
{
    public const int WAVES_PER_ROUND = 10;
    public const int MAX_ROUNDS = 10;
    public const int ENEMY_DAMAGE = 40;

    const float BETWEEN_WAVE_DELAY = 1f;

    static DataManager Data => App.Data.BaseData;

    public int MaxLives
    {
        get
        {
            if (Data != null && Data.TryGetDefine(EDefine.DEFINE_PLAYER_DEFAULT_HP, out DefineData d))
                return d.value;
            return PlayerManager.MAX_HP;
        }
    }

    float GetWaveDuration(int round)
    {
        float baseDuration = (Data != null && Data.TryGetDefine(EDefine.DEFINE_PLAYER_DEFAULT_TIMER, out DefineData timer))
            ? timer.value : 3f;
        if (Data != null && Data.TryGetRound((ERound)(round - 1), out RoundData rd))
            baseDuration += rd.roundTimer;
        return baseDuration;
    }

    int PlayerDamage
    {
        get
        {
            if (Data != null && Data.TryGetDefine(EDefine.DEFINE_PLAYER_DEFAULT_DAMAGE, out DefineData d))
                return d.value;
            return 100;
        }
    }

    int GetEnemyMaxHp(int round)
    {
        int baseHp = MaxLives;
        if (Data != null && Data.TryGetRound((ERound)(round - 1), out RoundData rd))
            return Mathf.RoundToInt(baseHp * rd.roundHPMultiflier);
        return baseHp;
    }

    void InitEnemyHp()
    {
        _enemyMaxHp = GetEnemyMaxHp(_currentRound);
        _enemyHp    = _enemyMaxHp;
    }
    const float SLIDE_DURATION = 0.5f;

    static readonly WaitForSeconds WAIT_OUTCOME = new(BETWEEN_WAVE_DELAY);
    static readonly WaitForSeconds WAIT_SLIDE   = new(SLIDE_DURATION);

    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players  => App.Game.Players;
    InputManager Input     => App.SystemManager.Input;

    Coroutine _waveCoroutine;
    Coroutine _hostSetupCoroutine;
    Coroutine _betweenWaveCoroutine;
    bool _hostSetupStarted;

    int _currentRound;
    int _wonWavesInRound;
    int _lives;
    int _enemyHp;
    int _enemyMaxHp;
    int _selectedStageIndex;
    EHandPosition _enemyHandPosition;
    StagePanel _stagePanel;
    VersusPanel _versusPanel;

    public EInGamePhase Phase { get; private set; } = EInGamePhase.WaitingForRoundSelect;
    public bool IsWaveActive => Phase == EInGamePhase.WaveInput;
    public float WaveTimeRemaining { get; private set; }
    public EFingerType LocalAssignedFinger => Players.LocalAssignedFinger;
    public bool LocalFingerExtended => Input.IsFingerExtended;
    public EHandPosition LastHandPosition { get; private set; } = EHandPosition.Invalid;
    public int CurrentRound => _currentRound;
    public int WonWavesInRound => _wonWavesInRound;
    public int Lives => _lives;
    public int SelectedStageIndex => _selectedStageIndex;

    public event Action<EFingerType> OnLocalAssignedFingerChanged;
    public event Action<bool>        OnLocalFingerExtendedChanged;
    public event Action<EFingerType> OnLocalFingerMaskChanged;
    public event Action<float>       OnWaveTimerUpdated;
    public event Action<int>         OnStageOpened;
    public event Action              OnReadyStarted;
    public event Action<float>       OnWaveStarted;
    public event Action<EHandPosition> OnWaveJudged;
    public event Action<EOutcome>    OnOutcomeDetermined;
    public event Action              OnWaveResultShown;
    public event Action<int>         OnLivesChanged;
    public event Action<int, int>    OnEnemyHpChanged;
    public event Action<int>         OnCurrentRoundChanged;
    public event Action<int>         OnRoundClear;
    public event Action              OnGameOver;

    void OnEnable()
    {
        Input.OnFingerToggled               += HandleFingerToggled;
        Input.OnSingleControlMaskChanged    += HandleSingleControlMaskChanged;
        Network.OnInGameSceneReady          += HandleInGameSceneReady;
        Network.OnFingerAssignmentsReceived += HandleFingerAssignmentsReceived;
        Network.OnRoundStarted              += HandleWaveStarted;
        Network.OnRoundResultReceived       += HandleWaveResultReceived;
        Network.OnStageSelected             += HandleRoundSelected;
    }

    void OnDisable()
    {
        Input.OnFingerToggled               -= HandleFingerToggled;
        Input.OnSingleControlMaskChanged    -= HandleSingleControlMaskChanged;
        Network.OnInGameSceneReady          -= HandleInGameSceneReady;
        Network.OnFingerAssignmentsReceived -= HandleFingerAssignmentsReceived;
        Network.OnRoundStarted              -= HandleWaveStarted;
        Network.OnRoundResultReceived       -= HandleWaveResultReceived;
        Network.OnStageSelected             -= HandleRoundSelected;
    }

    void Start()
    {
        CachePanels();
        BeginInGameSetup();
    }

    void CachePanels()
    {
        if (!App.UI.InGame.TryGetPanel(out _stagePanel))
        {
#if UNITY_EDITOR
            Debug.LogError("[Error] Can't find stage panel!");
#endif
        }

        if (!App.UI.InGame.TryGetPanel(out _versusPanel))
        {
#if UNITY_EDITOR
            Debug.LogError("[Error] Can't find versus panel!");
#endif
        }
    }

    void BeginInGameSetup()
    {
        if (_hostSetupCoroutine != null)
        {
            StopCoroutine(_hostSetupCoroutine);
            _hostSetupCoroutine = null;
        }

        _hostSetupStarted  = false;
        _currentRound      = 1;
        _wonWavesInRound   = 0;
        _lives             = MaxLives;
        _enemyHp           = 0;
        _enemyMaxHp        = 0;
        _enemyHandPosition = EHandPosition.Invalid;
        Phase              = EInGamePhase.WaitingForRoundSelect;
        LastHandPosition   = EHandPosition.Invalid;
        WaveTimeRemaining  = 0f;
        Input.SetEnabled(false);
        Input.ResetFingerState();
        Network.ResetInGameRoundNotification();
    }

    void HandleInGameSceneReady()
    {
        if (Network.IsServerHost)
            StartCoroutine(EarlySpawnPlayerObjectsCoroutine());
    }

    IEnumerator EarlySpawnPlayerObjectsCoroutine()
    {
        yield return Players.ServerEnsurePlayerObjectsCoroutine();
    }

    void HandleRoundSelected(int stageIndex)
    {
        _selectedStageIndex = stageIndex;

        if (_stagePanel != null) _stagePanel.ClosePanel();
        if (Phase == EInGamePhase.WaitingForRoundSelect)
            Phase = EInGamePhase.WaitingForSetup;

        OnStageOpened?.Invoke(stageIndex);
        TryBeginHostSetup();
    }

    void TryBeginHostSetup()
    {
        if (_hostSetupStarted || !Network.IsServerHost || Phase != EInGamePhase.WaitingForSetup)
            return;

        _hostSetupStarted   = true;
        _hostSetupCoroutine = StartCoroutine(HostSetupCoroutine());
    }

    IEnumerator HostSetupCoroutine()
    {
        yield return Players.ServerEnsurePlayerObjectsCoroutine();

        Network.ServerInitializeFingerAssignments();
        yield return null;

        ApplyAssignmentsReady();

        if (_versusPanel != null)
            yield return new WaitWhile(() => _versusPanel.IsAnimating);

        Network.ServerStartRound(GetWaveDuration(_currentRound));
        _hostSetupCoroutine = null;
    }

    void HandleFingerAssignmentsReceived()
    {
        if (Phase != EInGamePhase.WaitingForSetup)
            Phase = EInGamePhase.WaitingForSetup;

        ApplyAssignmentsReady();
    }

    void ApplyAssignmentsReady()
    {
        if (Phase != EInGamePhase.WaitingForSetup || LocalAssignedFinger == EFingerType.None)
            return;

        Phase = EInGamePhase.AssignmentsReady;
        InitEnemyHp();
        OnLocalAssignedFingerChanged?.Invoke(LocalAssignedFinger);
        OnReadyStarted?.Invoke();
        if (_versusPanel != null) _versusPanel.OpenPanel();
    }

    void HandleFingerToggled(bool isExtended)
    {
        if (!IsWaveActive || LocalAssignedFinger == EFingerType.None)
            return;

        Network.ClientSendFingerState(isExtended);
        OnLocalFingerExtendedChanged?.Invoke(isExtended);

#if UNITY_EDITOR
        Debug.Log($"[InGameManager] 손가락 입력 — {Players.LocalDisplayName}, {LocalAssignedFinger}, {(isExtended ? "펴기" : "접기")}");
#endif
    }

    void HandleSingleControlMaskChanged(EFingerType mask)
    {
        if (!IsWaveActive)
            return;

        OnLocalFingerMaskChanged?.Invoke(mask);
    }

    void HandleWaveStarted(float durationSeconds)
    {
        if (_hostSetupCoroutine != null)
        {
            StopCoroutine(_hostSetupCoroutine);
            _hostSetupCoroutine = null;
        }

        if (_waveCoroutine != null)
            StopCoroutine(_waveCoroutine);

        _waveCoroutine = StartCoroutine(WaveTimerCoroutine(durationSeconds));
    }

    IEnumerator WaveTimerCoroutine(float durationSeconds)
    {
        Phase             = EInGamePhase.WaveInput;
        WaveTimeRemaining = durationSeconds;
        _enemyHandPosition = EHandPosition.Invalid;
        Input.ResetFingerState();
        Input.SetEnabled(true);
        OnLocalFingerExtendedChanged?.Invoke(false);
        OnWaveStarted?.Invoke(durationSeconds);
        OnWaveTimerUpdated?.Invoke(WaveTimeRemaining);

        while (WaveTimeRemaining > 0f)
        {
            WaveTimeRemaining -= Time.deltaTime;
            OnWaveTimerUpdated?.Invoke(Mathf.Max(0f, WaveTimeRemaining));
            yield return null;
        }

        WaveTimeRemaining = 0f;
        OnWaveTimerUpdated?.Invoke(0f);
        Input.SetEnabled(false);
        Phase = EInGamePhase.WaveJudging;

        if (Network.IsServerHost)
            Network.ServerJudgeAndBroadcastRoundResult();
    }

    void HandleWaveResultReceived(EHandPosition handPosition)
    {
        LastHandPosition = handPosition;

        EOutcome outcome = DetermineOutcome(handPosition, _enemyHandPosition);
        Phase = EInGamePhase.WaveComplete;
        OnOutcomeDetermined?.Invoke(outcome);
        OnWaveJudged?.Invoke(handPosition);

        bool isRoundClear = false;
        bool isGameOver   = false;

        switch (outcome)
        {
            case EOutcome.Win:
                _enemyHp -= PlayerDamage;
                OnEnemyHpChanged?.Invoke(Mathf.Max(_enemyHp, 0), _enemyMaxHp);
                if (_enemyHp <= 0)
                    _wonWavesInRound++;
                if (_wonWavesInRound >= WAVES_PER_ROUND)
                    isRoundClear = true;
                break;

            case EOutcome.Lose:
                _lives -= ENEMY_DAMAGE;
                OnLivesChanged?.Invoke(_lives);
                if (_lives <= 0)
                {
                    Phase       = EInGamePhase.GameOver;
                    isGameOver  = true;
                }
                break;
        }

        if (_betweenWaveCoroutine != null)
            StopCoroutine(_betweenWaveCoroutine);
        _betweenWaveCoroutine = StartCoroutine(BetweenWavesCoroutine(isRoundClear, isGameOver));
    }

    IEnumerator BetweenWavesCoroutine(bool isRoundClear, bool isGameOver)
    {
        yield return WAIT_OUTCOME;
        OnWaveResultShown?.Invoke();

        if (!isRoundClear && !isGameOver && _enemyHp <= 0)
        {
            _enemyHp = _enemyMaxHp;
            OnEnemyHpChanged?.Invoke(_enemyHp, _enemyMaxHp);
        }

        if (isGameOver)
        {
            Input.ResetFingerState();
            OnLocalFingerExtendedChanged?.Invoke(false);
            OnGameOver?.Invoke();

            _lives           = MaxLives;
            _wonWavesInRound = 0;
            _currentRound    = 1;
            _hostSetupStarted = false;
            Phase            = EInGamePhase.WaitingForRoundSelect;
            OnLivesChanged?.Invoke(_lives);
            OnCurrentRoundChanged?.Invoke(_currentRound);

            if (_stagePanel != null) _stagePanel.OpenPanel();
            _betweenWaveCoroutine = null;
            yield break;
        }

        if (isRoundClear)
        {
            int clearedRound = _currentRound;
            _currentRound++;
            _wonWavesInRound = 0;
            OnCurrentRoundChanged?.Invoke(_currentRound);
            OnRoundClear?.Invoke(clearedRound);
        }

        if (!Network.IsServerHost) yield break;

        yield return WAIT_SLIDE;

        if (isRoundClear)
        {
            if (_currentRound > MAX_ROUNDS)
            {
                Phase = EInGamePhase.RoundClear;
                _betweenWaveCoroutine = null;
                yield break;
            }

            Phase             = EInGamePhase.WaitingForSetup;
            _hostSetupStarted = false;
            yield return Players.ServerEnsurePlayerObjectsCoroutine();
            Network.ServerInitializeFingerAssignments();
            yield return null;
            ApplyAssignmentsReady();
        }

        if (_versusPanel != null && _versusPanel.IsAnimating)
            yield return new WaitWhile(() => _versusPanel.IsAnimating);

        Network.ServerStartRound(GetWaveDuration(_currentRound));
        _betweenWaveCoroutine = null;
    }

    public void SetEnemyHandPosition(EHandPosition position)
    {
        _enemyHandPosition = position;
    }

    static EOutcome DetermineOutcome(EHandPosition player, EHandPosition enemy)
    {
        bool playerInvalid = player == EHandPosition.Invalid;
        bool enemyInvalid  = enemy == EHandPosition.Invalid || enemy == EHandPosition.Random;

        if (playerInvalid && enemyInvalid) return EOutcome.Draw;
        if (playerInvalid) return EOutcome.Lose;
        if (enemyInvalid)  return EOutcome.Win;

        if (player == enemy) return EOutcome.Draw;

        bool win = (player == EHandPosition.Rock     && enemy == EHandPosition.Scissors) ||
                   (player == EHandPosition.Paper    && enemy == EHandPosition.Rock)     ||
                   (player == EHandPosition.Scissors && enemy == EHandPosition.Paper);

        return win ? EOutcome.Win : EOutcome.Lose;
    }
}
