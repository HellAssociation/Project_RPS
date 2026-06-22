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

    [SerializeField] RoundSpriteData roundSpriteData;

    System.Random _rng = new();
    Coroutine _randomCoroutine;

    void Start()
    {
        CloseAll();

        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnStageOpened   += HandleStageOpened;
        inGame.OnWaveStarted   += HandleRoundStarted;
        inGame.OnEnemyAppeared += HandleEnemyAppeared;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnStageOpened   -= HandleStageOpened;
        inGame.OnWaveStarted   -= HandleRoundStarted;
        inGame.OnEnemyAppeared -= HandleEnemyAppeared;
    }

    void HandleStageOpened(int _)
    {
        InGameManager inGame = App.SceneManager.InGame;
        _rng = new System.Random(inGame != null ? inGame.EnemySeed : 0);
    }

    void HandleEnemyAppeared(bool isBoss)
    {
        if (TryDrawAppearance(out EEnemyHandType type))
            ApplyAppearance(type);

        SetHandColor(isBoss ? Color.red : Color.white);
    }

    bool TryDrawAppearance(out EEnemyHandType type)
    {
        type = default;

        if (roundSpriteData == null)
        {
            Debug.LogError("[Error] RoundSpriteData is not assigned!");
            return false;
        }

        int round = App.SceneManager.InGame != null ? App.SceneManager.InGame.CurrentRound : 1;
        IReadOnlyList<EEnemyHandType> pool = roundSpriteData.GetPool(round);

        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"[Warning] Round {round} sprite pool is empty. Keeping previous appearance.");
            return false;
        }

        type = pool[_rng.Next(pool.Count)];
        return true;
    }

    void ApplyAppearance(EEnemyHandType _type)
    {
        if (!roundSpriteData.TryGetAppearance(_type, out HandAppearance appearance) || appearance == null)
        {
            Debug.LogError($"[Error] {_type} can't find hand appearance!");
            return;
        }

        SetHandSprite(appearance.HandSprite);

        foreach (EFingerType fingerType in RpsHandUtility.AllFingers)
        {
            Finger finger = GetFinger(fingerType);
            if (finger == null) continue;

            finger.SetSpriteData(new FingerSpriteData(
                appearance.GetFingerSprite(fingerType, true),
                appearance.GetFingerSprite(fingerType, false)));
        }
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
}
