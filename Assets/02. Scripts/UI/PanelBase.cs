using System;
using SystemEnums;
using UnityEngine;

[DefaultExecutionOrder((int)EExecutionOrder.UIManagement)]
public abstract class PanelBase : MonoBehaviour
{
    [SerializeField] protected GameObject _panelGameObject;
    public virtual bool IsOpened => _panelGameObject.activeSelf;

    public virtual bool CanCloseWithESC => false;

    public virtual bool IsStackable => false;

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

    // Converts a screen point to the canvas' local space (overlay canvases use a null camera).
    protected static bool TryGetCanvasLocalPoint(Canvas canvas, Vector2 screenPos, out Vector2 localPos)
    {
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)canvas.transform, screenPos, cam, out localPos);
    }
}
