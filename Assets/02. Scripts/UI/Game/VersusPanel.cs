using DG.Tweening;
using SystemEnums;
using UnityEngine;
using UnityEngine.UI;

public class VersusPanel : PanelBase
{
    #region [Function] Inheritance
    public override bool IsOpened => _panelGameObject.activeSelf;
    public override bool CanCloseWithESC => false;
    public override bool IsStackable => false;
    public override EUIType UIType => EUIType.Versus;
    #endregion

    [SerializeField] Image divideLineImage;
    [SerializeField] Image versusImage;

    bool _isAnimating;
    public bool IsAnimating => _isAnimating;

    public override void OpenPanel()
    {
        base.OpenPanel();
        PlayIntroSequence();
    }

    void PlayIntroSequence()
    {
        _isAnimating = true;

        divideLineImage.DOKill();
        versusImage.transform.DOKill();

        // Line: hidden at full scale, punches in
        divideLineImage.color = new Color(divideLineImage.color.r, divideLineImage.color.g, divideLineImage.color.b, 0f);
        divideLineImage.rectTransform.localScale = Vector3.one;
        versusImage.transform.localScale = Vector3.zero;

        Sequence sequence = DOTween.Sequence();

        // Line flashes in instantly, then punches — preserves zigzag shape
        sequence.Append(divideLineImage.DOFade(1f, 0.08f));
        sequence.AppendCallback(() => divideLineImage.rectTransform.DOPunchScale(new Vector3(0.08f, 0.2f, 0f), 0.45f, 7, 0.4f));

        // VS image pops in with overshoot bounce
        sequence.Append(versusImage.transform.DOScale(1.25f, 0.3f).SetEase(Ease.OutBack));
        sequence.Append(versusImage.transform.DOScale(1f, 0.12f).SetEase(Ease.OutSine));

        // Hold
        sequence.AppendInterval(0.9f);

        // Outro: both fade out together
        sequence.Append(versusImage.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack));
        sequence.Join(divideLineImage.DOFade(0f, 0.25f));

        sequence.OnComplete(() =>
        {
            _isAnimating = false;
            ClosePanel();
        });
    }
}
