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
            ResetFingerState();
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

    public void ResetFingerState()
    {
        IsFingerExtended = false;
        ExtendedFingersMask = EFingerType.None;
    }
}
