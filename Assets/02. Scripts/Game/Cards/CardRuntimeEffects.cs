using System.Collections.Generic;
using SystemEnums;
using UnityEngine.InputSystem;

/// <summary>Surface a CardEffectRuntime exposes to input effects for reconfiguring InputManager.</summary>
public interface IInputControl
{
    System.Random Rng { get; }
    void ApplyBindings(IReadOnlyDictionary<EFingerType, Key> bindings);
    void LockFinger(EFingerType finger, bool isOpen);
    void ClearLocks();
}

/// <summary>Identity carried by every runtime (non-stat) card effect.</summary>
public interface IRuntimeCardEffect
{
    ECardEffect Id { get; }
}

/// <summary>Reconfigure input once per round (e.g. KeyChaos remaps keys).</summary>
public interface IRoundBeginEffect : IRuntimeCardEffect
{
    void OnRoundBegin(IInputControl control);
}

/// <summary>Reconfigure input once per match (e.g. Fracture re-rolls a locked finger).</summary>
public interface IWaveBeginEffect : IRuntimeCardEffect
{
    void OnWaveBegin(IInputControl control);
}

/// <summary>Inspect/veto each finger toggle, visited in ascending Order.</summary>
public interface IToggleEffect : IRuntimeCardEffect
{
    int Order { get; }
    bool CanToggle(EFingerType finger, bool willOpen);
    void OnToggled(EFingerType finger, bool willOpen);
}

/// <summary>Reserved hook fired at win/loss judgment (no test card uses it yet).</summary>
public interface IJudgeEffect : IRuntimeCardEffect
{
    void OnJudge(EHandPosition self, EHandPosition enemy, EOutcome outcome);
}
