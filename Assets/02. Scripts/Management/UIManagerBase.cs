using System;
using System.Collections.Generic;
using SystemEnums;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder((int)EExecutionOrder.BaseManagement)]
public abstract class UIManagerBase : CommonManagerBase
{
    private Dictionary<Type, PanelBase> _panelDictionary = new();
    private Stack<PanelBase> _panelStack = new();

    protected override void Awake()
    {
        base.Awake();
        _panelDictionary = new();
        _panelStack = new();
    }

    protected virtual void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (_panelStack.TryPeek(out PanelBase top) && top != null && top.CanCloseWithESC)
        {
            top.ClosePanel();
        }
    }

    public void RegisterPanel(PanelBase panel)
    {
        if (panel == null)
        {
            return;
        }

        if (_panelDictionary.ContainsKey(panel.GetType()))
        {
            Debug.LogError($"Panel {panel.GetType().Name} already registered");
            return;
        }

        _panelDictionary.Add(panel.GetType(), panel);
    }

    public void PushUIStack(PanelBase panel)
    {
        if (panel == null)
        {
            return;
        }

        _panelStack.Push(panel);
    }

    public void PopUIStack(PanelBase panel)
    {
        if (_panelStack.Count > 0)
        {
            _panelStack.Pop();
        }
    }

    public bool TryGetPanel<T>(out T panel) where T : PanelBase
    {
        if (_panelDictionary.TryGetValue(typeof(T), out PanelBase value) && value is T typedPanel)
        {
            panel = typedPanel;
            return true;
        }

        panel = default;
        return false;
    }

    public T GetPanel<T>() where T : PanelBase
    {
        if (TryGetPanel(out T panel))
        {
            return panel;
        }

        throw new InvalidOperationException($"[{GetType().Name}] Panel '{typeof(T).Name}' is not registered.");
    }

    public bool OpenPanel<T>() where T : PanelBase
    {
        if (TryGetPanel(out T panel))
        {
            panel.OpenPanel();
            return true;
        }

        return false;
    }

    public bool ClosePanel<T>() where T : PanelBase
    {
        if (TryGetPanel(out T panel))
        {
            panel.ClosePanel();
            return true;
        }

        return false;
    }
}
