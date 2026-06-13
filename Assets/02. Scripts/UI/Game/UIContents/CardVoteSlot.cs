using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One card option in the card-vote panel: shows name/description and live vote count.</summary>
public class CardVoteSlot : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI nameTMP;
    [SerializeField] TextMeshProUGUI descriptionTMP;
    [SerializeField] TextMeshProUGUI tallyTMP;
    [SerializeField] GameObject selectedMark;
    [SerializeField] GameObject winnerMark;

    int _slotIndex = -1;
    Action<int> _onClick;

    public void Init(int slotIndex, Action<int> onClick)
    {
        _slotIndex = slotIndex;
        _onClick = onClick;

        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    void HandleClick() => _onClick?.Invoke(_slotIndex);

    public void SetCard(string cardName, string description)
    {
        gameObject.SetActive(true);
        if (nameTMP != null) nameTMP.text = cardName;
        if (descriptionTMP != null) descriptionTMP.text = description;
        SetTally(0);
        SetSelected(false);
        SetWinner(false);
        SetInteractable(true);
    }

    public void Hide() => gameObject.SetActive(false);

    public void SetTally(int count)
    {
        if (tallyTMP != null) tallyTMP.text = count.ToString();
    }

    public void SetSelected(bool selected)
    {
        if (selectedMark != null) selectedMark.SetActive(selected);
    }

    public void SetWinner(bool winner)
    {
        if (winnerMark != null) winnerMark.SetActive(winner);
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null) button.interactable = interactable;
    }
}
