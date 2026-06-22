using System.Collections.Generic;
using SystemEnums;
using UnityEngine.InputSystem;

/// <summary>
/// Holds the active behavioral (non-stat) card effects for one run, dispatches round/match
/// lifecycle hooks, and feeds the Order-sorted toggle chain to InputManager. Acts as the
/// IInputControl effects use to reconfigure input.
/// </summary>
public class CardEffectRuntime : IInputControl
{
    readonly List<IRoundBeginEffect> _roundBegin = new();
    readonly List<IWaveBeginEffect> _waveBegin = new();
    readonly List<IToggleEffect> _toggle = new();
    readonly System.Random _rng = new();

    InputManager _input;

    public System.Random Rng => _rng;

    public void Bind(InputManager input)
    {
        _input = input;
        _input?.SetToggleEffects(_toggle);
    }

    public void Clear()
    {
        _roundBegin.Clear();
        _waveBegin.Clear();
        _toggle.Clear();
        _input?.SetToggleEffects(_toggle);
        _input?.ClearFingerLocks();
    }

    public void Register(object effect)
    {
        if (effect is IRoundBeginEffect r) _roundBegin.Add(r);
        if (effect is IWaveBeginEffect w) _waveBegin.Add(w);
        if (effect is IToggleEffect t)
        {
            _toggle.Add(t);
            _toggle.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }

    public void OnRoundBegin()
    {
        for (int i = 0; i < _roundBegin.Count; i++) _roundBegin[i].OnRoundBegin(this);
    }

    public void OnWaveBegin()
    {
        _input?.ClearFingerLocks();
        for (int i = 0; i < _waveBegin.Count; i++) _waveBegin[i].OnWaveBegin(this);
    }

    void IInputControl.ApplyBindings(IReadOnlyDictionary<EFingerType, Key> bindings) => _input?.SetSingleControlBindings(bindings);
    void IInputControl.LockFinger(EFingerType finger, bool isOpen) => _input?.LockFinger(finger, isOpen);
    void IInputControl.ClearLocks() => _input?.ClearFingerLocks();
}
