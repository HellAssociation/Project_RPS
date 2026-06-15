using System.Collections;
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

    const float REVEAL_SECONDS = 1.5f;

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

        if (_slots == null || _slots.Length == 0)
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

        if (titleTMP != null)
            titleTMP.text = kind == ECardKind.Boon ? "축복" : "저주";

        for (int i = 0; i < _slots.Length; i++)
        {
            if (i < offered.Length && TryGetCardText(kind, offered[i], out string cardName, out string desc))
                _slots[i].SetCard(cardName, desc);
            else
                _slots[i].Hide();
        }

        OpenPanel();
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

        for (int i = 0; i < _slots.Length; i++)
        {
            if (i >= _offered.Length) continue;
            _slots[i].SetInteractable(false);
            _slots[i].SetWinner(chosenIndex >= 0 && _offered[i] == chosenIndex);
        }

        if (_closeCoroutine != null) StopCoroutine(_closeCoroutine);
        _closeCoroutine = StartCoroutine(CloseAfterReveal());
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
        for (int i = 0; i < _slots.Length; i++)
            if (i < _offered.Length) _slots[i].SetSelected(_offered[i] == _localSelection);
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

    IEnumerator CloseAfterReveal()
    {
        yield return new WaitForSeconds(REVEAL_SECONDS);
        ClosePanel();
        _offered = null;
        _closeCoroutine = null;
    }

    bool TryGetCardText(ECardKind kind, int index, out string cardName, out string description)
    {
        cardName = null;
        description = null;

        DataManager data = App.Data.BaseData;
        if (data == null) return false;

        if (kind == ECardKind.Boon && data.TryGetBoon((EBoon)index, out BoonData boon))
        {
            cardName = boon.name;
            description = boon.description;
            return true;
        }

        if (kind == ECardKind.Deviation && data.TryGetDeviation((EDeviation)index, out DeviationData deviation))
        {
            cardName = deviation.name;
            description = deviation.description;
            return true;
        }

        return false;
    }
}
