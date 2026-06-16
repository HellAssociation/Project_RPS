using System;
using System.Collections.Generic;
using SystemEnums;

/// <summary>
/// Maps a card effect id to its instance. value1~5 from the data become the effect's parameters.
/// Stat effects return an ICardEffect; behavioral effects return an IRuntimeCardEffect.
/// </summary>
public static class CardEffectRegistry
{
    static readonly Dictionary<ECardEffect, Func<int[], object>> Factories = new()
    {
        { ECardEffect.Deviation_Arthritis, v => new ArthritisEffect(v[0]) },
        { ECardEffect.Deviation_KeyChaos,  v => new KeyChaosEffect() },
        { ECardEffect.Deviation_Fracture,  v => new FractureEffect() },
        { ECardEffect.Deviation_Haste,     v => new HasteEffect(v[0]) },
        { ECardEffect.Deviation_Collar,    v => new CollarEffect() },
        { ECardEffect.Deviation_Log,       v => new LogEffect() },
    };

    public static bool TryCreate(ECardEffect id, int[] values, out object effect)
    {
        effect = null;
        if (!Factories.TryGetValue(id, out Func<int[], object> factory))
            return false;

        effect = factory(values);
        return true;
    }
}
