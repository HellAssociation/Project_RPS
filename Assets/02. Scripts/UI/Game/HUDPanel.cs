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
    [SerializeField] Image         playerHPImage;
    [SerializeField] Image         playerFollowHPImage;
    [SerializeField] RectTransform playerHPRect;

    [Header("Enemy")]
    [SerializeField] Image         enemyHPImage;
    [SerializeField] Image         enemyFollowHPImage;
    [SerializeField] RectTransform enemyHPRect;

    [Header("HP Bar")]
    [SerializeField] float fillDuration      = 1.7f;

    [Header("HP Bar - Ghost Drain")]
    [Tooltip("빨간 잔상 영역이 줄어들기 전까지 유지되는 시간(초)")]
    [SerializeField] float ghostHoldSeconds         = 0.3f;
    [Tooltip("플레이어 잔상이 다 줄어드는 데 걸리는 시간(초)")]
    [SerializeField] float playerGhostDrainDuration = 0.4f;
    [Tooltip("적 잔상이 다 줄어드는 데 걸리는 시간(초)")]
    [SerializeField] float enemyGhostDrainDuration  = 0.25f;
    [Tooltip("잔상이 줄어드는 Ease")]
    [SerializeField] Ease  ghostDrainEase           = Ease.OutCubic;

    [Header("Hit Shake")]
    [SerializeField] RectTransform playerHudShakeRect;
    [SerializeField] RectTransform enemyShakeRect;
    [SerializeField] float shakeDuration  = 0.3f;
    [SerializeField] float shakeStrength  = 16f;
    [SerializeField] int   shakeVibrato   = 28;

    Vector2 _hudShakeOrigin;

    InGameManager InGame => App.SceneManager.InGame;

    void Start()
    {
        if (playerHudShakeRect != null)
            _hudShakeOrigin = playerHudShakeRect.anchoredPosition;

        if (InGame == null) return;
        InGame.OnReadyStarted    += PlayFillIn;
        InGame.OnLivesChanged    += HandleLivesChanged;
        InGame.OnEnemyHpChanged  += HandleEnemyHpChanged;
        InGame.OnStageOpened     += HandleStageOpened;
    }

    void OnDestroy()
    {
        if (InGame == null) return;
        InGame.OnReadyStarted   -= PlayFillIn;
        InGame.OnLivesChanged   -= HandleLivesChanged;
        InGame.OnEnemyHpChanged -= HandleEnemyHpChanged;
        InGame.OnStageOpened    -= HandleStageOpened;
    }

    void HandleStageOpened(int stageIndex)
    {
        PlayFillIn();
    }

    void PlayFillIn()
    {
        if (playerHPImage != null)       playerHPImage.DOKill();
        if (playerFollowHPImage != null) playerFollowHPImage.DOKill();
        if (enemyHPImage != null)        enemyHPImage.DOKill();
        if (enemyFollowHPImage != null)  enemyFollowHPImage.DOKill();

        if (playerHPImage != null)       playerHPImage.fillAmount       = 0f;
        if (playerFollowHPImage != null) playerFollowHPImage.fillAmount = 0f;
        if (enemyHPImage != null)        enemyHPImage.fillAmount        = 0f;
        if (enemyFollowHPImage != null)  enemyFollowHPImage.fillAmount  = 0f;

        if (playerHPImage != null)       playerHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);
        if (playerFollowHPImage != null) playerFollowHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);
        if (enemyHPImage != null)        enemyHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);
        if (enemyFollowHPImage != null)  enemyFollowHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);
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
        if (enemyHPImage != null)       enemyHPImage.DOKill();
        if (enemyFollowHPImage != null) enemyFollowHPImage.DOKill();

        if (target >= 1f)
        {
            if (enemyHPImage != null)       enemyHPImage.fillAmount       = 1f;
            if (enemyFollowHPImage != null) enemyFollowHPImage.fillAmount = 1f;
            return;
        }

        FlashHpBar(enemyHPImage, enemyHPRect, target);
        DrainGhost(enemyFollowHPImage, target, enemyGhostDrainDuration);
    }

    void PlayDrain(float target)
    {
        if (playerHPImage != null)       playerHPImage.DOKill();
        if (playerFollowHPImage != null) playerFollowHPImage.DOKill();

        FlashHpBar(playerHPImage, playerHPRect, target);
        PlayHitShake();

        DrainGhost(playerFollowHPImage, target, playerGhostDrainDuration);
    }

    void FlashHpBar(Image hpImage, RectTransform hpRect, float target)
    {
        if (hpImage != null)
        {
            hpImage.fillAmount = target;
            hpImage.DOColor(Color.white, 0.05f).SetLoops(2, LoopType.Yoyo);
        }

        if (hpRect != null)
        {
            hpRect.DOKill();
            hpRect.DOPunchScale(new Vector3(0.02f, 0.14f, 0f), 0.28f, 5, 0.3f);
        }
    }

    // Tekken-style hit jolt: short, sharp shake on the HUD when the player takes damage
    void PlayHitShake()
    {
        if (playerHudShakeRect == null) return;

        playerHudShakeRect.DOKill();
        playerHudShakeRect.anchoredPosition = _hudShakeOrigin;
        playerHudShakeRect.DOShakeAnchorPos(shakeDuration, shakeStrength, shakeVibrato, 90f, false, true)
            .OnComplete(() => playerHudShakeRect.anchoredPosition = _hudShakeOrigin);
    }

    void DrainGhost(Image follow, float target, float duration)
    {
        if (follow == null) return;
        if (follow.fillAmount <= target) { follow.fillAmount = target; return; }

        follow.DOFillAmount(target, duration)
              .SetDelay(ghostHoldSeconds)
              .SetEase(ghostDrainEase);
    }
}
