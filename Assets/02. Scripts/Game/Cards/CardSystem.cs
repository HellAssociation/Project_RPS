using SystemEnums;
using UnityEngine;

/// <summary>
/// Resolves a CardRef to its data + effect and applies it to a RunState.
/// Card choices are host-authoritative and broadcast (NetworkManager.OnCardsApplied),
/// so every client applies the same effects to its local RunState.
/// </summary>
public static class CardSystem
{
    static DataManager Data => App.Data.BaseData;

    public static void Apply(RunState run, CardRef card)
    {
        if (run == null || !TryResolve(card, out string code, out int[] values))
            return;

        if (CardEffectRegistry.TryCreate(code, values, out ICardEffect effect))
            effect.Apply(run);
#if UNITY_EDITOR
        else
            Debug.LogWarning($"[CardSystem] No effect registered for card code: {code}");
#endif
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
