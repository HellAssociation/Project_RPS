using System;
using System.Collections;
using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

[DefaultExecutionOrder((int)EExecutionOrder.BaseManagement)]
public class InGameManager : SceneManagerBase
{
    public const int WAVES_PER_ROUND = 10;
    public const int MAX_ROUNDS = 10;
    public const int ENEMY_DAMAGE = 40;
    public const int CARDS_PER_VOTE = 3;

    const float BETWEEN_WAVE_DELAY = 1f;
    const float VOTE_SECONDS = 10f;
    const float VOTE_REVEAL_SECONDS = 1.5f;
    const float BOSS_INTRO_SECONDS = 3f;

    const float IMPACT_SLOW_TIME_SCALE = 0.25f;
    const float IMPACT_SLOW_HOLD = 0.25f;
    const float IMPACT_SLOW_RECOVER = 0.35f;

    readonly RunState _run = new();
    readonly CardEffectRuntime _effects = new();
    int _lastEffectRound;

    System.Random _voteRng;
    readonly int[] _voteTally = new int[CARDS_PER_VOTE];
    readonly List<int> _voteWinners = new(CARDS_PER_VOTE);
    readonly List<CardRef> _voteChosen = new(2);

    readonly List<CardRef> _acquiredCards = new();

    public int MaxLives => _run.MaxLives;
    public IReadOnlyList<CardRef> AcquiredCards => _acquiredCards;

    const float SLIDE_DURATION = 0.5f;

    static readonly WaitForSeconds WAIT_OUTCOME = new(BETWEEN_WAVE_DELAY);
    static readonly WaitForSeconds WAIT_SLIDE = new(SLIDE_DURATION);
    static readonly WaitForSeconds WAIT_VOTE_REVEAL = new(VOTE_REVEAL_SECONDS);
    static readonly WaitForSeconds WAIT_BOSS_INTRO = new(BOSS_INTRO_SECONDS);
    static readonly WaitForSecondsRealtime WAIT_IMPACT_SLOW = new(IMPACT_SLOW_HOLD);

    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players => App.Game.Players;
    InputManager Input => App.SystemManager.Input;

    Coroutine _waveCoroutine;
    Coroutine _hostSetupCoroutine;
    Coroutine _betweenWaveCoroutine;
    Coroutine _slowMoCoroutine;
    Action _pendingImpactFeedback;
    bool _pendingKillSlowMo;
    bool _hostSetupStarted;
    bool _rewardDone;

    int _selectedStageIndex;
    int _enemySeed;
    EHandPosition _enemyHandPosition;
    StagePanel _stagePanel;
    VersusPanel _versusPanel;

    public EInGamePhase Phase { get; private set; } = EInGamePhase.WaitingForRoundSelect;
    public bool IsWaveActive => Phase == EInGamePhase.WaveInput;
    public float WaveTimeRemaining { get; private set; }
    public EFingerType LocalAssignedFinger => Players.LocalAssignedFinger;
    public bool LocalFingerExtended => Input.IsFingerExtended;
    public EHandPosition LastHandPosition { get; private set; } = EHandPosition.Invalid;
    public int CurrentRound => _run.CurrentRound;
    public int WonWavesInRound => _run.WonWavesInRound;
    public int Lives => _run.Lives;
    public int SelectedStageIndex => _selectedStageIndex;
    public int EnemySeed => _enemySeed;

    public event Action<EFingerType> OnLocalAssignedFingerChanged;
    public event Action<bool> OnLocalFingerExtendedChanged;
    public event Action<EFingerType> OnLocalFingerMaskChanged;
    public event Action<float> OnWaveTimerUpdated;
    public event Action<int> OnStageOpened;
    public event Action OnReadyStarted;
    public event Action<float> OnWaveStarted;
    public event Action<EHandPosition> OnWaveJudged;
    public event Action<EOutcome> OnOutcomeDetermined;
    public event Action OnWaveResultShown;
    public event Action<int> OnLivesChanged;
    public event Action<int, int> OnEnemyHpChanged;
    public event Action<bool> OnEnemyAppeared;
    public event Action<int> OnCurrentRoundChanged;
    public event Action<int> OnRoundClear;
    public event Action OnGameOver;
    public event Action OnGameClear;
    public event Action OnAcquiredCardsChanged;

