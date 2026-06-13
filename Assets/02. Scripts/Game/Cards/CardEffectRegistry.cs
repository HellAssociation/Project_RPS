using System;
using System.Collections.Generic;
using SystemEnums;

/// <summary>
/// Maps a card code to its effect. value1~5 from the data become the effect's parameters.
/// The entries below are EXAMPLES wiring the placeholder BOON_A/B/C codes; replace with the
/// real card table as content is defined. Complex (non-stat) cards register an ICardEffect directly.
/// </summary>
public static class CardEffectRegistry
{
    static readonly Dictionary<string, Func<int[], ICardEffect>> Factories = new()
    {
        { "BOON_A", v => new StatModifierEffect(EStat.PlayerDamage,      EModifierOp.Add, v[0]) },
        { "BOON_B", v => new StatModifierEffect(EStat.WaveTimer,         EModifierOp.Add, v[0]) },
        { "BOON_C", v => new StatModifierEffect(EStat.EnemyHpMultiplier, EModifierOp.Mul, ToPercent(v[0])) },
    };

    public static bool TryCreate(string code, int[] values, out ICardEffect effect)
    {
        effect = null;
        if (string.IsNullOrEmpty(code) || !Factories.TryGetValue(code, out Func<int[], ICardEffect> factory))
            return false;

        effect = factory(values);
        return true;
    }

    static float ToPercent(int value) => 1f + value / 100f;
}

