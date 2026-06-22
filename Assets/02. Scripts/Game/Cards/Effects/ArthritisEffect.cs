using SystemEnums;

/// <summary>관절염: locks all input after a fixed number of toggles per match (resets each match).</summary>
public class ArthritisEffect : IWaveBeginEffect, IToggleEffect
{
    const int DefaultMax = 3;
    const int ToggleOrder = 200;

    readonly int _maxToggles;
    int _count;

    public ArthritisEffect(int maxToggles)
    {
        _maxToggles = maxToggles > 0 ? maxToggles : DefaultMax;
    }

    public ECardEffect Id => ECardEffect.Deviation_Arthritis;
    public int Order => ToggleOrder;

    public void OnWaveBegin(IInputControl control) => _count = 0;

    public bool CanToggle(EFingerType finger, bool willOpen) => _count < _maxToggles;

    public void OnToggled(EFingerType finger, bool willOpen) => _count++;
}
