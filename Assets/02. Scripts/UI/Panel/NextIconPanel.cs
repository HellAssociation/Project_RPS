using System.Collections.Generic;
using DG.Tweening;
using SystemEnums;
using UnityEngine;

public class NextIconPanel : PanelBase
{
    #region [Function] Inheritance
    public override EUIType UIType => EUIType.NextIcon;
    #endregion

    [Header("Rock / Paper / Scissors / Random")]
    [SerializeField] Sprite[] handPositionSprites;

    [SerializeField] NextIconSlot[] _slots;

    [SerializeField] float _tweenDuration = 0.35f;

    // Display = icon shown to players (Random stays hidden as "?"); Resolved = actual hand for judging.
    readonly struct HandSlot
    {
        public readonly EHandPosition Display;
        public readonly EHandPosition Resolved;
        public HandSlot(EHandPosition display, EHandPosition resolved)
        {
            Display = display;
            Resolved = resolved;
        }
    }

    static readonly EHandPosition[] ValidPositions =
    {
        EHandPosition.Rock,
        EHandPosition.Paper,
        EHandPosition.Scissors,
    };

    readonly Queue<HandSlot> _queue = new();
    System.Random _rng = new();

    NextIconSlot currentSlot;
    NextIconSlot nextSlot;

    readonly Vector2 leftPosition    = new(-88f, 0f);
    readonly Vector2 currentPosition = Vector2.zero;
    readonly Vector2 rightPosition   = new(88f, 0f);

    EHandPosition _currentHandPosition;
    EHandPosition _currentResolvedHandPosition;
    public EHandPosition CurrentHandPosition => _currentHandPosition;
    public EHandPosition CurrentResolvedHandPosition => _currentResolvedHandPosition;

    protected override void Awake()
    {
        base.Awake();
        foreach (var slot in _slots)
            slot.Init();
    }

    void Start()
    {
        GenerateQueue(0);
        LoadInitialSlots();

        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnStageOpened     += HandleStageOpened;
        inGame.OnReadyStarted    += HandleReadyStarted;
        inGame.OnWaveStarted     += HandleWaveStarted;
        inGame.OnWaveResultShown += HandleWaveResultShown;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnStageOpened     -= HandleStageOpened;
        inGame.OnReadyStarted    -= HandleReadyStarted;
        inGame.OnWaveStarted     -= HandleWaveStarted;
        inGame.OnWaveResultShown -= HandleWaveResultShown;
    }

    void HandleStageOpened(int _)
    {
        InGameManager inGame = App.SceneManager.InGame;
        GenerateQueue(inGame != null ? inGame.EnemySeed : 0);
        LoadInitialSlots();
    }

    void GenerateQueue(int seed)
    {
        _rng = new System.Random(seed);
        _queue.Clear();

        EHandPosition[] pool =
        {
            EHandPosition.Rock,
            EHandPosition.Paper,
            EHandPosition.Scissors,
            EHandPosition.Random,
            EHandPosition.Rock,
            EHandPosition.Paper,
            EHandPosition.Scissors,
            EHandPosition.Random,
            EHandPosition.Rock,
            EHandPosition.Scissors,
            EHandPosition.Paper,
        };

        CollectionUtility.Shuffle(pool, _rng);

        foreach (var pos in pool)
            _queue.Enqueue(MakeSlot(pos));
    }

    void LoadInitialSlots()
    {
        currentSlot = _slots[0];
        nextSlot    = _slots[1];

        LoadSlot(currentSlot, Dequeue());
        LoadSlot(nextSlot,    Dequeue());

        currentSlot.Rect.anchoredPosition = rightPosition;
        nextSlot.Rect.anchoredPosition    = rightPosition;

        _currentHandPosition         = currentSlot.HandPosition;
        _currentResolvedHandPosition = currentSlot.ResolvedHandPosition;
    }

    void HandleReadyStarted()
    {
        currentSlot.Rect.DOKill();
        currentSlot.Rect.DOAnchorPos(currentPosition, _tweenDuration).SetEase(Ease.InOutQuad);
    }

    void HandleWaveStarted(float _)
    {
        _currentHandPosition         = currentSlot.HandPosition;
        _currentResolvedHandPosition = currentSlot.ResolvedHandPosition;

        currentSlot.Rect.DOKill();
        currentSlot.Rect.DOAnchorPos(leftPosition, _tweenDuration).SetEase(Ease.InOutQuad)
            .OnComplete(RecycleAndSwap);

        nextSlot.Rect.DOKill();
        nextSlot.Rect.DOAnchorPos(currentPosition, _tweenDuration).SetEase(Ease.InOutQuad);
    }

    void HandleWaveResultShown() { }

    void RecycleAndSwap()
    {
        NextIconSlot recycled = currentSlot;
        LoadSlot(recycled, Dequeue());
        recycled.Rect.anchoredPosition = rightPosition;

        currentSlot = nextSlot;
        nextSlot    = recycled;
    }

    void LoadSlot(NextIconSlot slot, HandSlot hand)
    {
        slot.SetSlot(hand.Display, hand.Resolved, GetSprite(hand.Display));
    }

    HandSlot Dequeue()
    {
        if (_queue.Count > 0) return _queue.Dequeue();

        EHandPosition[] options = { EHandPosition.Rock, EHandPosition.Paper, EHandPosition.Scissors, EHandPosition.Random };
        return MakeSlot(options[_rng.Next(options.Length)]);
    }

    HandSlot MakeSlot(EHandPosition display)
    {
        EHandPosition resolved = display == EHandPosition.Random
            ? ValidPositions[_rng.Next(ValidPositions.Length)]
            : display;
        return new HandSlot(display, resolved);
    }

    public Sprite GetSprite(EHandPosition handPosition)
    {
        return handPosition switch
        {
            EHandPosition.Rock     => handPositionSprites[0],
            EHandPosition.Paper    => handPositionSprites[1],
            EHandPosition.Scissors => handPositionSprites[2],
            EHandPosition.Random   => handPositionSprites[3],
            _                      => null,
        };
    }
}
