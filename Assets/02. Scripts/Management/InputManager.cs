using System;
using System.Collections.Generic;
using SystemEnums;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder((int)EExecutionOrder.SystemHandler)]
public class InputManager : CommonManagerBase
{
    public event Action<bool> OnFingerToggled;
    public event Action<EFingerType> OnSingleControlMaskChanged;

    public bool IsEnabled { get; private set; } = true;
    public bool IsFingerExtended { get; private set; }
    public EFingerType ExtendedFingersMask { get; private set; } = EFingerType.None;

    public bool IsBlockedByModal { get; private set; }

    public Dictionary<EFingerType, Key> SingleControlKeyBindings = new()
    {
        { EFingerType.Index,  Key.Digit1 },
        { EFingerType.Thumb,  Key.Digit2 },
        { EFingerType.Pinky,  Key.Digit3 },
        { EFingerType.Middle, Key.Digit4 },
        { EFingerType.Ring,   Key.Digit5 },
    };

    List<IToggleEffect> _toggleEffects;
    readonly HashSet<EFingerType> _lockedFingers = new();

    void Update()
    {
        if (!IsEnabled || IsBlockedByModal || !App.IsGameScene)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (ModeData.IsSingleControl)
            HandleSingleControlInput();
        else
            HandleMultiControlInput();
    }

    void HandleMultiControlInput()
    {
        if (Keyboard.current[Key.Space].wasPressedThisFrame)
            ToggleFinger();
    }

    void HandleSingleControlInput()
    {
        bool changed = false;

        foreach (KeyValuePair<EFingerType, Key> binding in SingleControlKeyBindings)
        {
            EFingerType finger = binding.Key;
            if (_lockedFingers.Contains(finger) || !WasKeyPressed(binding.Value))
                continue;

            bool willOpen = (ExtendedFingersMask & finger) == 0;
            if (!ToggleAllowed(finger, willOpen))
                continue;

            ExtendedFingersMask ^= finger;
            NotifyToggled(finger, willOpen);
            changed = true;
        }

        if (changed)
            OnSingleControlMaskChanged?.Invoke(ExtendedFingersMask);
    }

    bool ToggleAllowed(EFingerType finger, bool willOpen)
    {
        if (_toggleEffects == null) return true;
        for (int i = 0; i < _toggleEffects.Count; i++)
            if (!_toggleEffects[i].CanToggle(finger, willOpen))
                return false;
        return true;
    }

    void NotifyToggled(EFingerType finger, bool willOpen)
    {
        if (_toggleEffects == null) return;
        for (int i = 0; i < _toggleEffects.Count; i++)
            _toggleEffects[i].OnToggled(finger, willOpen);
    }

    static bool WasKeyPressed(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard[key].wasPressedThisFrame)
            return true;

        Key numpad = ToNumpadKey(key);
        return numpad != key && keyboard[numpad].wasPressedThisFrame;
    }

    static Key ToNumpadKey(Key digitKey) => digitKey switch
    {
        Key.Digit1 => Key.Numpad1,
        Key.Digit2 => Key.Numpad2,
        Key.Digit3 => Key.Numpad3,
        Key.Digit4 => Key.Numpad4,
        Key.Digit5 => Key.Numpad5,
        _ => digitKey,
    };

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;

        if (!enabled)
        {
            IsFingerExtended = false;
        }
    }

    public void SetBlockedByModal(bool isBlocked)
    {
        IsBlockedByModal = isBlocked;
    }

    public void ToggleFinger()
    {
        SetFingerExtended(!IsFingerExtended);
    }

    public void SetFingerExtended(bool isExtended)
    {
        if (IsFingerExtended == isExtended)
        {
            return;
        }

        IsFingerExtended = isExtended;
        OnFingerToggled?.Invoke(isExtended);
    }

    public void RandomizeSingleControlBindings()
    {
        EFingerType[] fingers =
        {
            EFingerType.Thumb, EFingerType.Index, EFingerType.Middle,
            EFingerType.Ring,  EFingerType.Pinky,
        };
        Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

        var rng = new System.Random();
        for (int i = fingers.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (fingers[i], fingers[j]) = (fingers[j], fingers[i]);
        }

        SingleControlKeyBindings.Clear();
        for (int i = 0; i < keys.Length; i++)
            SingleControlKeyBindings[fingers[i]] = keys[i];
    }

    public void SetSingleControlBindings(IReadOnlyDictionary<EFingerType, Key> bindings)
    {
        SingleControlKeyBindings.Clear();
        foreach (KeyValuePair<EFingerType, Key> kv in bindings)
            SingleControlKeyBindings[kv.Key] = kv.Value;
    }

    public void SetToggleEffects(List<IToggleEffect> effects) => _toggleEffects = effects;

    public void LockFinger(EFingerType finger, bool isOpen)
    {
        _lockedFingers.Add(finger);
        if (isOpen) ExtendedFingersMask |= finger;
        else        ExtendedFingersMask &= ~finger;
        OnSingleControlMaskChanged?.Invoke(ExtendedFingersMask);
    }

    public void ClearFingerLocks() => _lockedFingers.Clear();

    // Forces the single-control hand to an exact finger mask (dev/cheat use).
    public void SetExtendedFingersMask(EFingerType mask)
    {
        ExtendedFingersMask = mask;
        OnSingleControlMaskChanged?.Invoke(mask);
    }

    public void ResetFingerState()
    {
        IsFingerExtended = false;
        ExtendedFingersMask = EFingerType.None;
        _lockedFingers.Clear();
    }
}
