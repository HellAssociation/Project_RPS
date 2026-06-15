using DG.Tweening;
using SystemEnums;
using UnityEngine;
using UnityEngine.UI;

public class OutcomePanel : PanelBase
{
    #region [Function] Inheritance
    public override EUIType UIType => EUIType.OutCome;
    #endregion

    [SerializeField] Image outcomeImage;
    [Tooltip("Win, Lose, Draw")]
    [SerializeField] Sprite[] outcomeSprites;

    Vector2 _originAnchoredPos;

    protected override void Awake()
    {
        base.Awake();
        _originAnchoredPos = outcomeImage.rectTransform.anchoredPosition;
    }

    void Start()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnOutcomeDetermined += ShowOutcome;
        inGame.OnWaveResultShown += HideOutcome;
        inGame.OnWaveStarted += HideOutcomeOnRoundStart;
        inGame.OnGameOver += HideOutcome;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnOutcomeDetermined -= ShowOutcome;
        inGame.OnWaveResultShown -= HideOutcome;
        inGame.OnWaveStarted -= HideOutcomeOnRoundStart;
        inGame.OnGameOver -= HideOutcome;
    }

    void ShowOutcome(EOutcome outcome)
    {
        outcomeImage.sprite = outcomeSprites[(int)outcome];
        OpenPanel();
        PlayAnimation(outcome);
    }

    void HideOutcome()
    {
        outcomeImage.DOKill();
        outcomeImage.transform.DOKill();
        ResetImageState();
        ClosePanel();
    }

    void HideOutcomeOnRoundStart(float _) => HideOutcome();

    void ResetImageState()
    {
        outcomeImage.rectTransform.anchoredPosition = _originAnchoredPos;
        outcomeImage.transform.localScale = Vector3.one;
        outcomeImage.color = Color.white;
    }

    void PlayAnimation(EOutcome outcome)
    {
        outcomeImage.DOKill();
        outcomeImage.transform.DOKill();
        ResetImageState();

        switch (outcome)
        {
            case EOutcome.Win:  PlayWinAnimation();  break;
            case EOutcome.Lose: PlayLoseAnimation(); break;
            case EOutcome.Draw: PlayDrawAnimation(); break;
        }
    }

    // Scale-pop burst — celebratory and snappy
    void PlayWinAnimation()
    {
        outcomeImage.transform.localScale = Vector3.zero;
        outcomeImage.color = new Color(1f, 1f, 1f, 0f);

        Sequence sequence = DOTween.Sequence();
        sequence.Append(outcomeImage.transform.DOScale(1.3f, 0.35f).SetEase(Ease.OutBack));
        sequence.Join(outcomeImage.DOFade(1f, 0.18f));
        sequence.Append(outcomeImage.transform.DOScale(1f, 0.15f).SetEase(Ease.OutSine));
    }

    // Drops from above and thuds — heavy, deflating
    void PlayLoseAnimation()
    {
        outcomeImage.rectTransform.anchoredPosition = _originAnchoredPos + new Vector2(0f, 180f);
        outcomeImage.color = new Color(1f, 1f, 1f, 0f);

        Sequence sequence = DOTween.Sequence();
        sequence.Append(outcomeImage.DOFade(1f, 0.08f));
        sequence.Append(outcomeImage.rectTransform
            .DOAnchorPos(_originAnchoredPos + new Vector2(0f, -15f), 0.38f)
            .SetEase(Ease.InCubic));
        sequence.Append(outcomeImage.rectTransform
            .DOAnchorPos(_originAnchoredPos, 0.15f)
            .SetEase(Ease.OutSine));
        sequence.AppendCallback(() =>
            outcomeImage.transform.DOPunchScale(new Vector3(0.05f, -0.22f, 0f), 0.35f, 2, 0f));
    }

    // Gentle fade-scale in with a soft pulse — neutral
    void PlayDrawAnimation()
    {
        outcomeImage.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
        outcomeImage.color = new Color(1f, 1f, 1f, 0f);

        Sequence sequence = DOTween.Sequence();
        sequence.Append(outcomeImage.transform.DOScale(1f, 0.35f).SetEase(Ease.OutSine));
        sequence.Join(outcomeImage.DOFade(1f, 0.3f));
        sequence.Append(outcomeImage.transform.DOPunchScale(new Vector3(0.06f, 0.06f, 0f), 0.4f, 3, 0.5f));
    }
}
