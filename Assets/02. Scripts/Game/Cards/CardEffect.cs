using SystemEnums;

/// <summary>A card's gameplay effect, applied to a RunState. Stat tweaks use StatModifierEffect;
/// complex (non-stat) cards implement this directly.</summary>
public interface ICardEffect
{
    void Apply(RunState run);
}

/// <summary>Most common effect: contributes one StatModifier to the run.</summary>
public class StatModifierEffect : ICardEffect
{
    readonly EStat _stat;
    readonly EModifierOp _op;
    readonly float _value;

    public StatModifierEffect(EStat stat, EModifierOp op, float value)
    {
        _stat  = stat;
        _op    = op;
        _value = value;
    }

    public void Apply(RunState run) => run.AddModifier(new StatModifier(_stat, _op, _value, this));
}
