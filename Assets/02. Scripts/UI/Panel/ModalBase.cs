using SystemEnums;
using UnityEngine;

public abstract class ModalBase : PanelBase
{
    public override bool CanCloseWithESC => false;

    public override bool IsStackable => true;

    protected override void Awake()
    {
        if (_panelGameObject == null)
        {
            _panelGameObject = gameObject;
        }

        _panelGameObject.SetActive(IsOpened);

        if (App.UI.Current == null)
        {
            Debug.LogError($"[{GetType().Name}] UI Manager가 없어 모달을 등록하지 못했습니다.");
            return;
        }

        App.UI.Current.RegisterModal(this);
    }

    public override void OpenPanel()
    {
        if (IsOpened)
        {
            return;
        }

        App.SystemManager.Input.SetBlockedByModal(true);
        base.OpenPanel();
        OnModalOpened();
    }

    public override void ClosePanel()
    {
        if (!IsOpened)
        {
            return;
        }

        OnModalClosed();
        base.ClosePanel();
        App.SystemManager.Input.SetBlockedByModal(false);
    }

    protected virtual void OnModalOpened() { }

    protected virtual void OnModalClosed() { }
}
