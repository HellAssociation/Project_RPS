using SystemEnums;
using UnityEngine;


public class ModalPanel : PanelBase
{
    #region [Function] Inheritance
    public override bool IsOpened => _panelGameObject.activeSelf;
    public override bool CanCloseWithESC => false;
    public override bool IsStackable => false;
    public override EUIType UIType => EUIType.Modal;
    #endregion

    [SerializeField] ModalContent[] _contents;

    ModalContent _active;

    protected override void Awake()
    {
        base.Awake();

        foreach (ModalContent content in _contents)
        {
            if (content == null) continue;
            content.gameObject.SetActive(false);
        }
    }

    public bool TryShow<T>(out T content) where T : ModalContent
    {
        content = FindContent<T>();
        if (content == null) return false;

        Show(content);
        return true;
    }

    public override void ClosePanel()
    {
        HideActive();
        base.ClosePanel();
    }

    void Show(ModalContent content)
    {
        if (_active == content && IsOpened) return;

        HideActive();

        _active = content;
        _active.OnRequestClose += HandleContentRequestedClose;
        _active.gameObject.SetActive(true);
        _active.Activate();

        App.SystemManager.Input.SetBlockedByModal(true);

        OpenPanel();
    }

    void HideActive()
    {
        if (_active == null) return;

        _active.OnRequestClose -= HandleContentRequestedClose;
        _active.Deactivate();
        _active.gameObject.SetActive(false);
        _active = null;

        App.SystemManager.Input.SetBlockedByModal(false);
    }

    void HandleContentRequestedClose() => ClosePanel();

#if UNITY_EDITOR
    // TEST: delete this method when done
    [ContextMenu("Test/Show Starforce")]
    void DebugShowStarforceModal()
    {
        TryShow<StarforceModalContent>(out _);
    }
#endif

    T FindContent<T>() where T : ModalContent
    {
        foreach (ModalContent content in _contents)
        {
            if (content is T typed) return typed;
        }

        return null;
    }
}
