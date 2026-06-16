using System.Collections;
using DG.Tweening;
using SystemEnums;
using TMPro;
using UnityEngine;

/// <summary>
/// 라운드 시작 시 축복/저주 카드 투표 UI. 호스트가 브로드캐스트한 제시 카드를 표시하고,
/// 클릭으로 투표(NetworkManager.ClientSendCardVote)하며, 모든 클라가 [Networked] 투표값을 읽어
/// 라이브 집계를 갱신합니다. 확정 결과는 호스트가 ServerBroadcastCardResult로 통보합니다.
/// </summary>
public class CardVotePanel : PanelBase
{
    #region [Function] Inheritance
    public override EUIType UIType => EUIType.CardVote;
    #endregion

    [SerializeField] TextMeshProUGUI titleTMP;
    [SerializeField] TextMeshProUGUI countdownTMP;
    [SerializeField] CardVoteSlot[] _slots;

    const float FADE_SECONDS = 0.2f;

    NetworkManager Network => App.SystemManager.Network;
    PlayerManager Players  => App.Game.Players;

    int[] _offered;
    readonly int[] _tallyBuffer = new int[InGameManager.CARDS_PER_VOTE];
    ECardKind _currentKind;
    int _localSelection = PlayerNetworkObject.NO_VOTE;
    bool _resolved;

    Coroutine _countdownCoroutine;
    Coroutine _closeCoroutine;

    protected override void Awake()
    {
        base.Awake();

        if (_slots == null || _slots.Length == 0 || HasMissingSlot(_slots))
            _slots = GetComponentsInChildren<CardVoteSlot>(true);

        for (int i = 0; i < _slots.Length; i++)
            _slots[i].Init(i, HandleSlotClicked);
    }

    void OnEnable()
    {
        Network.OnCardOfferReceived += HandleCardOffer;
        Network.OnCardVoteResult    += HandleCardResult;
        Players.OnPlayersChanged    += RefreshTally;
    }

    static bool HasMissingSlot(CardVoteSlot[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                return true;
        }

        return false;
    }

    void OnDisable()
    {
        Network.OnCardOfferReceived -= HandleCardOffer;
        Network.OnCardVoteResult    -= HandleCardResult;
        Players.OnPlayersChanged    -= RefreshTally;
    }

    void HandleCardOffer(ECardKind kind, int[] offered, float duration)
    {
        _currentKind    = kind;
        _offered        = offered;
        _resolved       = false;
        _localSelection = PlayerNetworkObject.NO_VOTE;

        if (_closeCoroutine != null)
        {
            StopCoroutine(_closeCoroutine);
            _closeCoroutine = null;
        }

        if (titleTMP != null)
            titleTMP.text = kind == ECardKind.Boon ? "축복" : "저주";

        for (int i = 0; i < _slots.Length; i++)
        {
            if (i < offered.Length && TryGetCardView(kind, offered[i], out string cardName, out string desc, out Sprite icon))
                _slots[i].SetCard(cardName, desc, icon);
            else
                _slots[i].Hide();
        }

        OpenPanel();
        FadeSlotsIn();
        RefreshTally();
        StartCountdown(duration);
    }

    void HandleSlotClicked(int slotIndex)
    {
        if (_resolved || _offered == null || slotIndex >= _offered.Length)
            return;

        _localSelection = _offered[slotIndex];
        Network.ClientSendCardVote(_localSelection);
        UpdateSelectionMarks();
    }

    void HandleCardResult(ECardKind kind, int chosenIndex)
    {
        if (_offered == null) return;

        _resolved = true;
        StopCountdown();
        RefreshTally();

        CardVoteSlot chosenSlot = null;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (i >= _offered.Length) continue;
            bool isWinner = chosenIndex >= 0 && _offered[i] == chosenIndex;
            _slots[i].SetInteractable(false);
            _slots[i].SetWinner(isWinner);
            if (isWinner)
                chosenSlot = _slots[i];
        }

        if (_closeCoroutine != null) StopCoroutine(_closeCoroutine);
        _closeCoroutine = StartCoroutine(CloseAfterReveal(chosenSlot));
    }

    void RefreshTally()
    {
        if (_offered == null || !IsOpened || _resolved && _closeCoroutine == null)
            return;

        Network.CollectCardVotes(_offered, _tallyBuffer);
        for (int i = 0; i < _slots.Length; i++)
            if (i < _offered.Length) _slots[i].SetTally(_tallyBuffer[i]);
    }

    void UpdateSelectionMarks()
    {
        if (_offered == null) return;
        bool hasSelection = _localSelection != PlayerNetworkObject.NO_VOTE;
        for (int i = 0; i < _slots.Length; i++)
            if (i < _offered.Length) _slots[i].SetVoteSelection(_offered[i] == _localSelection, hasSelection);
    }

    void StartCountdown(float duration)
    {
        StopCountdown();
        _countdownCoroutine = StartCoroutine(CountdownCoroutine(duration));
    }

    void StopCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
    }

    IEnumerator CountdownCoroutine(float duration)
    {
        float remaining = duration;
        while (remaining > 0f)
        {
            if (countdownTMP != null)
                countdownTMP.text = Mathf.CeilToInt(remaining).ToString();
            remaining -= Time.deltaTime;
            yield return null;
        }

        if (countdownTMP != null) countdownTMP.text = "0";
        _countdownCoroutine = null;
    }

    IEnumerator CloseAfterReveal(CardVoteSlot chosenSlot)
    {
        if (chosenSlot != null)
            yield return chosenSlot.PlayConfirmFeedback();

        yield return FadeSlotsOut();
        ClosePanel();
        _offered = null;
        _closeCoroutine = null;
    }

    void FadeSlotsIn()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (i >= _offered.Length) continue;
            _slots[i].SetAlpha(0f);
            _slots[i].FadeIn(FADE_SECONDS);
        }
    }

    IEnumerator FadeSlotsOut()
    {
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        bool hasTween = false;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (i >= _offered.Length) continue;
            Tween tween = _slots[i].FadeOut(FADE_SECONDS);
            if (tween == null) continue;

            sequence.Join(tween);
            hasTween = true;
        }

        if (hasTween)
            yield return sequence.WaitForCompletion();
        else
            sequence.Kill();
    }

    bool TryGetCardView(ECardKind kind, int index, out string cardName, out string description, out Sprite icon)
    {
        cardName = null;
        description = null;
        icon = null;

        DataManager data = App.Data.BaseData;
        if (data == null) return false;

        if (kind == ECardKind.Boon && data.TryGetBoon((EBoon)index, out BoonData boon))
        {
            cardName = ResolveString(data, boon.name);
            description = ResolveString(data, boon.description);
            icon = ResolveCardIcon(boon.code);
            return true;
        }

        if (kind == ECardKind.Deviation && data.TryGetDeviation((EDeviation)index, out DeviationData deviation))
        {
            cardName = ResolveString(data, deviation.name);
            description = ResolveString(data, deviation.description);
            icon = ResolveCardIcon(deviation.code);
            return true;
        }

        return false;
    }

    static string ResolveString(DataManager data, string code)
    {
        if (!string.IsNullOrEmpty(code) &&
            System.Enum.TryParse(code, ignoreCase: true, out EStringType key) &&
            data.TryGetString(key, out StringData stringData))
        {
            return stringData.korean;
        }

        return code;
    }

    static Sprite ResolveCardIcon(string code)
    {
        string address = $"Card_Icon_{code}";
        return App.SystemManager.Asset != null &&
               App.SystemManager.Asset.TryGetAsset(address, out Sprite sprite)
            ? sprite
            : null;
    }
}
