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
    [SerializeField] float fillDuration = 1.7f;
    [SerializeField] float delaySeconds = 0.3f;
    [SerializeField] float ghostSpeed   = 2.5f;

    float     _playerDelayed;
    Coroutine _ghostCoroutine;

    InGameManager InGame => App.SceneManager.InGame;

    void Start()
    {
        if (InGame == null) return;
        InGame.OnReadyStarted += PlayFillIn;
        InGame.OnLivesChanged += HandleLivesChanged;
        InGame.OnStageOpened += HandleStageOpened;
    }

    void OnDestroy()
    {
        if (InGame == null) return;
        InGame.OnReadyStarted -= PlayFillIn;
        InGame.OnLivesChanged -= HandleLivesChanged;
        InGame.OnStageOpened -= HandleStageOpened;
    }

    void HandleStageOpened(int stageIndex)
    {
        PlayFillIn();
    }

    void PlayFillIn()
    {
        if (_ghostCoroutine != null) { StopCoroutine(_ghostCoroutine); _ghostCoroutine = null; }

        if (playerHPImage != null)       playerHPImage.DOKill();
        if (playerFollowHPImage != null) playerFollowHPImage.DOKill();
        if (enemyHPImage != null)        enemyHPImage.DOKill();
        if (enemyFollowHPImage != null)  enemyFollowHPImage.DOKill();

        _playerDelayed = 0f;

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
            enemyFollowHPImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad);
    }

    void HandleLivesChanged(int lives)
    {
        PlayDrain((float)lives / PlayerManager.MAX_HP);
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

        if (_ghostCoroutine != null) StopCoroutine(_ghostCoroutine);
        _ghostCoroutine = StartCoroutine(GhostDrainCoroutine(target));
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
