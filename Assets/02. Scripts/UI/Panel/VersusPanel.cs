using DG.Tweening;
using System.Collections;
using SystemEnums;
using UnityEngine;
using UnityEngine.UI;

public class VersusPanel : PanelBase
{
    #region [Function] Inheritance
    public override EUIType UIType => EUIType.Versus;
    #endregion

    [SerializeField] Image readyImage;
    [SerializeField] Image fightImage;
    [SerializeField] Image kImage;
    [SerializeField] Image oImage;

    [Header("KO Sequence")]
    [SerializeField] float koStampGap = 0.35f;
    [SerializeField] float koHoldDuration = 0.75f;
    [SerializeField] float koFadeDuration = 0.1f;

    bool _isAnimating;
    public bool IsAnimating => _isAnimating;

    public override void OpenPanel()
    {
        base.OpenPanel();
        PlayIntroSequence();
    }

    public IEnumerator PlayKoSequence()
    {
        _isAnimating = true;
        base.OpenPanel();

        KillAllTweens();
        SetHidden(readyImage);
        SetHidden(fightImage);
        SetHidden(kImage);
        SetHidden(oImage);

        bool complete = false;
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.AppendCallback(() =>
        {
            App.SystemManager.Sound.PlayRandomSFX(SoundManager.KoSfx);
            Stamp(kImage);
        });
        sequence.AppendInterval(koStampGap);
        sequence.AppendCallback(() => Stamp(oImage));
        sequence.AppendInterval(koHoldDuration);
        sequence.Append(kImage.DOFade(0f, koFadeDuration));
        sequence.Join(oImage.DOFade(0f, koFadeDuration));
        sequence.OnComplete(() => complete = true);

        yield return new WaitUntil(() => complete);

        _isAnimating = false;
        ClosePanel();
    }

    void PlayIntroSequence()
    {
        _isAnimating = true;

        KillAllTweens();
        SetHidden(readyImage);
        SetHidden(fightImage);
        SetHidden(kImage);
        SetHidden(oImage);

        Sequence sequence = DOTween.Sequence();

        sequence.AppendCallback(() =>
        {
            App.SystemManager.Sound.PlaySFX(EAudioClip.SFX_Ready);
            Stamp(readyImage);
        });
        sequence.AppendInterval(0.75f);
        sequence.Append(readyImage.DOFade(0f, 0.1f));

        sequence.AppendCallback(() =>
        {
            App.SystemManager.Sound.PlaySFX(EAudioClip.SFX_Fight);
            Stamp(fightImage);
        });
        sequence.AppendInterval(0.75f);
        sequence.Append(fightImage.DOFade(0f, 0.1f));

        sequence.OnComplete(() =>
        {
            _isAnimating = false;
            ClosePanel();
        });
    }

    void KillAllTweens()
    {
        readyImage.DOKill();
        fightImage.DOKill();
        kImage.DOKill();
        oImage.DOKill();
        readyImage.transform.DOKill();
        fightImage.transform.DOKill();
        kImage.transform.DOKill();
        oImage.transform.DOKill();
    }

    static void SetHidden(Image img)
    {
        if (img == null) return;
        img.color = new Color(img.color.r, img.color.g, img.color.b, 0f);
        img.transform.localScale = Vector3.one;
    }

    static void Stamp(Image img)
    {
        if (img == null) return;
        img.transform.localScale = Vector3.one * 1.35f;
        img.color = new Color(img.color.r, img.color.g, img.color.b, 1f);

        img.transform.DOScale(1f, 0.08f).SetEase(Ease.OutExpo).SetUpdate(true)
            .OnComplete(() =>
                img.transform.DOPunchScale(Vector3.one * 0.05f, 0.3f, 6, 0.3f).SetUpdate(true));
    }
}
