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
            if (Keyboard.current[binding.Value].wasPressedThisFrame)
            {
                ExtendedFingersMask ^= binding.Key;
                changed = true;
            }
        }

        if (changed)
            OnSingleControlMaskChanged?.Invoke(ExtendedFingersMask);
    }

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

    public void ResetFingerState()
    {
        IsFingerExtended = false;
        ExtendedFingersMask = EFingerType.None;
    }
}
