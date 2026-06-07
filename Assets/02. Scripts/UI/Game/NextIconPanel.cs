using System.Collections.Generic;
using DG.Tweening;
using SystemEnums;
using UnityEngine;

public class NextIconPanel : PanelBase
{
    #region [Function] Inheritance
    public override bool IsOpened => _panelGameObject.activeSelf;
    public override bool CanCloseWithESC => false;
    public override bool IsStackable => false;
    public override EUIType UIType => EUIType.NextIcon;
    #endregion

    [Header("Rock / Paper / Scissors / Random")]
    [SerializeField] Sprite[] handPositionSprites;

    [SerializeField] NextIconSlot[] _slots;

    [SerializeField] float _tweenDuration = 0.35f;

    readonly Queue<EHandPosition> _queue = new();

    NextIconSlot currentSlot;
    NextIconSlot nextSlot;

    readonly Vector2 leftPosition    = new(-88f, 0f);
    readonly Vector2 currentPosition = Vector2.zero;
    readonly Vector2 rightPosition   = new(88f, 0f);

    EHandPosition _currentHandPosition;
    public EHandPosition CurrentHandPosition => _currentHandPosition;

    protected override void Awake()
    {
        base.Awake();
        foreach (var slot in _slots)
            slot.Init();
    }

    void Start()
    {
        GenerateQueue();
        LoadInitialSlots();

        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnReadyStarted    += HandleReadyStarted;
        inGame.OnWaveStarted     += HandleWaveStarted;
        inGame.OnWaveResultShown += HandleWaveResultShown;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnReadyStarted    -= HandleReadyStarted;
        inGame.OnWaveStarted     -= HandleWaveStarted;
        inGame.OnWaveResultShown -= HandleWaveResultShown;
    }

    void GenerateQueue()
    {
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

        for (int i = pool.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        foreach (var pos in pool)
            _queue.Enqueue(pos);
    }

    void LoadInitialSlots()
    {
        currentSlot = _slots[0];
        nextSlot    = _slots[1];

        LoadSlot(currentSlot, Dequeue());
        LoadSlot(nextSlot,    Dequeue());

        currentSlot.Rect.anchoredPosition = rightPosition;
        nextSlot.Rect.anchoredPosition    = rightPosition;

        _currentHandPosition = currentSlot.HandPosition;
    }

    void HandleReadyStarted()
    {
        currentSlot.Rect.DOKill();
        currentSlot.Rect.DOAnchorPos(currentPosition, _tweenDuration).SetEase(Ease.InOutQuad);
    }

    void HandleWaveStarted(float _)
    {
        _currentHandPosition = currentSlot.HandPosition;

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

    void LoadSlot(NextIconSlot slot, EHandPosition position)
    {
        slot.SetSlot(position, GetSprite(position));
    }

    EHandPosition Dequeue()
    {
        if (_queue.Count > 0) return _queue.Dequeue();

        EHandPosition[] options = { EHandPosition.Rock, EHandPosition.Paper, EHandPosition.Scissors, EHandPosition.Random };
        return options[Random.Range(0, options.Length)];
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
