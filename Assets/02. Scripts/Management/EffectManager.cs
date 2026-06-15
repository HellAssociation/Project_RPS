using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using SystemEnums;
using UnityEngine;

[DefaultExecutionOrder((int)EExecutionOrder.BaseManagement)]
public class EffectManager : CommonManagerBase
{
    [Serializable]
    public struct EffectEntry
    {
        public EEffect type;
        public MMF_Player player;
    }

    [Header("Effect Mapping")]
    [SerializeField] private List<EffectEntry> _effects = new();

    private readonly Dictionary<EEffect, MMF_Player> _effectMap = new();

    protected override void Awake()
    {
        base.Awake();
        BuildEffectMap();
    }

    private void BuildEffectMap()
    {
        _effectMap.Clear();

        foreach (var entry in _effects)
        {
            if (entry.type == EEffect.None || entry.player == null)
            {
                continue;
            }

            if (!_effectMap.TryAdd(entry.type, entry.player))
            {
                Debug.LogWarning($"[EffectManager] 중복된 이펙트 키: {entry.type}");
            }
        }
    }

    public void PlayEffect(EEffect effect)
    {
        if (TryGetPlayer(effect, out var player))
        {
            player.PlayFeedbacks();
        }
    }

    public void StopEffect(EEffect effect)
    {
        if (TryGetPlayer(effect, out var player))
        {
            player.StopFeedbacks();
        }
    }

    public void StopAll()
    {
        foreach (var player in _effectMap.Values)
        {
            if (player != null)
            {
                player.StopFeedbacks();
            }
        }
    }

    private bool TryGetPlayer(EEffect effect, out MMF_Player player)
    {
        if (effect == EEffect.None)
        {
            player = null;
            return false;
        }

        if (!_effectMap.TryGetValue(effect, out player))
        {
            Debug.LogWarning($"[EffectManager] 등록되지 않은 이펙트입니다: {effect}");
            return false;
        }

        return true;
    }
}
