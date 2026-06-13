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

    readonly RunState _run = new();

    System.Random _voteRng;
    readonly int[] _voteTally = new int[CARDS_PER_VOTE];
    readonly List<int> _voteWinners = new(CARDS_PER_VOTE);
    readonly List<CardRef> _voteChosen = new(2);

    public int MaxLives => _run.MaxLives;

    const float SLIDE_DURATION = 0.5f;

    static readonly WaitForSeconds WAIT_OUTCOME = new(BETWEEN_WAVE_DELAY);
    static readonly WaitForSeconds WAIT_SLIDE   = new(SLIDE_DURATION);
    static readonly WaitForSeconds WAIT_VOTE_REVEAL = new(VOTE_REVEAL_SECONDS);
    static readonly WaitForSeconds WAIT_BOSS_INTRO  = new(BOSS_INTRO_SECONDS);

    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players  => App.Game.Players;
    InputManager Input     => App.SystemManager.Input;

    Coroutine _waveCoroutine;
    Coroutine _hostSetupCoroutine;
    Coroutine _betweenWaveCoroutine;
    bool _hostSetupStarted;

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
        Network.OnCardsApplied              += HandleCardsApplied;
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
        Network.OnCardsApplied              -= HandleCardsApplied;
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
        _run.Reset();
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

    void HandleRoundSelected(int stageIndex, int seed)
    {
        _selectedStageIndex = stageIndex;
        _enemySeed          = seed;

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

        _hostSetupStarted   = true;
        _hostSetupCoroutine = StartCoroutine(HostSetupCoroutine());
    }

    IEnumerator HostSetupCoroutine()
    {
        yield return Players.ServerEnsurePlayerObjectsCoroutine();

        yield return HostCardVoteSequenceCoroutine();

        Network.ServerInitializeFingerAssignments();
        yield return null;

        ApplyAssignmentsReady();

        if (_versusPanel != null)
            yield return new WaitWhile(() => _versusPanel.IsAnimating);

        Network.ServerStartRound(_run.GetWaveDuration(_run.CurrentRound));
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
        _run.InitEnemyHp();
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
        OnLocalFingerMaskChanged?.Invoke(EFingerType.None);
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
        bool isGameOver   = false;

        switch (outcome)
        {
            case EOutcome.Win:
                _run.EnemyHp -= _run.PlayerDamage;
                OnEnemyHpChanged?.Invoke(Mathf.Max(_run.EnemyHp, 0), _run.EnemyMaxHp);
                if (_run.EnemyHp <= 0)
                    _run.WonWavesInRound++;
                if (_run.WonWavesInRound >= WAVES_PER_ROUND)
                    isRoundClear = true;
                break;

            case EOutcome.Lose:
                _run.Lives -= ENEMY_DAMAGE;
                OnLivesChanged?.Invoke(_run.Lives);
                if (_run.Lives <= 0)
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

        // Boss (every round's 10th enemy) appears the moment the 9th enemy is killed and the next HP bar fills.
        bool bossAppearing = false;

        if (!isRoundClear && !isGameOver && _run.EnemyHp <= 0)
        {
            bossAppearing = _run.WonWavesInRound == WAVES_PER_ROUND - 1;
            _run.EnemyHp = _run.EnemyMaxHp;
            OnEnemyHpChanged?.Invoke(_run.EnemyHp, _run.EnemyMaxHp);
        }

        if (isGameOver)
        {
            Input.ResetFingerState();
            OnLocalFingerExtendedChanged?.Invoke(false);
            OnLocalFingerMaskChanged?.Invoke(EFingerType.None);
            OnGameOver?.Invoke();

            _run.Reset();
            _hostSetupStarted = false;
            Phase            = EInGamePhase.WaitingForRoundSelect;
            OnLivesChanged?.Invoke(_run.Lives);
            OnCurrentRoundChanged?.Invoke(_run.CurrentRound);

            if (_stagePanel != null) _stagePanel.OpenPanel();
            _betweenWaveCoroutine = null;
            yield break;
        }

        if (isRoundClear)
        {
            int clearedRound = _run.CurrentRound;
            _run.CurrentRound++;
            _run.WonWavesInRound = 0;
            OnCurrentRoundChanged?.Invoke(_run.CurrentRound);
            OnRoundClear?.Invoke(clearedRound);
        }

        if (!Network.IsServerHost) yield break;

        yield return WAIT_SLIDE;

        if (isRoundClear)
        {
            if (_run.CurrentRound > MAX_ROUNDS)
            {
                Phase = EInGamePhase.RoundClear;
                _betweenWaveCoroutine = null;
                yield break;
            }

            Phase             = EInGamePhase.WaitingForSetup;
            _hostSetupStarted = false;
            yield return Players.ServerEnsurePlayerObjectsCoroutine();
            yield return HostCardVoteSequenceCoroutine();
            Network.ServerInitializeFingerAssignments();
            yield return null;
            ApplyAssignmentsReady();
        }

        if (bossAppearing)
        {
            Phase = EInGamePhase.BossIntro;
            ApplyBossRule(_run.CurrentRound);
            Network.ServerBroadcastBossIntro(_run.CurrentRound);
            yield return WAIT_BOSS_INTRO;
        }

        if (_versusPanel != null && _versusPanel.IsAnimating)
            yield return new WaitWhile(() => _versusPanel.IsAnimating);

        Network.ServerStartRound(_run.GetWaveDuration(_run.CurrentRound));
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
        int[] offered = deck.Draw(CARDS_PER_VOTE, _voteRng);
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
            CardSystem.Apply(_run, cards[i]);
    }
}
