using System;
using SystemEnums;
using UnityEngine;

/// <summary>
/// Resolves a CardRef to its data + effect and applies it. Stat effects modify the RunState;
/// behavioral effects register into the CardEffectRuntime. Card choices are host-authoritative
/// and broadcast (NetworkManager.OnCardsApplied), so every client applies the same effects.
/// </summary>
public static class CardSystem
{
    static DataManager Data => App.Data.BaseData;

    public static void Apply(RunState run, CardRef card, CardEffectRuntime runtime)
    {
        if (run == null || !TryResolve(card, out string code, out int[] values))
            return;

        if (!Enum.TryParse(code, ignoreCase: true, out ECardEffect id) ||
            !CardEffectRegistry.TryCreate(id, values, out object effect))
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[CardSystem] No effect registered for card code: {code}");
#endif
            return;
        }

        if (effect is ICardEffect stat)
            stat.Apply(run);

        if (runtime != null && effect is IRuntimeCardEffect)
            runtime.Register(effect);
    }

    static bool TryResolve(CardRef card, out string code, out int[] values)
    {
        code = null;
        values = null;

        if (Data == null)
            return false;

        switch (card.Kind)
        {
            case ECardKind.Boon:
                if (Data.TryGetBoon((EBoon)card.Index, out BoonData b))
                {
                    code = b.code;
                    values = new[] { b.value1, b.value2, b.value3, b.value4, b.value5 };
                    return true;
                }
                break;

            case ECardKind.Deviation:
                if (Data.TryGetDeviation((EDeviation)card.Index, out DeviationData d))
                {
                    code = d.code;
                    values = new[] { d.value1, d.value2, d.value3, d.value4, d.value5 };
                    return true;
                }
                break;
        }

        return false;
    }
}
