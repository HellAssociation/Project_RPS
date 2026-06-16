using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

/// <summary>
/// Persistent HUD mirroring the run's acquired augments. Rebuilds its CardIconSlots whenever
/// InGameManager.OnAcquiredCardsChanged fires (acquire, wave-loss removal, game over). Boon vs
/// deviation styling is handled by CardIconSlot.SetIcon.
/// </summary>
public class CardIconPanel : PanelBase
{
    public override EUIType UIType => EUIType.CardIcon;

    [SerializeField] CardIconSlot _slotPrefab;
    [SerializeField] Transform _slotContainer;

    readonly List<CardIconSlot> _slots = new();

    InGameManager _inGame;

    void Start()
    {
        _inGame = App.SceneManager.InGame;
        if (_inGame == null) return;

        _inGame.OnAcquiredCardsChanged += Rebuild;
        Rebuild();
    }

    void OnDestroy()
    {
        if (_inGame != null)
            _inGame.OnAcquiredCardsChanged -= Rebuild;
    }

    void Rebuild()
    {
        if (_slotPrefab == null || _slotContainer == null) return;

        IReadOnlyList<CardRef> cards = _inGame.AcquiredCards;
        EnsureSlotCount(cards.Count);

        for (int i = 0; i < _slots.Count; i++)
        {
            bool used = i < cards.Count;
            _slots[i].gameObject.SetActive(used);
            if (used)
                _slots[i].SetIcon(CardPresentation.ResolveIcon(cards[i]), CardPresentation.IsBoon(cards[i]));
        }
    }

    void EnsureSlotCount(int count)
    {
        while (_slots.Count < count)
            _slots.Add(Instantiate(_slotPrefab, _slotContainer));
    }
}
