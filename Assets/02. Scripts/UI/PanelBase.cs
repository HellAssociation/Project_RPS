using System;
using SystemEnums;
using UnityEngine;

[DefaultExecutionOrder((int)EExecutionOrder.UIManagement)]
public abstract class PanelBase : MonoBehaviour
{
    [SerializeField] protected GameObject _panelGameObject;
    public abstract bool IsOpened { get; }

    public abstract bool CanCloseWithESC { get; }

    public abstract bool IsStackable { get; }

    public abstract EUIType UIType { get; }

    public virtual Type PanelType => GetType();

    protected virtual void Awake()
    {
        if (_panelGameObject == null)
        {
            _panelGameObject = gameObject;
        }

        _panelGameObject.SetActive(IsOpened);
        RegisterToUIManager();
    }

    void RegisterToUIManager()
    {
        if (App.UI.Current == null)
        {
            Debug.LogError($"[{GetType().Name}] UI Manager가 없어 패널을 등록하지 못했습니다.");
            return;
        }

        App.UI.Current.RegisterPanel(this);
    }

    public virtual void OpenPanel()
    {
        if (IsStackable && !IsOpened && App.UI.Current != null)
        {
            App.UI.Current.PushUIStack(this);
        }

        _panelGameObject.SetActive(true);
    }

    public virtual void ClosePanel()
    {
        if (IsStackable && IsOpened && App.UI.Current != null)
        {
            App.UI.Current.PopUIStack(this);
        }

        _panelGameObject.SetActive(false);
    }
}
