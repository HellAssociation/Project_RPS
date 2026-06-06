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

    [SerializeField] Image readyImage;
    [SerializeField] Image fightImage;

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

        readyImage.DOKill();
        fightImage.DOKill();
        readyImage.transform.DOKill();
        fightImage.transform.DOKill();

        SetHidden(readyImage);
        SetHidden(fightImage);

        Sequence seq = DOTween.Sequence();

        seq.AppendCallback(() => Stamp(readyImage));
        seq.AppendInterval(0.75f);
        seq.Append(readyImage.DOFade(0f, 0.1f));

        seq.AppendCallback(() => Stamp(fightImage));
        seq.AppendInterval(0.75f);
        seq.Append(fightImage.DOFade(0f, 0.1f));

        seq.OnComplete(() =>
        {
            _isAnimating = false;
            ClosePanel();
        });
    }

    static void SetHidden(Image img)
    {
        img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);
        img.transform.localScale = Vector3.one;
    }

    static void Stamp(Image img)
    {
        img.transform.localScale = Vector3.one * 1.35f;
        img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);

        img.transform.DOScale(1f, 0.08f).SetEase(Ease.OutExpo)
            .OnComplete(() =>
                img.transform.DOPunchScale(Vector3.one * 0.05f, 0.3f, 6, 0.3f));
    }
}
