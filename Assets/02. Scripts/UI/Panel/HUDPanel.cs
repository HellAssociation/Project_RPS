using DG.Tweening;
using SystemEnums;
using UnityEngine;
using UnityEngine.UI;

public class HUDPanel : PanelBase
{
    #region [Function] Inheritance
    public override EUIType UIType => EUIType.HUD;
    #endregion

    [Header("Player")]
    [SerializeField] Image playerHPImage;
    [SerializeField] Image playerFollowHPImage;

    [Header("Enemy")]
    [SerializeField] Image enemyHPImage;
    [SerializeField] Image enemyFollowHPImage;

    [Header("HP Bar")]
    [SerializeField] float fillDuration = 1.7f;

    [Header("HP Bar - Drain Timing")]
    [Tooltip("1. 재생 딜레이(시작 딜레이, 초)")]
    [SerializeField] float drainStartDelay = 0.3f;
    [Tooltip("2. 본 체력바가 줄어드는 시간(초)")]
    [SerializeField] float mainDrainDuration = 0.1f;
    [Tooltip("3. 본 체력바가 줄어드는 Ease")]
    [SerializeField] Ease mainDrainEase = Ease.OutQuad;
    [Tooltip("4. 빨간색 체력바가 감소하는 시간(초)")]
    [SerializeField] float ghostDrainDuration = 0.25f;
    [Tooltip("5. 빨간색 체력바가 감소하는 Ease")]
    [SerializeField] Ease ghostDrainEase = Ease.OutCubic;

    InGameManager InGame => App.SceneManager.InGame;

    void Start()
    {
        if (InGame == null) return;
        InGame.OnReadyStarted += PlayFillIn;
        InGame.OnLivesChanged += HandleLivesChanged;
        InGame.OnEnemyHpChanged += HandleEnemyHpChanged;
        InGame.OnStageOpened += HandleStageOpened;
    }

    void OnDestroy()
    {
        if (InGame == null) return;
        InGame.OnReadyStarted -= PlayFillIn;
        InGame.OnLivesChanged -= HandleLivesChanged;
        InGame.OnEnemyHpChanged -= HandleEnemyHpChanged;
        InGame.OnStageOpened -= HandleStageOpened;
    }

    void HandleStageOpened(int stageIndex)
    {
        PlayFillIn();
    }

    void PlayFillIn()
    {
        if (playerHPImage != null) playerHPImage.DOKill();
        if (playerFollowHPImage != null) playerFollowHPImage.DOKill();
        if (enemyHPImage != null) enemyHPImage.DOKill();
        if (enemyFollowHPImage != null) enemyFollowHPImage.DOKill();

        if (playerHPImage != null) playerHPImage.fillAmount = 0f;
        if (playerFollowHPImage != null) playerFollowHPImage.fillAmount = 0f;
        if (enemyHPImage != null) enemyHPImage.fillAmount = 0f;
        if (enemyFollowHPImage != null) enemyFollowHPImage.fillAmount = 0f;

        if (playerHPImage != null) playerHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);
        if (playerFollowHPImage != null) playerFollowHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);
        if (enemyHPImage != null) enemyHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);
        if (enemyFollowHPImage != null) enemyFollowHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);
    }

    void HandleLivesChanged(int lives)
    {
        int maxLives = InGame != null ? InGame.MaxLives : PlayerManager.MAX_HP;
        PlayDrain((float)lives / maxLives);
    }

    void HandleEnemyHpChanged(int hp, int maxHp)
    {
        if (maxHp <= 0) return;
        PlayEnemyDrain((float)Mathf.Max(hp, 0) / maxHp);
    }

    void PlayEnemyDrain(float target)
    {
        DrainBars(enemyHPImage, enemyFollowHPImage, target);
    }

    void PlayDrain(float target)
    {
        DrainBars(playerHPImage, playerFollowHPImage, target);
    }

    void DrainBars(Image main, Image ghost, float target)
    {
        if (main != null) main.DOKill();
        if (ghost != null) ghost.DOKill();

        if (target >= 1f)
        {
            if (main != null) main.fillAmount = 1f;
            if (ghost != null) ghost.fillAmount = 1f;
            return;
        }

        if (main != null) main.DOColor(Color.white, 0.05f).SetLoops(2, LoopType.Yoyo);

        DrainBar(main, target, mainDrainDuration, mainDrainEase);
        DrainBar(ghost, target, ghostDrainDuration, ghostDrainEase);
    }

    void DrainBar(Image bar, float target, float duration, Ease ease)
    {
        if (bar == null) return;
        if (bar.fillAmount <= target) { bar.fillAmount = target; return; }

        bar.DOFillAmount(target, duration)
           .SetDelay(drainStartDelay)
           .SetEase(ease);
    }
}
