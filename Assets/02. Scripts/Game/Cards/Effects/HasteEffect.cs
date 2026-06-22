using SystemEnums;

/// <summary>빠른손: shortens the wave timer by a percentage (stat modifier, applied once).</summary>
public class HasteEffect : ICardEffect
{
    const int DefaultPercent = 10;
    readonly float _multiplier;

    public HasteEffect(int percentFaster)
    {
        int percent = percentFaster > 0 ? percentFaster : DefaultPercent;
        _multiplier = 1f - percent / 100f;
    }

    public void Apply(RunState run) => run.AddModifier(new StatModifier(EStat.WaveTimer, EModifierOp.Mul, _multiplier, this));
}
