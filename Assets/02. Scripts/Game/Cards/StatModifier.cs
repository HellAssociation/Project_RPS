using SystemEnums;

/// <summary>A single stat adjustment contributed by a card. Resolved by RunState.GetStat.</summary>
public readonly struct StatModifier
{
    public readonly EStat Stat;
    public readonly EModifierOp Op;
    public readonly float Value;
    public readonly object Source;

    public StatModifier(EStat stat, EModifierOp op, float value, object source = null)
    {
        Stat   = stat;
        Op     = op;
        Value  = value;
        Source = source;
    }
}