    void OnEnable()
    {
        Input.OnFingerToggled += HandleFingerToggled;
        Input.OnSingleControlMaskChanged += HandleSingleControlMaskChanged;
        Network.OnInGameSceneReady += HandleInGameSceneReady;
        Network.OnFingerAssignmentsReceived += HandleFingerAssignmentsReceived;
        Network.OnRoundStarted += HandleWaveStarted;
        Network.OnRoundResultReceived += HandleWaveResultReceived;
        Network.OnStageSelected += HandleRoundSelected;
        Network.OnCardsApplied += HandleCardsApplied;
        Network.OnRoundCleared += HandleRoundClear;
        Network.OnRewardDone += HandleRewardDone;
        HandImpactHitbox.OnImpactLanded += HandleImpactLanded;
    }

    void OnDisable()
    {
        Input.OnFingerToggled -= HandleFingerToggled;
        Input.OnSingleControlMaskChanged -= HandleSingleControlMaskChanged;
        Network.OnInGameSceneReady -= HandleInGameSceneReady;
        Network.OnFingerAssignmentsReceived -= HandleFingerAssignmentsReceived;
        Network.OnRoundStarted -= HandleWaveStarted;
        Network.OnRoundResultReceived -= HandleWaveResultReceived;
        Network.OnStageSelected -= HandleRoundSelected;
        Network.OnCardsApplied -= HandleCardsApplied;
        Network.OnRoundCleared -= HandleRoundClear;
        Network.OnRewardDone -= HandleRewardDone;
        HandImpactHitbox.OnImpactLanded -= HandleImpactLanded;
    }

    void Start()
    {
        CachePanels();
        _effects.Bind(Input);
        StartCoroutine(WaitDataThenSetup());
    }

