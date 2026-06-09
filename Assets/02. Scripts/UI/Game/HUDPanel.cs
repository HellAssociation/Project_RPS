using System.Collections;
using DG.Tweening;
using SystemEnums;
using UnityEngine;
using UnityEngine.UI;

public class HUDPanel : PanelBase
{
    #region [Function] Inheritance
    public override bool IsOpened => _panelGameObject.activeSelf;
    public override bool CanCloseWithESC => false;
    public override bool IsStackable => false;
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
    [SerializeField] float delaySeconds      = 0.3f;
    [SerializeField] float ghostSpeed        = 2.5f;
    [SerializeField] float enemyGhostSpeed   = 6f;

    [Header("Hit Shake")]
    [SerializeField] RectTransform playerHudShakeRect;
    [SerializeField] RectTransform enemyShakeRect;
    [SerializeField] float shakeDuration  = 0.3f;
    [SerializeField] float shakeStrength  = 16f;
    [SerializeField] int   shakeVibrato   = 28;

    float     _playerDelayed;
    float     _enemyDelayed;
    Vector2   _hudShakeOrigin;
    Coroutine _ghostCoroutine;
    Coroutine _enemyGhostCoroutine;

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
        if (_ghostCoroutine != null)      { StopCoroutine(_ghostCoroutine);      _ghostCoroutine      = null; }
        if (_enemyGhostCoroutine != null) { StopCoroutine(_enemyGhostCoroutine); _enemyGhostCoroutine = null; }

        if (playerHPImage != null)       playerHPImage.DOKill();
        if (playerFollowHPImage != null) playerFollowHPImage.DOKill();
        if (enemyHPImage != null)        enemyHPImage.DOKill();
        if (enemyFollowHPImage != null)  enemyFollowHPImage.DOKill();

        _playerDelayed = 0f;
        _enemyDelayed  = 0f;

        if (playerHPImage != null)       playerHPImage.fillAmount       = 0f;
        if (playerFollowHPImage != null) playerFollowHPImage.fillAmount = 0f;
        if (enemyHPImage != null)        enemyHPImage.fillAmount        = 0f;
        if (enemyFollowHPImage != null)  enemyFollowHPImage.fillAmount  = 0f;

        if (playerHPImage != null)
            playerHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);

        if (playerFollowHPImage != null)
            playerFollowHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad)
                .OnUpdate(() => _playerDelayed = playerFollowHPImage.fillAmount)
                .OnComplete(() => _playerDelayed = 1f);

        if (enemyHPImage != null)
            enemyHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);

        if (enemyFollowHPImage != null)
            enemyFollowHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad)
                .OnUpdate(() => _enemyDelayed = enemyFollowHPImage.fillAmount)
                .OnComplete(() => _enemyDelayed = 1f);
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
        if (_enemyGhostCoroutine != null) { StopCoroutine(_enemyGhostCoroutine); _enemyGhostCoroutine = null; }

        if (target >= 1f)
        {
            if (enemyHPImage != null)       enemyHPImage.fillAmount       = 1f;
            if (enemyFollowHPImage != null) enemyFollowHPImage.fillAmount = 1f;
            _enemyDelayed = 1f;
            return;
        }

        if (enemyHPImage != null)
        {
            enemyHPImage.fillAmount = target;
            enemyHPImage.DOColor(Color.white, 0.05f).SetLoops(2, LoopType.Yoyo);
        }

        if (enemyHPRect != null)
        {
            enemyHPRect.DOKill();
            enemyHPRect.DOPunchScale(new Vector3(0.02f, 0.14f, 0f), 0.28f, 5, 0.3f);
        }

        _enemyGhostCoroutine = StartCoroutine(EnemyGhostDrainCoroutine(target));
    }

    IEnumerator EnemyGhostDrainCoroutine(float target)
    {
        float timer = delaySeconds;
        while (timer > 0f) { timer -= Time.deltaTime; yield return null; }

        while (_enemyDelayed > target)
        {
            _enemyDelayed -= enemyGhostSpeed * Time.deltaTime;
            _enemyDelayed  = Mathf.Max(_enemyDelayed, target);
            if (enemyFollowHPImage != null)
                enemyFollowHPImage.fillAmount = _enemyDelayed;
            yield return null;
        }

        _enemyGhostCoroutine = null;
    }

    void PlayDrain(float target)
    {
        if (playerHPImage != null)       playerHPImage.DOKill();
        if (playerFollowHPImage != null) playerFollowHPImage.DOKill();

        if (playerHPImage != null)
        {
            playerHPImage.fillAmount = target;
            playerHPImage.DOColor(Color.white, 0.05f).SetLoops(2, LoopType.Yoyo);
        }

        if (playerHPRect != null)
        {
            playerHPRect.DOKill();
            playerHPRect.DOPunchScale(new Vector3(0.02f, 0.14f, 0f), 0.28f, 5, 0.3f);
        }

        PlayHitShake();

        if (_ghostCoroutine != null) StopCoroutine(_ghostCoroutine);
        _ghostCoroutine = StartCoroutine(GhostDrainCoroutine(target));
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

    IEnumerator GhostDrainCoroutine(float target)
    {
        float timer = delaySeconds;
        while (timer > 0f) { timer -= Time.deltaTime; yield return null; }

        while (_playerDelayed > target)
        {
            _playerDelayed -= ghostSpeed * Time.deltaTime;
            _playerDelayed  = Mathf.Max(_playerDelayed, target);
            if (playerFollowHPImage != null)
                playerFollowHPImage.fillAmount = _playerDelayed;
            yield return null;
        }

        _ghostCoroutine = null;
    }
}
