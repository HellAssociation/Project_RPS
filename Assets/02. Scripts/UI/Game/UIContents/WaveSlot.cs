using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class WaveSlot : MonoBehaviour
{
    [SerializeField] RectTransform rect;
    [SerializeField] Image         waveBallImage;
    [SerializeField] Image         delteImage;

    const float BOSS_SCALE = 1.5f;

    WaveType waveType;
    Color    _originalBallColor;

    public void Init(WaveType _waveType)
    {
        waveType           = _waveType;
        _originalBallColor = waveBallImage.color;

        delteImage.gameObject.SetActive(false);

        if (waveType == WaveType.Boss)
            rect.localScale = Vector3.one * BOSS_SCALE;
    }

    public void MarkDefeated()
    {
        rect.DOKill();
        rect.DOPunchScale(Vector3.one * 0.18f, 0.35f, 5, 0.3f);

        waveBallImage.DOKill();
        waveBallImage.DOColor(
            new Color(0.4f, 0.4f, 0.4f, waveBallImage.color.a), 0.25f);

        delteImage.gameObject.SetActive(true);
        delteImage.transform.DOKill();
        delteImage.transform.localScale = Vector3.one * 1.7f;
        DOTween.Sequence()
            .Append(delteImage.transform.DOScale(1f, 0.18f).SetEase(Ease.OutBack))
            .Append(delteImage.transform.DOPunchScale(Vector3.one * 0.09f, 0.28f, 4, 0.3f));
    }

    public void Clear()
    {
        rect.DOKill();
        waveBallImage.DOKill();
        delteImage.transform.DOKill();

        delteImage.gameObject.SetActive(false);
        waveBallImage.color = _originalBallColor;
        rect.localScale     = waveType == WaveType.Boss ? Vector3.one * BOSS_SCALE : Vector3.one;
    }
}

public enum WaveType
{
    None   = 0,
    Normal,
    Boss,
}
