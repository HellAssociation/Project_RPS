using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardVoteSlot : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI nameTMP;
    [SerializeField] TextMeshProUGUI descriptionTMP;
    [SerializeField] TextMeshProUGUI tallyTMP;
    [SerializeField] Image backgroundImage;
    [SerializeField] Image iconImage;
    [SerializeField] MMF_Player confirmFeedback;

    static readonly Color NormalColor = Color.white;
    static readonly Color MutedColor = new Color(0.42f, 0.42f, 0.42f, 1f);

    int _slotIndex = -1;
    Action<int> _onClick;
    Vector3 _baseScale = Vector3.one;
    Graphic[] _fadeTargets = Array.Empty<Graphic>();
    Tween _fadeTween;

    public void Init(int slotIndex, Action<int> onClick)
    {
        _slotIndex = slotIndex;
        _onClick = onClick;
        _baseScale = transform.localScale;

        CacheFadeTargets();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    void HandleClick() => _onClick?.Invoke(_slotIndex);

    public void SetCard(string cardName, string description, Sprite icon)
    {
        gameObject.SetActive(true);
        if (nameTMP != null) nameTMP.text = cardName;
        if (descriptionTMP != null) descriptionTMP.text = description;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        ResetVisualState();
        SetTally(0);
        SetInteractable(true);
    }

    public void Hide()
    {
        ResetVisualState();
        gameObject.SetActive(false);
    }

    public void SetTally(int count)
    {
        if (tallyTMP != null) tallyTMP.text = count.ToString();
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null) button.interactable = interactable;
    }

    public void SetVoteSelection(bool selected, bool hasSelection)
    {
        ApplyCardColor(!hasSelection || selected ? NormalColor : MutedColor);
    }

    public void SetWinner(bool winner)
    {
        ApplyCardColor(winner ? NormalColor : MutedColor);
    }

    public IEnumerator PlayConfirmFeedback()
    {
        if (confirmFeedback == null)
            yield break;

        yield return confirmFeedback.PlayFeedbacksCoroutine(transform.position);
    }

    public Tween FadeIn(float duration) => FadeTo(1f, duration);

    public Tween FadeOut(float duration) => FadeTo(0f, duration);

    public void SetAlpha(float alpha)
    {
        KillFadeTween();
        ApplyFadeAlpha(alpha);
    }

    void ResetVisualState()
    {
        KillFadeTween();
        ApplyCardColor(NormalColor);
        ApplyFadeAlpha(1f);
        transform.DOKill();
        transform.localScale = _baseScale;
    }

    void ApplyCardColor(Color color)
    {
        if (backgroundImage != null) backgroundImage.color = color;
        if (iconImage != null) iconImage.color = color;
    }

    void CacheFadeTargets()
    {
        var targets = new List<Graphic>();
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
            targets.Add(images[i]);

        if (nameTMP != null) targets.Add(nameTMP);
        if (descriptionTMP != null) targets.Add(descriptionTMP);
        if (tallyTMP != null) targets.Add(tallyTMP);

        _fadeTargets = targets.ToArray();
    }

    void ApplyFadeAlpha(float alpha)
    {
        for (int i = 0; i < _fadeTargets.Length; i++)
        {
            Graphic graphic = _fadeTargets[i];
            if (graphic == null) continue;

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }

    void KillFadeTween()
    {
        if (_fadeTween != null && _fadeTween.IsActive())
            _fadeTween.Kill();

        _fadeTween = null;
    }

    Tween FadeTo(float alpha, float duration)
    {
        KillFadeTween();

        Sequence sequence = DOTween.Sequence().SetEase(Ease.OutQuad).SetUpdate(true);
        bool hasTween = false;

        for (int i = 0; i < _fadeTargets.Length; i++)
        {
            Graphic graphic = _fadeTargets[i];
            if (graphic == null) continue;

            sequence.Join(graphic.DOFade(alpha, duration));
            hasTween = true;
        }

        if (!hasTween)
        {
            sequence.Kill();
            return null;
        }

        _fadeTween = sequence;
        return sequence;
    }
}