    IEnumerator WaitDataThenSetup()
    {
        DataManager data = App.Data.BaseData;
        if (data != null && !data.IsDataLoaded)
            yield return new WaitUntil(() => data.IsDataLoaded);
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

        _hostSetupStarted = false;
        _run.Reset();
        _effects.Clear();
        _lastEffectRound = 0;
        _acquiredCards.Clear();
        _enemyHandPosition = EHandPosition.Invalid;
        Phase = EInGamePhase.WaitingForRoundSelect;
        LastHandPosition = EHandPosition.Invalid;
        WaveTimeRemaining = 0f;
        Input.SetEnabled(false);
        Input.ResetFingerState();
        Network.ResetInGameRoundNotification();

        App.UI.InGame.TryGetPanel(out _stagePanel);
        _stagePanel.OpenPanel();
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

    void HandleRoundSelected(int stageIndex, int seed)
    {
        _selectedStageIndex = stageIndex;
        _enemySeed = seed;

        if (ModeData.IsSingleControl)
            Input.RandomizeSingleControlBindings();

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

        _hostSetupStarted = true;
        _hostSetupCoroutine = StartCoroutine(HostSetupCoroutine());
    }

    IEnumerator HostSetupCoroutine()
    {
        yield return HostPrepareAssignmentsCoroutine();
        yield return WaitVersusThenStartRound();
        _hostSetupCoroutine = null;
    }

    // Host ritual shared by initial setup and post-round-clear: ensure objects -> assign fingers -> ready.
    IEnumerator HostPrepareAssignmentsCoroutine()
    {
        yield return Players.ServerEnsurePlayerObjectsCoroutine();
        Network.ServerInitializeFingerAssignments();
        yield return null;
        ApplyAssignmentsReady();
    }

    IEnumerator WaitVersusThenStartRound()
    {
        if (_versusPanel != null)
            yield return new WaitWhile(() => _versusPanel.IsAnimating);

        Network.ServerStartRound(_run.GetWaveDuration(_run.CurrentRound));
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
        _run.InitEnemyHp();
        OnEnemyAppeared?.Invoke(false);
        OnLocalAssignedFingerChanged?.Invoke(LocalAssignedFinger);
        OnReadyStarted?.Invoke();
        App.SystemManager.Sound.PlayBGM(EAudioClip.BGM_Battle);
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
        Phase = EInGamePhase.WaveInput;
        WaveTimeRemaining = durationSeconds;
        _enemyHandPosition = EHandPosition.Invalid;
        Input.ResetFingerState();
        Input.SetEnabled(true);
        OnLocalFingerExtendedChanged?.Invoke(false);
        OnLocalFingerMaskChanged?.Invoke(EFingerType.None);

        if (_lastEffectRound != _run.CurrentRound)
        {
            _lastEffectRound = _run.CurrentRound;
            _effects.OnRoundBegin();
        }
        _effects.OnWaveBegin();

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

        EOutcome outcome = OutcomeResolver.Resolve(handPosition, _enemyHandPosition);
        Phase = EInGamePhase.WaveComplete;
        OnOutcomeDetermined?.Invoke(outcome);
        OnWaveJudged?.Invoke(handPosition);

        bool isRoundClear = false;
        bool isGameOver = false;
        bool enemyKilled = false;

        switch (outcome)
        {
            case EOutcome.Win:
                _run.EnemyHp -= _run.PlayerDamage;
                if (_run.EnemyHp <= 0)
                {
                    enemyKilled = true;
                    _run.WonWavesInRound++;
                }
                if (_run.WonWavesInRound >= WAVES_PER_ROUND)
                    isRoundClear = true;
                int enemyHp = Mathf.Max(_run.EnemyHp, 0);
                int enemyMaxHp = _run.EnemyMaxHp;
                ArmImpactFeedback(() => OnEnemyHpChanged?.Invoke(enemyHp, enemyMaxHp), enemyKilled);
                break;

            case EOutcome.Lose:
                _run.Lives -= ENEMY_DAMAGE;
                if (_run.Lives <= 0)
                {
                    Phase = EInGamePhase.GameOver;
                    isGameOver = true;
                }
                int lives = _run.Lives;
                ArmImpactFeedback(() => OnLivesChanged?.Invoke(lives), isGameOver);
                break;
        }

        if (isRoundClear)
        {
            if (Network.IsServerHost)
                Network.ServerRoundClear();
            return;
        }

        if (_betweenWaveCoroutine != null)
        {
            StopCoroutine(_betweenWaveCoroutine);
            CancelImpactSlowMotion();
        }
        _betweenWaveCoroutine = StartCoroutine(BetweenWavesCoroutine(isGameOver, enemyKilled));
    }

    IEnumerator BetweenWavesCoroutine(bool isGameOver, bool enemyKilled)
    {
        yield return WAIT_OUTCOME;
        FlushPendingImpactFeedback();
        OnWaveResultShown?.Invoke();

        if ((enemyKilled || isGameOver) && _versusPanel != null)
            yield return _versusPanel.PlayKoSequence();

        // Boss (every round's 10th enemy) appears the moment the 9th enemy is killed and the next HP bar fills.
        bool bossAppearing = false;

        if (!isGameOver && _run.EnemyHp <= 0)
        {
            bossAppearing = _run.WonWavesInRound == WAVES_PER_ROUND - 1;
            _run.EnemyHp = _run.EnemyMaxHp;
            OnEnemyHpChanged?.Invoke(_run.EnemyHp, _run.EnemyMaxHp);
            OnEnemyAppeared?.Invoke(bossAppearing);
        }

        if (isGameOver)
        {
            Input.ResetFingerState();
            OnLocalFingerExtendedChanged?.Invoke(false);
            OnLocalFingerMaskChanged?.Invoke(EFingerType.None);
            OnGameOver?.Invoke();
            App.SystemManager.Sound.StopBGM();

            _run.Reset();
            _effects.Clear();
            _lastEffectRound = 0;
            ClearAcquiredAugments();
            _hostSetupStarted = false;
            Phase = EInGamePhase.WaitingForRoundSelect;
            OnLivesChanged?.Invoke(_run.Lives);
            OnCurrentRoundChanged?.Invoke(_run.CurrentRound);

            if (_stagePanel != null) _stagePanel.OpenPanel();
            _betweenWaveCoroutine = null;
            yield break;
        }

        if (!Network.IsServerHost)
        {
            _betweenWaveCoroutine = null;
            yield break;
        }

        yield return WAIT_SLIDE;

        if (bossAppearing)
        {
            Phase = EInGamePhase.BossIntro;
            ApplyBossRule(_run.CurrentRound);
            Network.ServerBroadcastBossIntro(_run.CurrentRound);
            yield return WAIT_BOSS_INTRO;
        }

        yield return WaitVersusThenStartRound();
        _betweenWaveCoroutine = null;
    }

    void ArmImpactFeedback(Action feedback, bool killSlowMo)
    {
        _pendingImpactFeedback = feedback;
        _pendingKillSlowMo = killSlowMo;
    }

    void HandleImpactLanded()
    {
        FlushPendingImpactFeedback();
    }

    void FlushPendingImpactFeedback()
    {
        if (_pendingImpactFeedback == null) return;

        Action feedback = _pendingImpactFeedback;
        bool killSlowMo = _pendingKillSlowMo;
        _pendingImpactFeedback = null;
        _pendingKillSlowMo = false;

        feedback.Invoke();

        if (killSlowMo)
        {
            if (_slowMoCoroutine != null) StopCoroutine(_slowMoCoroutine);
            _slowMoCoroutine = StartCoroutine(PlayImpactSlowMotion());
        }
    }

    void CancelImpactSlowMotion()
    {
        if (_slowMoCoroutine != null)
        {
            StopCoroutine(_slowMoCoroutine);
            _slowMoCoroutine = null;
        }
        Time.timeScale = 1f;
    }

    IEnumerator PlayImpactSlowMotion()
    {
        Time.timeScale = IMPACT_SLOW_TIME_SCALE;
        yield return WAIT_IMPACT_SLOW;

        float t = 0f;
        while (t < IMPACT_SLOW_RECOVER)
        {
            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(IMPACT_SLOW_TIME_SCALE, 1f, t / IMPACT_SLOW_RECOVER);
            yield return null;
        }
        Time.timeScale = 1f;
        _slowMoCoroutine = null;
    }

    void HandleRoundClear()
    {
        _rewardDone = false;
        if (_betweenWaveCoroutine != null)
        {
            StopCoroutine(_betweenWaveCoroutine);
            CancelImpactSlowMotion();
        }
        _betweenWaveCoroutine = StartCoroutine(RoundClearCoroutine());
    }

    void HandleRewardDone()
    {
        _rewardDone = true;
    }

    IEnumerator RoundClearCoroutine()
    {
        Phase = EInGamePhase.RoundClear;

        yield return WAIT_OUTCOME;
        FlushPendingImpactFeedback();
        OnWaveResultShown?.Invoke();

        if (_versusPanel != null)
            yield return _versusPanel.PlayKoSequence();

        App.SystemManager.Sound.StopBGM();

        int clearedRound = _run.CurrentRound;
        if (clearedRound >= MAX_ROUNDS)
        {
            OnGameClear?.Invoke();
            _betweenWaveCoroutine = null;
            yield break;
        }

        _run.CurrentRound++;
        _run.WonWavesInRound = 0;
        OnCurrentRoundChanged?.Invoke(_run.CurrentRound);
        OnRoundClear?.Invoke(clearedRound);

        yield return WAIT_SLIDE;

        if (Network.IsServerHost)
        {
            yield return HostCardVoteSequenceCoroutine();
            Network.ServerRewardDone();
        }
        else
        {
            yield return new WaitUntil(() => _rewardDone);
        }

        Input.ResetFingerState();
        OnLocalFingerExtendedChanged?.Invoke(false);
        OnLocalFingerMaskChanged?.Invoke(EFingerType.None);

        Phase = EInGamePhase.WaitingForRoundSelect;
        _hostSetupStarted = false;
        if (_stagePanel != null) _stagePanel.OpenPanel();
        _betweenWaveCoroutine = null;
    }

    /// <summary>Boss special-rule hook (placeholder). Real effects (IRoundRule / EnemyData) come later.</summary>
    void ApplyBossRule(int round)
    {
#if UNITY_EDITOR
        Debug.Log($"[InGameManager] 보스 등장 — Round {round} (효과 placeholder)");
#endif
    }

    IEnumerator HostCardVoteSequenceCoroutine()
    {
        if (!Network.IsServerHost) yield break;

        Phase = EInGamePhase.CardVoting;
        _voteRng ??= new System.Random();
        _voteChosen.Clear();

        yield return HostSingleVoteCoroutine(ECardKind.Boon, _run.BoonDeck);
        yield return HostSingleVoteCoroutine(ECardKind.Deviation, _run.DeviationDeck);

        if (_voteChosen.Count > 0)
            Network.ServerApplyCards(_voteChosen);

        Phase = EInGamePhase.WaitingForSetup;
    }

    IEnumerator HostSingleVoteCoroutine(ECardKind kind, CardDeck deck)
    {
        int round = _run.CurrentRound;
        int[] offered = deck.Draw(CARDS_PER_VOTE, _voteRng, index => IsCardAvailableAtRound(kind, index, round));
        Debug.Log($"[DBG Vote] kind={kind} round={round} offered={offered.Length} deckRemaining={deck.RemainingCount}");
        if (offered.Length == 0) yield break;

        Network.ServerResetCardVotes();
        Network.ServerBroadcastCardOffer(kind, offered, VOTE_SECONDS);

        float remaining = VOTE_SECONDS;
        while (remaining > 0f && !Network.AllPlayersVoted())
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        int chosenIndex = ResolveVote(offered);
        Network.ServerBroadcastCardResult(kind, chosenIndex);
        if (chosenIndex >= 0)
            _voteChosen.Add(new CardRef(kind, chosenIndex));

        yield return WAIT_VOTE_REVEAL;
    }

    static bool IsCardAvailableAtRound(ECardKind kind, int index, int round)
    {
        DataManager data = App.Data.BaseData;
        if (data == null) return true;

        if (kind == ECardKind.Boon && data.TryGetBoon((EBoon)index, out BoonData boon))
            return boon.appearRound <= round;

        if (kind == ECardKind.Deviation && data.TryGetDeviation((EDeviation)index, out DeviationData deviation))
            return deviation.appearRound <= round;

        return false;
    }

    int ResolveVote(int[] offered)
    {
        Network.CollectCardVotes(offered, _voteTally);

        int max = 0;
        for (int i = 0; i < offered.Length; i++)
            if (_voteTally[i] > max) max = _voteTally[i];

        // 전원 무투표 → 완전 무작위.
        if (max == 0)
            return offered[_voteRng.Next(offered.Length)];

        // 최다 득표(동점 포함) 후보 중 무작위 추첨.
        _voteWinners.Clear();
        for (int i = 0; i < offered.Length; i++)
            if (_voteTally[i] == max) _voteWinners.Add(offered[i]);

        return _voteWinners[_voteRng.Next(_voteWinners.Count)];
    }

    public void SetEnemyHandPosition(EHandPosition position)
    {
        _enemyHandPosition = position;
    }

    void HandleCardsApplied(IReadOnlyList<CardRef> cards)
    {
        if (cards == null) return;

        for (int i = 0; i < cards.Count; i++)
        {
            CardSystem.Apply(_run, cards[i], _effects);
            _acquiredCards.Add(cards[i]);
        }

        OnAcquiredCardsChanged?.Invoke();
    }

    void ClearAcquiredAugments()
    {
        _acquiredCards.Clear();
        OnAcquiredCardsChanged?.Invoke();
    }
}
