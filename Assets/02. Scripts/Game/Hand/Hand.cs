using System.Collections.Generic;
using MoreMountains.Feedbacks;
using SystemEnums;
using UnityEngine;

[DefaultExecutionOrder((int)EExecutionOrder.GameContent)]
public abstract class Hand : MonoBehaviour
{
    [SerializeField] SpriteRenderer handSpriteRenderer;
    [SerializeField] Finger[] fingers;
    [SerializeField] EHandImpactOwner side;
    [SerializeField] MMF_Player attackEffect;
    [SerializeField] MMF_Player hitEffect;

    protected const int FINGER_COUNT = 5;
    protected Dictionary<EFingerType, Finger> fingerTable;

    InGameManager _inGame;

    protected virtual void Awake()
    {
        if (handSpriteRenderer == null)
            handSpriteRenderer = GetComponent<SpriteRenderer>();

        CacheFingers();
        CacheOutcomeEffects();
    }

    protected virtual void OnEnable()
    {
        _inGame = App.SceneManager.InGame;
        if (_inGame != null)
            _inGame.OnOutcomeDetermined += HandleOutcome;
    }

    protected virtual void OnDisable()
    {
        if (_inGame != null)
            _inGame.OnOutcomeDetermined -= HandleOutcome;
    }

    void CacheFingers()
    {
        if (fingers == null || fingers.Length != FINGER_COUNT)
            fingers = GetComponentsInChildren<Finger>();

        if (fingers == null || fingers.Length != FINGER_COUNT)
        {
            Debug.LogError("[Error] Can't find fingers!");
            return;
        }

        fingerTable = new(FINGER_COUNT);

        for (int i = 0; i < FINGER_COUNT; i++)
        {
            if (!fingerTable.TryAdd(fingers[i].FingerType, fingers[i]))
            {
                Debug.LogError($"[Error] {fingers[i]} : index = {i} can't add finger!");
                continue;
            }

            fingers[i].Init();
        }
    }

    void CacheOutcomeEffects()
    {
        if (attackEffect == null)
        {
            Transform attack = transform.Find("AttackEffect");
            if (attack != null) attackEffect = attack.GetComponent<MMF_Player>();
        }

        if (hitEffect == null)
        {
            Transform hit = transform.Find("HitEffect");
            if (hit != null) hitEffect = hit.GetComponent<MMF_Player>();
        }
    }

    void HandleOutcome(EOutcome outcome)
    {
        if (outcome == EOutcome.Draw) return;

        bool playerWon = outcome == EOutcome.Win;
        bool playAttack = side == EHandImpactOwner.Player ? playerWon : !playerWon;

        if (playAttack)
            attackEffect?.PlayFeedbacks();
        else
            hitEffect?.PlayFeedbacks();
    }

    protected Finger GetFinger(EFingerType _fingerType)
    {
        if (fingerTable.TryGetValue(_fingerType, out var finger))
            return finger;

        Debug.LogError($"[Error] {_fingerType} can't find finger!");
        return null;
    }

    protected void SetHandSprite(Sprite _sprite)
    {
        if (handSpriteRenderer != null)
            handSpriteRenderer.sprite = _sprite;
    }

    protected void ApplyHandPosition(EHandPosition handPosition)
    {
        if (handPosition == EHandPosition.Invalid || handPosition == EHandPosition.Random) return;
        ApplyFingerMask((EFingerType)(int)handPosition);
    }

    protected void ApplyFingerMask(EFingerType mask)
    {
        if (fingerTable == null) return;
        foreach (var kvp in fingerTable)
            kvp.Value.SetOpen((mask & kvp.Key) != 0);
    }

    protected void CloseAll() => ApplyFingerMask(EFingerType.None);
}

public enum EEnemyHandType
{
    Enemy_01,
    Enemy_02,
    Enemy_03,
    Enemy_04,
    Enemy_05,
    Enemy_06,
    Enemy_07,
    Enemy_08,
    Enemy_09,
    Enemy_10,
}
