using System;
using UnityEngine;

/// <summary>
/// ModalPanel 안에서 보여지는 개별 콘텐츠(스타포스류 미니게임 등)의 베이스.
/// ModalPanel은 등록된 콘텐츠 중 타입으로 찾아 활성화/비활성화만 담당하고,
/// 콘텐츠는 자신의 진행 상태에 따라 RequestClose로 종료를 요청한다.
/// </summary>
public abstract class ModalContent : MonoBehaviour
{
    public event Action OnRequestClose;

    public abstract void Activate();
    public abstract void Deactivate();

    protected void RequestClose() => OnRequestClose?.Invoke();
}
