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

    readonly Vector3 leftPosition    = new(-248f, 0f, 0f);
    readonly Vector3 currentPosition = new(  48f, 0f, 0f);
    readonly Vector3 rightPosition   = new( 248f, 0f, 0f);

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
        inGame.OnWaveStarted     += HandleWaveStarted;
        inGame.OnWaveResultShown += HandleWaveResultShown;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
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
            EHandPosition.Rock,
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

        currentSlot.Rect.localPosition = rightPosition;
        nextSlot.Rect.localPosition    = rightPosition;

        _currentHandPosition = currentSlot.HandPosition;
    }

    void HandleWaveStarted(float _)
    {
        currentSlot.Rect.DOKill();
        currentSlot.Rect.DOLocalMove(currentPosition, _tweenDuration).SetEase(Ease.OutBack);
    }

    void HandleWaveResultShown()
    {
        currentSlot.Rect.DOKill();
        currentSlot.Rect.DOLocalMove(leftPosition, _tweenDuration).SetEase(Ease.InBack)
            .OnComplete(RecycleAndSwap);
    }

    void RecycleAndSwap()
    {
        NextIconSlot recycled = currentSlot;
        LoadSlot(recycled, Dequeue());
        recycled.Rect.localPosition = rightPosition;

        currentSlot          = nextSlot;
        nextSlot             = recycled;
        _currentHandPosition = currentSlot.HandPosition;
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
