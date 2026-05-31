using System.Collections;
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

    Coroutine _randomCoroutine;

    void Start()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnRoundStarted += HandleRoundStarted;
    }

    void OnDestroy()
    {
        InGameManager inGame = App.SceneManager.InGame;
        if (inGame == null) return;
        inGame.OnRoundStarted -= HandleRoundStarted;
    }

    void HandleRoundStarted(float duration)
    {
        if (_randomCoroutine != null)
        {
            StopCoroutine(_randomCoroutine);
            _randomCoroutine = null;
        }

        EHandPosition slotPosition = GetFrontSlotPosition();

        if (slotPosition == EHandPosition.Random)
        {
            _randomCoroutine = StartCoroutine(RandomCycleCoroutine(duration));
        }
        else
        {
            ApplyHandPosition(slotPosition);
            App.SceneManager.InGame?.SetEnemyHandPosition(slotPosition);
        }
    }

    IEnumerator RandomCycleCoroutine(float totalDuration)
    {
        float elapsed = 0f;
        float lockAt = Mathf.Max(0f, totalDuration - LOCK_BEFORE_END);

        while (elapsed < lockAt)
        {
            ApplyHandPosition(VALID_POSITIONS[Random.Range(0, VALID_POSITIONS.Length)]);
            yield return new WaitForSeconds(RANDOM_CYCLE_INTERVAL);
            elapsed += RANDOM_CYCLE_INTERVAL;
        }

        EHandPosition locked = VALID_POSITIONS[Random.Range(0, VALID_POSITIONS.Length)];
        ApplyHandPosition(locked);
        App.SceneManager.InGame?.SetEnemyHandPosition(locked);
        _randomCoroutine = null;
    }

    // Reads the current front slot from NextIconPanel via UIManager
    static EHandPosition GetFrontSlotPosition()
    {
        InGameUIManager ui = App.UI.InGame;
        if (ui == null) return EHandPosition.Rock;

        if (ui.TryGetPanel(out NextIconPanel panel))
            return panel.CurrentHandPosition;

        return EHandPosition.Rock;
    }
}
