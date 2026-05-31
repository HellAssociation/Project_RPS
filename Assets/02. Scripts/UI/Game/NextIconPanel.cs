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

    [Header("Slots — left to right, index 0 is front (visible), index 3 is off-screen right")]
    [SerializeField] NextIconSlot[] _slots;

    [Tooltip("Width of one slot in pixels (matches RectTransform width)")]
    [SerializeField] float _slotWidth = 200f;

    [SerializeField] float _tweenDuration = 0.35f;

    readonly Queue<EHandPosition> _queue = new();

    // ordered list: index 0 = leftmost (front), index 3 = rightmost (off-screen right)
    readonly List<NextIconSlot> _orderedSlots = new();

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
        BuildOrderedSlots();
        GenerateQueue();
        LoadInitialSlots();

        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnRoundResultShown += ConsumeAndAnimate;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnRoundResultShown -= ConsumeAndAnimate;
    }

    // Sort slots by their initial anchoredPosition.x so order matches left-to-right
    void BuildOrderedSlots()
    {
        _orderedSlots.Clear();
        _orderedSlots.AddRange(_slots);
        _orderedSlots.Sort((a, b) => a.Rect.anchoredPosition.x.CompareTo(b.Rect.anchoredPosition.x));
    }

    void GenerateQueue()
    {
        _queue.Clear();

        // Fixed pool for a stage: mix of Rock/Paper/Scissors/Random
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

        // Fisher-Yates shuffle
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
        for (int i = 0; i < _orderedSlots.Count; i++)
            LoadSlot(_orderedSlots[i], Dequeue());

        _currentHandPosition = _orderedSlots[0].HandPosition;
    }

    void LoadSlot(NextIconSlot slot, EHandPosition position)
    {
        slot.SetSlot(position, GetSprite(position));
    }

    EHandPosition Dequeue()
    {
        if (_queue.Count > 0)
            return _queue.Dequeue();

        // Fallback: random when queue is exhausted
        EHandPosition[] options = { EHandPosition.Rock, EHandPosition.Paper, EHandPosition.Scissors, EHandPosition.Random };
        return options[Random.Range(0, options.Length)];
    }

    // Slides all slots left by _slotWidth, then recycles the front slot to the right
    void ConsumeAndAnimate()
    {
        // Update current to the slot that will be front after animation
        if (_orderedSlots.Count > 1)
            _currentHandPosition = _orderedSlots[1].HandPosition;

        Sequence seq = DOTween.Sequence();

        for (int i = 0; i < _orderedSlots.Count; i++)
        {
            Vector2 target = _orderedSlots[i].Rect.anchoredPosition + new Vector2(-_slotWidth, 0f);
            seq.Join(_orderedSlots[i].Rect.DOAnchorPos(target, _tweenDuration).SetEase(Ease.OutBack));
        }

        seq.OnComplete(RecycleFrontSlot);
    }

    void RecycleFrontSlot()
    {
        NextIconSlot front = _orderedSlots[0];

        // Move it to the right of the last slot
        float rightX = _orderedSlots[^1].Rect.anchoredPosition.x + _slotWidth;
        front.Rect.anchoredPosition = new Vector2(rightX, front.Rect.anchoredPosition.y);

        LoadSlot(front, Dequeue());

        // Rotate ordered list: front becomes back
        _orderedSlots.RemoveAt(0);
        _orderedSlots.Add(front);
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
