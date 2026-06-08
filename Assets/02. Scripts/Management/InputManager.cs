using System;
using SystemEnums;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 인게임 손가락(RPS) 입력을 중앙에서 처리합니다.
/// Space bar로 배정된 손가락의 펴기/접기를 토글합니다.
/// </summary>
[DefaultExecutionOrder((int)EExecutionOrder.SystemHandler)]
public class InputManager : CommonManagerBase
{
    public event Action<bool> OnFingerToggled;

    public bool IsEnabled { get; private set; } = true;
    public bool IsFingerExtended { get; private set; }

    /// <summary>모달(스타포스 등)이 열려 있는 동안 Space 입력이 손가락 토글로 새지 않도록 막는다.</summary>
    public bool IsBlockedByModal { get; private set; }

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

        if (Keyboard.current[Key.Space].wasPressedThisFrame)
        {
            ToggleFinger();
        }
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
    }
}
