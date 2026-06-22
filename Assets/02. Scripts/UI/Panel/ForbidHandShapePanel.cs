using DG.Tweening;
using SystemEnums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ForbidHandShapePanel : PanelBase
{
    #region [Function] Inheritance
    public override EUIType UIType => EUIType.ForbidHandShape;
    #endregion

    [SerializeField] ForbidHandShapeSlot rock;
    [SerializeField] ForbidHandShapeSlot paper;
    [SerializeField] ForbidHandShapeSlot scissors;

    [System.Serializable]
    private struct ForbidHandShapeSlot
    {
        public RectTransform HandShapeRect;
        public Image           IconDim;
        public Image           IconFill;
        public TextMeshProUGUI coolDownText;
        public Image           Flash;
    }

    float _rockRemaining,     _rockTotal;
    float _paperRemaining,    _paperTotal;
    float _scissorsRemaining, _scissorsTotal;

    static readonly Color DIM_COLOR  = new(0f, 0f, 0f, 180f / 255f);
    static readonly Color FILL_COLOR = new(0f, 0f, 0f, 160f / 255f);

    protected override void Awake()
    {
        base.Awake();
        InitSlot(rock);
        InitSlot(paper);
        InitSlot(scissors);
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.R))
            SetCooldown(EHandPosition.Rock, 5f);
#endif

        TickSlot(ref _rockRemaining,     _rockTotal,     rock);
        TickSlot(ref _paperRemaining,    _paperTotal,    paper);
        TickSlot(ref _scissorsRemaining, _scissorsTotal, scissors);
    }

    public void SetCooldown(EHandPosition hand, float duration)
    {
        switch (hand)
        {
            case EHandPosition.Rock:
                _rockRemaining = _rockTotal = duration;
                ActivateSlot(rock);
                break;
            case EHandPosition.Paper:
                _paperRemaining = _paperTotal = duration;
                ActivateSlot(paper);
                break;
            case EHandPosition.Scissors:
                _scissorsRemaining = _scissorsTotal = duration;
                ActivateSlot(scissors);
                break;
        }
    }

    void InitSlot(ForbidHandShapeSlot slot)
    {
        slot.IconDim.color          = Color.clear;
        slot.IconFill.color         = Color.clear;
        slot.IconFill.fillAmount    = 1f;
        slot.Flash.color            = Color.clear;
        slot.coolDownText.gameObject.SetActive(false);
    }

    void ActivateSlot(ForbidHandShapeSlot slot)
    {
        slot.IconDim.DOKill();
        slot.Flash.DOKill();
        slot.HandShapeRect.DOKill();
        slot.IconDim.color       = DIM_COLOR;
        slot.IconFill.color      = FILL_COLOR;
        slot.IconFill.fillAmount = 1f;
        slot.Flash.color         = Color.clear;
        slot.coolDownText.gameObject.SetActive(true);
    }

    void TickSlot(ref float remaining, float total, ForbidHandShapeSlot slot)
    {
        if (remaining <= 0f || total <= 0f) return;

        remaining -= Time.deltaTime;

        if (remaining <= 0f)
        {
            remaining = 0f;
            CompleteSlot(slot);
            return;
        }

        slot.IconFill.fillAmount = remaining / total;
        slot.coolDownText.text   = remaining < 1f
            ? remaining.ToString("F1")
            : Mathf.CeilToInt(remaining).ToString();
    }

    void CompleteSlot(ForbidHandShapeSlot slot)
    {
        slot.IconFill.fillAmount = 0f;
        slot.coolDownText.gameObject.SetActive(false);

        slot.IconDim.DOFade(0f, 0.2f);

        slot.Flash.color = Color.clear;
        DOTween.Sequence()
            .Append(slot.Flash.DOFade(1f, 0.07f))
            .Append(slot.Flash.DOFade(0f, 0.4f));

        slot.HandShapeRect.DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.4f);
    }
}
