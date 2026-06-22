using SystemEnums;

/// <summary>골절: each match, locks one random finger to a random open/closed state.</summary>
public class FractureEffect : IWaveBeginEffect
{
    static readonly EFingerType[] Fingers =
    {
        EFingerType.Thumb, EFingerType.Index, EFingerType.Middle,
        EFingerType.Ring,  EFingerType.Pinky,
    };

    public ECardEffect Id => ECardEffect.Deviation_Fracture;

    public void OnWaveBegin(IInputControl control)
    {
        EFingerType finger = Fingers[control.Rng.Next(Fingers.Length)];
        bool isOpen = control.Rng.Next(2) == 0;
        control.LockFinger(finger, isOpen);
    }
}
