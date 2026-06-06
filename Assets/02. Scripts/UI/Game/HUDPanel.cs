using System.Collections;
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
    [SerializeField] Image playerHPImage;
    [SerializeField] Image playerFollowHPImage;

    [Header("Enemy")]
    [SerializeField] Image enemyHPImage;
    [SerializeField] Image enemyFollowHPImage;

    [Header("HP Bar")]
    [SerializeField] float delaySeconds = 0.4f;
    [SerializeField] float drainSpeed   = 0.8f;
    [SerializeField] float fillDuration = 0.8f;

    float _currentFill;
    float _delayedFill;

    Coroutine _drainCoroutine;
    Coroutine _fillCoroutine;

    InGameManager InGame => App.SceneManager.InGame;

    void Start()
    {
        if (InGame != null)
            InGame.OnLivesChanged += HandleLivesChanged;

        PlayFillIn();
    }

    void OnDestroy()
    {
        if (InGame != null)
            InGame.OnLivesChanged -= HandleLivesChanged;
    }

    void HandleLivesChanged(int lives)
    {
        float target = (float)lives / PlayerManager.MAX_HP;

        if (target > _currentFill)
            PlayFillIn();
        else
            PlayDrain(target);
    }

    void PlayDrain(float target)
    {
        if (_fillCoroutine != null)
        {
            StopCoroutine(_fillCoroutine);
            _fillCoroutine = null;
        }
        if (_drainCoroutine != null) StopCoroutine(_drainCoroutine);
        _drainCoroutine = StartCoroutine(DrainCoroutine(target));
    }

    void PlayFillIn()
    {
        if (_drainCoroutine != null)
        {
            StopCoroutine(_drainCoroutine);
            _drainCoroutine = null;
        }
        if (_fillCoroutine != null) StopCoroutine(_fillCoroutine);
        _fillCoroutine = StartCoroutine(FillInCoroutine());
    }

    IEnumerator DrainCoroutine(float target)
    {
        _currentFill = target;
        if (playerHPImage != null) playerHPImage.fillAmount = _currentFill;

        float timer = delaySeconds;
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        while (_delayedFill > _currentFill)
        {
            _delayedFill -= drainSpeed * Time.deltaTime;
            _delayedFill  = Mathf.Max(_delayedFill, _currentFill);
            if (playerFollowHPImage != null) playerFollowHPImage.fillAmount = _delayedFill;
            yield return null;
        }

        _drainCoroutine = null;
    }

    IEnumerator FillInCoroutine()
    {
        float startFill = _currentFill;
        float elapsed   = 0f;

        while (elapsed < fillDuration)
        {
            elapsed += Time.deltaTime;
            float fill = Mathf.Lerp(startFill, 1f, Mathf.Clamp01(elapsed / fillDuration));
            _currentFill = fill;
            _delayedFill = fill;
            if (playerHPImage != null)      playerHPImage.fillAmount      = fill;
            if (playerFollowHPImage != null) playerFollowHPImage.fillAmount = fill;
            yield return null;
        }

        _currentFill = 1f;
        _delayedFill = 1f;
        if (playerHPImage != null)      playerHPImage.fillAmount      = 1f;
        if (playerFollowHPImage != null) playerFollowHPImage.fillAmount = 1f;
        _fillCoroutine = null;
    }
}
