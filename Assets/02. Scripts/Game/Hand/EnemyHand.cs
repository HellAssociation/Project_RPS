using System.Collections;
using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

public class EnemyHand : Hand
{
    static readonly EHandPosition[] VALID_POSITIONS =
    {
        EHandPosition.Rock,
        EHandPosition.Paper,
        EHandPosition.Scissors,
    };

    const float RANDOM_CYCLE_INTERVAL = 0.1f;
    const float LOCK_BEFORE_END = 1f;
    const int HAND_SPRITE_DATA_COUNT = 10;

    [SerializeField] HandSpriteData[] handSpriteDataSet;

    Dictionary<EEnemyHandType, HandSpriteData> handSpriteDataTable;

    Coroutine _randomCoroutine;

    protected override void Awake()
    {
        base.Awake();
        CacheHandSpriteData();
    }

    void Start()
    {
        CloseAll();

        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnWaveStarted += HandleRoundStarted;
        inGame.OnStageOpened += HandleStageOpened;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnWaveStarted -= HandleRoundStarted;
        inGame.OnStageOpened -= HandleStageOpened;
    }

    void HandleStageOpened(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= HAND_SPRITE_DATA_COUNT) return;
        ApplyHandSpriteData((EEnemyHandType)stageIndex);
    }

    void HandleRoundStarted(float duration)
    {
        if (_randomCoroutine != null)
        {
            StopCoroutine(_randomCoroutine);
            _randomCoroutine = null;
        }

        EHandPosition display  = GetFrontDisplayPosition();
        EHandPosition resolved = GetFrontResolvedPosition();

        if (display == EHandPosition.Random)
        {
            _randomCoroutine = StartCoroutine(RandomCycleCoroutine(duration, resolved));
        }
        else
        {
            ApplyHandPosition(resolved);
            App.SceneManager.InGame?.SetEnemyHandPosition(resolved);
        }
    }

    IEnumerator RandomCycleCoroutine(float totalDuration, EHandPosition locked)
    {
        float elapsed = 0f;
        float lockAt = Mathf.Max(0f, totalDuration - LOCK_BEFORE_END);

        while (elapsed < lockAt)
        {
            ApplyHandPosition(VALID_POSITIONS[Random.Range(0, VALID_POSITIONS.Length)]);
            yield return new WaitForSeconds(RANDOM_CYCLE_INTERVAL);
            elapsed += RANDOM_CYCLE_INTERVAL;
        }

        ApplyHandPosition(locked);
        App.SceneManager.InGame?.SetEnemyHandPosition(locked);
        _randomCoroutine = null;
    }

    // Reads the current front slot from NextIconPanel via UIManager
    static EHandPosition GetFrontDisplayPosition()
    {
        InGameUIManager ui = App.UI.InGame;
        if (ui == null) return EHandPosition.Rock;

        if (ui.TryGetPanel(out NextIconPanel panel))
            return panel.CurrentHandPosition;

        return EHandPosition.Rock;
    }

    static EHandPosition GetFrontResolvedPosition()
    {
        InGameUIManager ui = App.UI.InGame;
        if (ui == null) return EHandPosition.Rock;

        if (ui.TryGetPanel(out NextIconPanel panel))
            return panel.CurrentResolvedHandPosition;

        return EHandPosition.Rock;
    }

    void CacheHandSpriteData()
    {
        handSpriteDataTable = new(HAND_SPRITE_DATA_COUNT);

        if (handSpriteDataSet == null) return;

        foreach (HandSpriteData data in handSpriteDataSet)
        {
            if (data == null) continue;

            if (!handSpriteDataTable.TryAdd(data.HandId, data))
                Debug.LogError($"[Error] {data.HandId} duplicate hand sprite data!");
        }
    }

    public void ApplyHandSpriteData(EEnemyHandType _handType)
    {
        if (handSpriteDataTable == null || !handSpriteDataTable.TryGetValue(_handType, out HandSpriteData data) || data == null)
        {
            Debug.LogError($"[Error] {_handType} can't find hand sprite data!");
            return;
        }

        SetHandSprite(data.HandSprite);

        foreach (EFingerType fingerType in RpsHandUtility.AllFingers)
        {
            Finger finger = GetFinger(fingerType);
            if (finger == null) continue;

            finger.SetSpriteData(new FingerSpriteData(
                data.GetFingerSprite(fingerType, true),
                data.GetFingerSprite(fingerType, false)));
        }
    }
}
